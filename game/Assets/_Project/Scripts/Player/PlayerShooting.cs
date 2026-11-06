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
    /// Disparo por raycast desde la mira de la cámara (la escopeta lanza varios perdigones), con modos de disparo (auto, semi, ráfaga, bombeo, cerrojo; X cambia),
    /// retroceso con patrón que se recupera, dispersión que crece al moverse y en ráfaga, kickback, casquillos y sonido. Cuerpo a cuerpo, recarga con la reserva del
    /// tipo de munición y cambio de arma (1-5 o rueda; con mira telescópica la rueda hace zoom). Las armas y la munición viven en PlayerInventory.
    /// </summary>
    public class PlayerShooting : MonoBehaviour
    {
        public IReadOnlyList<WeaponState> Weapons => _inventory.Weapons;
        public WeaponState Current => _inventory.Weapons[_current];
        public int CurrentReserve => _inventory.Reserve(Current);
        public bool Reloading { get; private set; }
        public float ReloadProgress => Reloading ? 1f - Mathf.Clamp01((_reloadEnd - Time.time) / Current.Data.reloadTime) : 0f;
        public PlayerStats Stats { get; } = new PlayerStats();
        public FireControl FireControl { get; } = new FireControl();
        public SpreadState Spread { get; } = new SpreadState();
        /// <summary>Dispersión actual en grados (la retícula se abre con ella).</summary>
        public float CurrentSpread { get; private set; }
        public float SwingStart { get; private set; } = -10f;
        public Transform SightPoint { get; private set; }

        public event Action HitConfirmed;
        /// <summary>Cada impacto en un enemigo, con zona, daño final y si mató. Lo usan el hitmarker, los números flotantes y las métricas del director.</summary>
        public event Action<ShotHit> ShotHitReported;
        /// <summary>Cada disparo o golpe, acierte o no (precisión del director).</summary>
        public event Action ShotFired;
        public event Action<WeaponState> WeaponChanged;
        public event Action ReloadStarted;
        /// <summary>Golpe cuerpo a cuerpo: (pesado, índice de combo). Hoy siempre (false, 0): hooks para el Animator.</summary>
        public event Action<bool, int> MeleeSwung;

        private PlayerInventory _inventory;
        private int _current;
        private float _reloadEnd;

        private PlayerCamera _camera;
        private PlayerMotor _motor;
        private PlayerImpulse _impulse;
        private WeaponHandling _handling;
        private Health _health;
        private Transform _weaponPose;
        private Transform _muzzle;
        private GameObject _gunVisual;
        private ShotFx _fx;
        private MuzzleFlash _flash;
        private AudioSource _audio;
        // Todo menos la capa 2 (jugador y cuerpo de movimiento de los zombis): los hitboxes (capa Hitbox) y el mundo sí cuentan, y el primero que toca el rayo
        // gana; así una pared o una cobertura delante del zombi bloquea la bala, y un brazo delante del torso se lleva el impacto.
        private const int ShotMask = ~(1 << 2);

        public void Initialize(PlayerCamera cam, PlayerMotor motor, Transform weaponPose, Transform muzzle, PlayerInventory inventory)
        {
            _camera = cam;
            _motor = motor;
            _health = GetComponent<Health>();
            _impulse = GetComponent<PlayerImpulse>();
            _handling = GetComponent<WeaponHandling>();
            _weaponPose = weaponPose;
            _muzzle = muzzle;
            _inventory = inventory;
            _fx = new ShotFx(new GameObject("ShotFxPool").transform);
            _flash = muzzle.gameObject.AddComponent<MuzzleFlash>();
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false; _audio.spatialBlend = 0.15f;
            SightPoint = new GameObject("SightPoint").transform;    // punto de mira: al importar un modelo real, colócalo en la mira del arma
            SightPoint.SetParent(weaponPose, false);
            _inventory.WeaponAdded += OnWeaponAdded;
            // no se puede apuntar durante el vault ni con un arma cuerpo a cuerpo
            _camera.CanAim = () => !_motor.IsVaulting && !Current.Data.IsMelee;
            OnWeaponEquipped();
        }

        private void OnDestroy() { if (_inventory != null) _inventory.WeaponAdded -= OnWeaponAdded; }

        /// <summary>Al recoger un arma nueva se queda con la que ya tienes; se elige con su número.</summary>
        private void OnWeaponAdded(WeaponState w) { }

        private void Update()
        {
            if (_camera == null || _health.IsDead) return;
            float dt = Time.deltaTime;
            var data = Current.Data;
            _camera.Weapon = data;

            Spread.Tick(dt, data);
            bool aimed = _camera.Aim.Eased > 0.5f;
            CurrentSpread = data.IsMelee ? 0f : Spread.Current(data, aimed, _motor.IsCrouched, _motor.Speed01, !_motor.IsGrounded);
            if (Cursor.lockState != CursorLockMode.Locked) return;

            HandleSwitch();
            data = Current.Data;

            if (GameInput.FireModePressed && FireControl.Cycle(data)) HudMessages.Add("Modo de disparo: " + FireModes.Label(FireControl.Mode), Color.white, 1.2f);
            if (Reloading && Time.time >= _reloadEnd) FinishReload();
            if (GameInput.ReloadPressed && !data.IsMelee) StartReload();

            bool wants = FireControl.Wants(Time.time, GameInput.FirePressed, GameInput.FireHeld);
            if (!wants || Reloading || _motor.IsVaulting) return; // no se ataca mientras se salta un obstáculo

            if (data.IsMelee) { Swing(); FireControl.Commit(Time.time, data); }
            else if (Current.Magazine <= 0)
            {
                FireControl.Cancel(Time.time, 0.4f);
                _audio.PlayOneShot(ProceduralAudio.Empty(), 0.6f);
                StartReload();
                if (!Reloading && _inventory.Reserve(Current) <= 0) HudMessages.Add("Sin munición de " + PlayerInventory.AmmoName(data.ammoType), new Color(1f, 0.6f, 0.4f));
            }
            else { Fire(); FireControl.Commit(Time.time, data); }
        }

        // ------------------------------------------------------------------ cambio de arma
        private void HandleSwitch()
        {
            int count = _inventory.Weapons.Count;
            int slot = GameInput.WeaponSlotPressed;
            // con mira telescópica la rueda hace zoom (lo lee PlayerCamera), no cambia de arma
            if (slot < 0 && !(_camera.Aiming && Current.Data.HasScope))
            {
                float scroll = GameInput.Scroll;
                if (scroll > 0.1f) slot = (_current + 1) % count;
                else if (scroll < -0.1f) slot = (_current - 1 + count) % count;
            }
            if (slot < 0 || slot >= count || slot == _current) return;

            Reloading = false; // cambiar de arma cancela la recarga
            _current = slot;
            OnWeaponEquipped();
            FireControl.Cancel(Time.time, 0.25f);
            WeaponChanged?.Invoke(Current);
        }

        private void OnWeaponEquipped()
        {
            var data = Current.Data;
            FireControl.SetWeapon(data);
            Spread.Reset();
            _camera.Recoil.Reset();
            _camera.Weapon = data;
            BuildGunVisual();
        }

        // ------------------------------------------------------------------ recarga
        private void StartReload()
        {
            var w = Current;
            if (Reloading || w.Data.IsMelee || w.Magazine >= w.Data.magazineSize || _inventory.Reserve(w) <= 0) return;
            Reloading = true;
            _reloadEnd = Time.time + w.Data.reloadTime;
            FireControl.Cancel(Time.time);
            _audio.PlayOneShot(ProceduralAudio.Reload(), 0.5f);
            ReloadStarted?.Invoke();
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
            Stats.ShotsFired++;
            ShotFired?.Invoke();
            _motor.FaceCameraUntil = Time.time + 0.5f;

            // El rayo sale de la cámara en la dirección de la MIRA (rotación lógica con retroceso, sin el balanceo de la carrera ni las sacudidas), adelantado hasta la
            // altura del pecho del jugador en tercera persona para no chocar con objetos entre la cámara y el jugador.
            Transform cam = _camera.Cam.transform;
            Vector3 forward = _camera.AimForward;
            Quaternion aimRot = _camera.AimRotation;
            Vector3 chest = transform.position + Vector3.up * _camera.PivotHeight;
            Vector3 origin = cam.position + forward * (_camera.FirstPersonBlend > 0.5f ? 0f : Mathf.Max(0f, Vector3.Dot(chest - cam.position, forward)));
            float spread = CurrentSpread;

            bool anyHit = false;
            for (int p = 0; p < data.pellets; p++)
            {
                Vector3 dir = SpreadDirection(aimRot, spread);
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

            // retroceso (cámara, recuperable), dispersión, kickback visual, casquillo, sonido y sacudida
            _camera.AddRecoil(data, _motor.IsCrouched);
            Spread.OnShot(data, _camera.Aim.Eased > 0.5f);
            _handling?.OnShot();
            if (data.ejectsCasings) _fx.SpawnCasing(_muzzle.position - _weaponPose.forward * data.length * 0.6f, _weaponPose.right, _weaponPose.up, data.kind == WeaponKind.Shotgun ? 1.6f : 1f);
            _audio.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            _audio.PlayOneShot(ProceduralAudio.Shot(data.kind), 0.8f);
            if (data.DefaultFireMode == FireModes.Pump || data.DefaultFireMode == FireModes.Bolt) StartCoroutine(PlayAction(data.DefaultFireMode == FireModes.Pump));
            _impulse?.Fire(data.fireShake);
            _flash.Flash();
            _inventory.NotifyChanged();
        }

        private System.Collections.IEnumerator PlayAction(bool pump)
        {
            yield return new WaitForSeconds(pump ? 0.3f : 0.45f);
            _audio.pitch = 1f;
            _audio.PlayOneShot(ProceduralAudio.Action(pump), 0.7f);
        }

        // ------------------------------------------------------------------ cuerpo a cuerpo
        private void Swing()
        {
            var data = Current.Data;
            SwingStart = Time.time;
            _motor.FaceCameraUntil = Time.time + 0.5f;
            ShotFired?.Invoke();
            MeleeSwung?.Invoke(false, 0);
            _audio.PlayOneShot(ProceduralAudio.Swing(), 0.7f);

            Vector3 forward = Quaternion.Euler(0f, _camera.Yaw, 0f) * Vector3.forward;
            Vector3 origin = transform.position + Vector3.up * 1.1f;
            if (Physics.SphereCast(origin, 0.45f, forward, out RaycastHit hit, data.range, ShotMask, QueryTriggerInteraction.Ignore))
            {
                if (ApplyHit(hit, forward, data, hit.distance, true)) HitConfirmed?.Invoke();
                _fx.SpawnImpact(hit.point, hit.normal);
            }
            _impulse?.Fire(data.fireShake);
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

        /// <summary>Dirección dentro de un cono de "degrees" grados alrededor de la mira.</summary>
        private static Vector3 SpreadDirection(Quaternion aim, float degrees)
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
            return (aim * new Vector3(r.x, r.y, 1f)).normalized;
        }

        private void BuildGunVisual()
        {
            if (_gunVisual != null) Destroy(_gunVisual);
            var data = Current.Data;
            var c = data.color;
            _gunVisual = Prim.Create(PrimitiveType.Cube, "Gun", new Vector3(0f, 0f, data.length * 0.5f),
                new Vector3(data.thickness, data.IsMelee ? data.thickness : 0.14f, data.length), new Color(c[0], c[1], c[2]), _weaponPose, false);
            _gunVisual.layer = 2;
            _muzzle.localPosition = new Vector3(0f, 0f, data.length + 0.05f);
            // mira provisional: sobre el arma, a un cuarto de su largo (cámara de primera persona al apuntar con aimFirstPerson)
            SightPoint.localPosition = new Vector3(0f, (data.IsMelee ? data.thickness : 0.14f) * 0.5f + 0.05f, data.length * 0.25f);
            if (data.HasScope)
            {
                var scope = Prim.Create(PrimitiveType.Cylinder, "Scope", new Vector3(0f, 0.11f, data.length * 0.4f), new Vector3(0.05f, 0.14f, 0.05f), new Color(0.08f, 0.08f, 0.09f), _gunVisual.transform.parent, false);
                scope.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                scope.layer = 2;
                scope.transform.SetParent(_gunVisual.transform, true);
            }
            _camera.SightPoint = SightPoint;
            _camera.RefreshBodyRenderers();
        }
    }
}
