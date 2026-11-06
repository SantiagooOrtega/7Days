using System;
using System.Collections.Generic;
using UnityEngine;
using SevenDays.Combat;
using SevenDays.Core;
using SevenDays.Procedural;
using SevenDays.Weapons;

namespace SevenDays.Player
{
    /// <summary>Estadísticas de la partida; las usará la IA director para medir el rendimiento.</summary>
    public class PlayerStats
    {
        public int ShotsFired;
        public int ShotsHit;
        public int Kills;
        public float Accuracy => ShotsFired == 0 ? 0f : (float)ShotsHit / ShotsFired;
    }

    public struct ShotHit
    {
        public HitReport Report;
        public Vector3 Point;
        public float Distance;
        public string WeaponId;
        public bool Melee;
        public GameObject Target;
    }

    /// <summary>
    /// Disparo (raycast desde el centro de la cámara; la escopeta lanza varios perdigones), cuerpo a cuerpo, recarga con la reserva del tipo de
    /// munición y cambio de arma (teclas 1-5 o rueda). Las armas y la munición viven en PlayerInventory.
    /// </summary>
    public class PlayerShooting : MonoBehaviour
    {
        public IReadOnlyList<WeaponState> Weapons => _inventory.Weapons;
        public WeaponState Current => _inventory.Weapons[_current];
        public int CurrentReserve => _inventory.Reserve(Current);
        public bool Reloading { get; private set; }
        public float ReloadProgress => Reloading ? 1f - Mathf.Clamp01((_reloadEnd - Time.time) / Current.Data.reloadTime) : 0f;
        public PlayerStats Stats { get; } = new PlayerStats();
        public event Action HitConfirmed;
        /// <summary>Cada impacto en un enemigo, con zona, daño final y si mató. Lo usan el hitmarker, los números flotantes y las métricas del director.</summary>
        public event Action<ShotHit> ShotHitReported;
        public event Action<WeaponState> WeaponChanged;

        private PlayerInventory _inventory;
        private int _current;
        private float _nextFire;
        private float _reloadEnd;
        private float _swingStart = -10f;
        // Todo menos la capa 2 (jugador y cuerpo de movimiento de los zombis): los hitboxes (capa Hitbox) y el mundo sí cuentan, y el primero que toca el rayo
        // gana; así una pared o una cobertura delante del zombi bloquea la bala, y un brazo delante del torso se lleva el impacto.
        private const int ShotMask = ~(1 << 2);

        private PlayerCamera _camera;
        private PlayerMotor _motor;
        private Health _health;
        private Transform _weaponHolder;
        private Transform _muzzle;
        private GameObject _gunVisual;
        private ShotFx _fx;
        private MuzzleFlash _flash;

        public void Initialize(PlayerCamera cam, PlayerMotor motor, Transform weaponHolder, Transform muzzle, PlayerInventory inventory)
        {
            _camera = cam;
            _motor = motor;
            _health = GetComponent<Health>();
            _weaponHolder = weaponHolder;
            _muzzle = muzzle;
            _inventory = inventory;
            _fx = new ShotFx(new GameObject("ShotFxPool").transform);
            _flash = muzzle.gameObject.AddComponent<MuzzleFlash>();
            _inventory.WeaponAdded += OnWeaponAdded;
            BuildGunVisual();
        }

        private void OnDestroy() { if (_inventory != null) _inventory.WeaponAdded -= OnWeaponAdded; }

        /// <summary>Al recoger un arma nueva se queda con la que ya tienes; se elige con su número.</summary>
        private void OnWeaponAdded(WeaponState w) { }

        private void Update()
        {
            if (_camera == null || _health.IsDead) return;
            AnimateSwing();
            if (Cursor.lockState != CursorLockMode.Locked) return;

            HandleSwitch();

            var data = Current.Data;
            if (Reloading && Time.time >= _reloadEnd) FinishReload();
            if (GameInput.ReloadPressed && !data.IsMelee) StartReload();

            bool wantsFire = data.automatic ? GameInput.FireHeld : GameInput.FirePressed;
            if (!wantsFire || Reloading || _motor.IsVaulting || Time.time < _nextFire) return; // no se ataca mientras se salta un obstáculo

            if (data.IsMelee) Swing();
            else if (Current.Magazine <= 0) { StartReload(); if (!Reloading && _inventory.Reserve(Current) <= 0) HudMessages.Add("Sin munición de " + PlayerInventory.AmmoName(data.ammoType), new Color(1f, 0.6f, 0.4f)); _nextFire = Time.time + 0.4f; }
            else Fire();
        }

        // ------------------------------------------------------------------ cambio de arma
        private void HandleSwitch()
        {
            int count = _inventory.Weapons.Count;
            int slot = GameInput.WeaponSlotPressed;
            if (slot < 0)
            {
                float scroll = GameInput.Scroll;
                if (scroll > 0.1f) slot = (_current + 1) % count;
                else if (scroll < -0.1f) slot = (_current - 1 + count) % count;
            }
            if (slot < 0 || slot >= count || slot == _current) return;

            Reloading = false; // cambiar de arma cancela la recarga
            _current = slot;
            _nextFire = Time.time + 0.25f;
            BuildGunVisual();
            WeaponChanged?.Invoke(Current);
        }

        // ------------------------------------------------------------------ recarga
        private void StartReload()
        {
            var w = Current;
            if (Reloading || w.Data.IsMelee || w.Magazine >= w.Data.magazineSize || _inventory.Reserve(w) <= 0) return;
            Reloading = true;
            _reloadEnd = Time.time + w.Data.reloadTime;
        }

        private void FinishReload()
        {
            var w = Current;
            int taken = _inventory.Ammo.Take(w.Data.ammoType, w.Data.magazineSize - w.Magazine);
            w.Magazine += taken;
            Reloading = false;
            _inventory.NotifyChanged();
        }

        // ------------------------------------------------------------------ disparo
        private void Fire()
        {
            var w = Current;
            var data = w.Data;
            w.Magazine--;
            _nextFire = Time.time + data.SecondsBetweenShots;
            Stats.ShotsFired++;
            _motor.FaceCameraUntil = Time.time + 0.5f;

            // El rayo sale del centro de la cámara, adelantado hasta la altura del pecho del jugador
            // para no chocar con objetos que estén entre la cámara y el jugador.
            Transform cam = _camera.Cam.transform;
            Vector3 chest = transform.position + Vector3.up * _camera.PivotHeight;
            Vector3 origin = cam.position + cam.forward * Mathf.Max(0f, Vector3.Dot(chest - cam.position, cam.forward));
            float spread = _camera.Aiming ? data.spreadAim : data.spreadHip;

            bool anyHit = false;
            for (int p = 0; p < data.pellets; p++)
            {
                Vector3 dir = SpreadDirection(cam, spread);
                Vector3 end = origin + dir * data.range;
                if (Physics.Raycast(origin, dir, out RaycastHit hit, data.range, ShotMask, QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    if (ApplyHit(hit, dir, data, Vector3.Distance(origin, hit.point), false)) anyHit = true;
                    if (p < 3) _fx.SpawnImpact(hit.point, hit.normal); // con muchos perdigones solo se dibujan unos pocos efectos
                }
                if (p < 3) _fx.SpawnTracer(_muzzle.position, end);
            }
            if (anyHit) { Stats.ShotsHit++; HitConfirmed?.Invoke(); }

            _flash.Flash();
            _camera.Kick(data.recoil * (_camera.Aiming ? 0.6f : 1f));
            _inventory.NotifyChanged();
        }

        // ------------------------------------------------------------------ cuerpo a cuerpo
        private void Swing()
        {
            var data = Current.Data;
            _nextFire = Time.time + data.SecondsBetweenShots;
            _swingStart = Time.time;
            _motor.FaceCameraUntil = Time.time + 0.5f;

            Vector3 forward = Quaternion.Euler(0f, _camera.Yaw, 0f) * Vector3.forward;
            Vector3 origin = transform.position + Vector3.up * 1.1f;
            if (Physics.SphereCast(origin, 0.45f, forward, out RaycastHit hit, data.range, ShotMask, QueryTriggerInteraction.Ignore))
            {
                if (ApplyHit(hit, forward, data, hit.distance, true)) HitConfirmed?.Invoke();
                _fx.SpawnImpact(hit.point, hit.normal);
            }
            _camera.Kick(data.recoil);
        }

        /// <summary>Aplica el daño del arma a lo que tocó el rayo: con falloff por distancia y, si es un hitbox, con su zona. Devuelve true si dañó a un enemigo.</summary>
        private bool ApplyHit(RaycastHit hit, Vector3 dir, WeaponData data, float distance, bool melee)
        {
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead) return false;
            var health = (target as Hitbox)?.Owner;
            var info = new DamageInfo
            {
                Amount = data.damage * DamageModel.Falloff(data, distance),
                Point = hit.point, Direction = dir, Source = gameObject,
                ArmorPenetration = data.armorPenetration, HeadshotMultiplier = data.headshotMultiplier, Knockback = data.knockback,
                Distance = distance, WeaponId = data.id,
            };
            bool killed = target.TakeDamage(info);
            if (killed) Stats.Kills++;
            if (health != null && health.LastReport.Valid)
                ShotHitReported?.Invoke(new ShotHit { Report = health.LastReport, Point = hit.point, Distance = distance, WeaponId = data.id, Melee = melee, Target = health.gameObject });
            return true;
        }

        /// <summary>Balanceo visual de la barra: el arma baja y vuelve en 0.3 s.</summary>
        private void AnimateSwing()
        {
            if (_weaponHolder == null) return;
            float t = (Time.time - _swingStart) / 0.3f;
            float angle = t >= 0f && t <= 1f ? Mathf.Sin(t * Mathf.PI) * 80f : 0f;
            _weaponHolder.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        private static Vector3 SpreadDirection(Transform cam, float degrees)
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
            return (cam.forward + cam.right * r.x + cam.up * r.y).normalized;
        }

        private void BuildGunVisual()
        {
            if (_gunVisual != null) Destroy(_gunVisual);
            var data = Current.Data;
            var c = data.color;
            _gunVisual = Prim.Create(PrimitiveType.Cube, "Gun", new Vector3(0f, 0f, data.length * 0.5f),
                new Vector3(data.thickness, data.IsMelee ? data.thickness : 0.14f, data.length), new Color(c[0], c[1], c[2]), _weaponHolder, false);
            _gunVisual.layer = 2;
            _muzzle.localPosition = new Vector3(0f, 0f, data.length + 0.05f);
        }
    }
}
