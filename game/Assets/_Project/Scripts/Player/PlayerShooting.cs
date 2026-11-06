using System;
using System.Collections.Generic;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Weapons;

namespace SevenDays.Player
{
    /// <summary>Munición y estado de un arma concreta del jugador.</summary>
    public class WeaponState
    {
        public WeaponDefinition Def;
        public int Magazine;
        public int Reserve;

        public WeaponState(WeaponDefinition def)
        {
            Def = def;
            Magazine = def.magazineSize;
            Reserve = def.reserveAmmo;
        }
    }

    /// <summary>Estadísticas de la partida; las usará la IA director para medir el rendimiento.</summary>
    public class PlayerStats
    {
        public int ShotsFired;
        public int ShotsHit;
        public int Kills;
        public float Accuracy => ShotsFired == 0 ? 0f : (float)ShotsHit / ShotsFired;
    }

    /// <summary>Disparo (raycast desde el centro de la cámara), recarga y cambio de arma.</summary>
    public class PlayerShooting : MonoBehaviour
    {
        public IReadOnlyList<WeaponState> Weapons => _weapons;
        public WeaponState Current => _weapons[_current];
        public bool Reloading { get; private set; }
        public float ReloadProgress => Reloading ? 1f - Mathf.Clamp01((_reloadEnd - Time.time) / Current.Def.reloadTime) : 0f;
        public PlayerStats Stats { get; } = new PlayerStats();
        public event Action HitConfirmed;
        public event Action<WeaponState> WeaponChanged;

        private readonly List<WeaponState> _weapons = new List<WeaponState>();
        private int _current;
        private float _nextFire;
        private float _reloadEnd;

        private PlayerCamera _camera;
        private PlayerMotor _motor;
        private Health _health;
        private Transform _weaponHolder;
        private Transform _muzzle;
        private GameObject _gunVisual;
        private ShotFx _fx;
        private MuzzleFlash _flash;

        public void Initialize(PlayerCamera cam, PlayerMotor motor, Transform weaponHolder, Transform muzzle,
            IEnumerable<WeaponDefinition> weapons)
        {
            _camera = cam;
            _motor = motor;
            _health = GetComponent<Health>();
            _weaponHolder = weaponHolder;
            _muzzle = muzzle;
            foreach (var w in weapons) _weapons.Add(new WeaponState(w));
            _fx = new ShotFx(new GameObject("ShotFxPool").transform);
            _flash = muzzle.gameObject.AddComponent<MuzzleFlash>();
            BuildGunVisual();
        }

        private void Update()
        {
            if (_camera == null || _health.IsDead) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            HandleSwitch();

            if (Reloading && Time.time >= _reloadEnd) FinishReload();
            if (GameInput.ReloadPressed) StartReload();

            var def = Current.Def;
            bool wantsFire = def.automatic ? GameInput.FireHeld : GameInput.FirePressed;
            if (wantsFire && !Reloading && Time.time >= _nextFire)
            {
                if (Current.Magazine <= 0) StartReload();
                else Fire();
            }
        }

        private void HandleSwitch()
        {
            int slot = GameInput.WeaponSlotPressed;
            if (slot < 0)
            {
                float scroll = GameInput.Scroll;
                if (scroll > 0.1f) slot = (_current + 1) % _weapons.Count;
                else if (scroll < -0.1f) slot = (_current - 1 + _weapons.Count) % _weapons.Count;
            }
            if (slot < 0 || slot >= _weapons.Count || slot == _current) return;

            Reloading = false; // cambiar de arma cancela la recarga
            _current = slot;
            _nextFire = Time.time + 0.25f;
            BuildGunVisual();
            WeaponChanged?.Invoke(Current);
        }

        private void StartReload()
        {
            var w = Current;
            if (Reloading || w.Magazine >= w.Def.magazineSize || w.Reserve <= 0) return;
            Reloading = true;
            _reloadEnd = Time.time + w.Def.reloadTime;
        }

        private void FinishReload()
        {
            var w = Current;
            int taken = Mathf.Min(w.Def.magazineSize - w.Magazine, w.Reserve);
            w.Magazine += taken;
            w.Reserve -= taken;
            Reloading = false;
        }

        private void Fire()
        {
            var w = Current;
            var def = w.Def;
            w.Magazine--;
            _nextFire = Time.time + 60f / def.roundsPerMinute;
            Stats.ShotsFired++;
            _motor.FaceCameraUntil = Time.time + 0.5f;

            // El rayo sale del centro de la cámara, adelantado hasta la altura del pecho del jugador
            // para no chocar con objetos que estén entre la cámara y el jugador.
            Transform cam = _camera.Cam.transform;
            Vector3 chest = transform.position + Vector3.up * _camera.PivotHeight;
            Vector3 origin = cam.position + cam.forward * Mathf.Max(0f, Vector3.Dot(chest - cam.position, cam.forward));
            Vector3 dir = SpreadDirection(cam, _camera.Aiming ? def.spreadAim : def.spreadHip);

            Vector3 end = origin + dir * def.range;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, def.range, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead)
                {
                    Stats.ShotsHit++;
                    bool killed = target.TakeDamage(new DamageInfo
                    {
                        Amount = def.damage, Point = hit.point, Direction = dir, Source = gameObject,
                    });
                    if (killed) Stats.Kills++;
                    HitConfirmed?.Invoke();
                }
                _fx.SpawnImpact(hit.point, hit.normal);
            }
            _fx.SpawnTracer(_muzzle.position, end);
            _flash.Flash();
            _camera.Kick(def.recoil * (_camera.Aiming ? 0.6f : 1f));
        }

        private static Vector3 SpreadDirection(Transform cam, float degrees)
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
            return (cam.forward + cam.right * r.x + cam.up * r.y).normalized;
        }

        private void BuildGunVisual()
        {
            if (_gunVisual != null) Destroy(_gunVisual);
            var def = Current.Def;
            var c = def.color;
            _gunVisual = Prim.Create(PrimitiveType.Cube, "Gun", new Vector3(0f, 0f, def.length * 0.5f),
                new Vector3(0.09f, 0.14f, def.length), new Color(c[0], c[1], c[2]), _weaponHolder, false);
            _gunVisual.layer = 2;
            _muzzle.localPosition = new Vector3(0f, 0f, def.length + 0.05f);
        }
    }
}
