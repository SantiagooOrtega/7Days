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
        /// <summary>Recarga por eventos (cargador, cartuchos, tambor, cerrojo): la animación, el sonido y la munición se sincronizan con ella.</summary>
        public ReloadState Reload { get; } = new ReloadState();
        public bool Reloading => Reload.Active;
        public float ReloadProgress => Reload.Active ? Reload.Progress : 0f;
        /// <summary>Sacar el arma al cambiar (0 guardada .. 1 lista).</summary>
        public DrawState Draw { get; } = new DrawState();
        public GunParts Parts { get; private set; }
        /// <summary>Instante del último disparo (corredera) y del ciclo de bombeo/cerrojo.</summary>
        public float LastShotTime { get; private set; } = -10f;
        public float ActionStart { get; private set; } = -10f;
        // cuerpo a cuerpo
        public bool SwingHeavy { get; private set; }
        public bool SwingMirror { get; private set; }
        public bool MeleeCharging { get; private set; }
        public float MeleeCharge => MeleeCharging ? Mathf.Clamp01((Time.time - _meleeHoldStart) / MeleeSwing.HeavyHold) : 0f;
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
        private float _meleeHoldStart, _meleeImpactAt = -1f, _meleeReadyAt, _meleeScale = 1f, _meleeKnock = 1f;

        private PlayerCamera _camera;
        private PlayerMotor _motor;
        private PlayerImpulse _impulse;
        private WeaponHandling _handling;
        private Health _health;
        private PlayerCover _cover;
        private float _damageScale = 1f, _nextNoLineWarning;
        private bool _reloadQueued;
        private readonly RaycastHit[] _hits = new RaycastHit[16];
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
            _cover = GetComponent<PlayerCover>();
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

            // cobertura: el clic izquierdo asoma al personaje (por encima o por el lado) y dispara desde ahí; Ctrl + clic, o sin hueco, dispara a ciegas
            bool inCover = _cover != null && _cover.Active && !data.IsMelee;
            var coverShot = CoverFireRules.Decide(inCover, data.IsMelee, GameInput.CrouchHeld, inCover ? _cover.PopDir : PopDirection.None);
            if (coverShot == CoverShot.PopAndFire && (GameInput.FireHeld || GameInput.FirePressed) && !Reloading) _cover.RequestFirePop();
            if (_reloadQueued && (_cover == null || _cover.CanReload)) { _reloadQueued = false; StartReload(); }

            if (GameInput.FireModePressed && FireControl.Cycle(data)) HudMessages.Add("Modo de disparo: " + FireModes.Label(FireControl.Mode), Color.white, 1.2f);
            Draw.Tick(dt);
            TickReload(dt);
            if (GameInput.ReloadPressed && !data.IsMelee) StartReload();
            // la escopeta deja de recargar para disparar si ya tiene algún cartucho dentro
            if (GameInput.FirePressed && Reload.CanInterruptToFire) { Reload.Cancel(); _inventory.NotifyChanged(); }
            if (data.IsMelee) { UpdateMelee(data); return; }
            if (!Draw.Ready) return;   // sacando el arma

            bool wants = FireControl.Wants(Time.time, GameInput.FirePressed, GameInput.FireHeld);
            if (!wants || Reloading || _motor.IsVaulting) return; // no se ataca mientras se salta un obstáculo

            // asomándose: solo dispara cuando ya está fuera (el clic se recuerda 0.15 s y asomarse tarda ~0.1 s)
            bool blind = coverShot == CoverShot.Blind;
            if (coverShot == CoverShot.PopAndFire && !_cover.Exposure.CanShootAimed) return;

            if (Current.Magazine <= 0)
            {
                FireControl.Cancel(Time.time, 0.4f);
                _audio.PlayOneShot(ProceduralAudio.Empty(), 0.6f);
                StartReload();
                if (!Reloading && _inventory.Reserve(Current) <= 0) HudMessages.Add("Sin munición de " + PlayerInventory.AmmoName(data.ammoType), new Color(1f, 0.6f, 0.4f));
            }
            else if (Fire(blind)) FireControl.Commit(Time.time, data);
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

            Reload.Cancel(); // cambiar de arma cancela la recarga (los cartuchos de escopeta ya metidos se quedan)
            _reloadQueued = false;
            MeleeCharging = false; _meleeImpactAt = -1f;
            Draw.Holster();   // guardar y sacar: animación de 0.35 s
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
            if (_cover != null && !_cover.CanReload)
            {
                // solo se recarga cubierto: si estaba asomado por disparar, vuelve a cubrirse y recarga en cuanto esté a cubierto
                if (_camera.Aiming) { HudMessages.Add("Cúbrete para recargar", new Color(1f, 0.85f, 0.5f), 1.2f); return; }
                _cover.CancelFirePop();
                _reloadQueued = true;
                return;
            }
            if (!Reload.Begin(w.Data, w.Data.magazineSize - w.Magazine, _inventory.Reserve(w))) return;
            FireControl.Cancel(Time.time);
            ReloadStarted?.Invoke();
        }

        /// <summary>Avanza la recarga: cada evento suena, y la munición entra en el momento exacto (cargador nuevo o cada cartucho).</summary>
        private void TickReload(float dt)
        {
            if (!Reload.Active) return;
            var w = Current;
            var events = Reload.Tick(dt, out int ammo);
            if (ammo > 0)
            {
                int taken = _inventory.Ammo.Take(w.Data.ammoType, Mathf.Min(ammo, w.Data.magazineSize - w.Magazine));
                w.Magazine += taken;
                _inventory.NotifyChanged();
            }
            for (int i = 0; i < events.Count; i++)
            {
                switch (events[i])
                {
                    case ReloadEvent.MagOut: case ReloadEvent.CylinderOpen: case ReloadEvent.BoltOpen: _audio.PlayOneShot(ProceduralAudio.Action(false), 0.45f); break;
                    case ReloadEvent.MagIn: case ReloadEvent.ShellIn: _audio.PlayOneShot(ProceduralAudio.Reload(), 0.5f); break;
                    case ReloadEvent.Slap: case ReloadEvent.CylinderClose: case ReloadEvent.BoltClose: _audio.PlayOneShot(ProceduralAudio.Action(true), 0.55f); break;
                    case ReloadEvent.Done: if (w.Data.kind == WeaponKind.Shotgun) { ActionStart = Time.time; _audio.PlayOneShot(ProceduralAudio.Action(true), 0.6f); } break;   // bombeo final
                }
            }
        }

        // ------------------------------------------------------------------ disparo
        private bool Fire(bool blind)
        {
            var w = Current;
            var data = w.Data;
            bool inCover = _cover != null && _cover.Active;

            // El rayo sale de la cámara en la dirección de la MIRA (rotación lógica con retroceso, sin el balanceo de la carrera ni las sacudidas). El origen es el punto
            // de la línea de mira más cercano al pecho del jugador (en tercera persona, para no chocar con objetos entre la cámara y el jugador) o, en cobertura, a la
            // posición ASOMADA (cabeza/arma por encima o por el lado, o la mano sobre el borde a ciegas). Si queda dentro de la cobertura se saca por su borde libre.
            Transform cam = _camera.Cam.transform;
            Vector3 forward = _camera.AimForward;
            Quaternion aimRot = _camera.AimRotation;
            Vector3 body = inCover ? _cover.PopBase(blind) : transform.position + Vector3.up * _camera.PivotHeight;
            Vector3 origin = cam.position + forward * (_camera.FirstPersonBlend > 0.5f ? 0f : Mathf.Max(0f, Vector3.Dot(body - cam.position, forward)));
            if (inCover)
            {
                if (blind) origin = body;      // a ciegas el arma sale de la mano sobre el borde, no de la línea de la cámara
                _cover.ResolveOrigin(ref origin, blind);
            }

            // sin línea de tiro libre (un muro pegado al cañón que NO es la propia cobertura): no se dispara y se avisa sin gastar munición
            if (inCover && !HasFreeLine(origin, forward))
            {
                if (Time.time >= _nextNoLineWarning) { _nextNoLineWarning = Time.time + 1.2f; HudMessages.Add("Sin línea de tiro", new Color(1f, 0.75f, 0.45f), 1.2f); }
                FireControl.Cancel(Time.time, 0.25f);
                return false;
            }

            w.Magazine--;
            LastShotTime = Time.time;
            Stats.ShotsFired++;
            ShotFired?.Invoke();
            _motor.FaceCameraUntil = Time.time + 0.5f;
            var cfg = _camera.Settings;
            float spread = blind ? BlindFire.Spread(data, cfg) : CurrentSpread;
            _damageScale = blind ? cfg.blindDamageMultiplier : 1f;
            bool ignoreOwn = inCover && (blind || _cover.Exposure.Value > 0.2f);
            // asomado: el disparo va desde la posición asomada HACIA el punto que marca la retícula (visto desde la cámara, atravesando solo su propia
            // cobertura), no paralelo a la cámara: así no se clava en el muro aunque la cámara quede baja
            if (inCover && !blind) aimRot = Quaternion.LookRotation(CrosshairTarget(cam.position, forward, data.range) - origin);
            Vector3 tracerStart = inCover ? origin : _muzzle.position;   // en cobertura el cañón visual puede estar detrás del muro

            bool anyHit = false;
            for (int p = 0; p < data.pellets; p++)
            {
                Vector3 dir = SpreadDirection(aimRot, spread);
                Vector3 end = origin + dir * data.range;
                if (CastShot(origin, dir, data.range, ignoreOwn, out RaycastHit hit))
                {
                    end = hit.point;
                    if (ApplyHit(hit, dir, data, Vector3.Distance(origin, hit.point), false)) anyHit = true;
                    if (p < 3) _fx.SpawnImpact(hit.point, hit.normal); // con muchos perdigones solo se dibujan unos pocos efectos
                }
                if (p < 3) _fx.SpawnTracer(tracerStart, end);
            }
            _damageScale = 1f;
            if (anyHit) { Stats.ShotsHit++; HitConfirmed?.Invoke(); }
            if (blind) _cover.OnBlindShot();
            if (inCover && PlayerCover.DebugLogs) LogCoverShot(blind, origin, ignoreOwn, aimRot);

            // retroceso (cámara, recuperable), dispersión, kickback visual, casquillo, sonido y sacudida
            _camera.AddRecoil(data, _motor.IsCrouched);
            Spread.OnShot(data, _camera.Aim.Eased > 0.5f);
            _handling?.OnShot();
            if (data.ejectsCasings && data.kind != WeaponKind.Shotgun && data.kind != WeaponKind.SniperRifle && data.kind != WeaponKind.Revolver)
                _fx.SpawnCasing(Parts != null ? Parts.CasingPoint.position : _muzzle.position, _weaponPose.right, _weaponPose.up, 1f);   // escopeta y cerrojo expulsan al ciclar
            _audio.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            _audio.PlayOneShot(ProceduralAudio.Shot(data.kind), 0.8f);
            if (data.DefaultFireMode == FireModes.Pump || data.DefaultFireMode == FireModes.Bolt) StartCoroutine(PlayAction(data.DefaultFireMode == FireModes.Pump));
            _impulse?.Fire(data.fireShake);
            _flash.Flash();
            _inventory.NotifyChanged();
            return true;
        }

        /// <summary>
        /// Rayo del disparo. Con "ignoreOwn" (asomado y pegado a su cobertura) se salta SOLO el collider de la propia cobertura y solo en el primer metro: cualquier otra
        /// cobertura o muro, y la propia cobertura más lejos, siguen deteniendo la bala.
        /// </summary>
        private bool CastShot(Vector3 origin, Vector3 dir, float range, bool ignoreOwn, out RaycastHit best)
        {
            if (!ignoreOwn || _cover == null || _cover.CoverCollider == null)
                return Physics.Raycast(origin, dir, out best, range, ShotMask, QueryTriggerInteraction.Ignore);
            var own = _cover.CoverCollider;
            int n = Physics.RaycastNonAlloc(origin, dir, _hits, range, ShotMask, QueryTriggerInteraction.Ignore);
            best = default; float bestD = float.MaxValue; bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.collider == own && h.distance < 1f) continue;
                if (h.distance < bestD) { bestD = h.distance; best = h; found = true; }
            }
            return found;
        }

        /// <summary>Punto que marca la retícula: el primer impacto del rayo de la cámara, sin contar la propia cobertura (desde detrás del muro la mira "ve" más allá).</summary>
        private Vector3 CrosshairTarget(Vector3 camPos, Vector3 forward, float range)
        {
            var own = _cover != null ? _cover.CoverCollider : null;
            int n = Physics.RaycastNonAlloc(camPos, forward, _hits, range + 10f, ShotMask, QueryTriggerInteraction.Ignore);
            float best = range + 10f;
            for (int i = 0; i < n; i++) if (_hits[i].collider != own && _hits[i].distance > 0.5f && _hits[i].distance < best) best = _hits[i].distance;
            return camPos + forward * best;
        }

        private void LogCoverShot(bool blind, Vector3 origin, bool ignoreOwn, Quaternion aimRot)
        {
            Vector3 dir = aimRot * Vector3.forward;
            string hit = CastShot(origin, dir, Current.Data.range, ignoreOwn, out RaycastHit h) ? h.collider.name + " a " + h.distance.ToString("0.00") + " m" + (h.collider == _cover.CoverCollider ? " (¡SU PROPIA COBERTURA!)" : "") : "nada";
            var e = _cover.LastEval;
            Debug.Log("[7Days][Cobertura] disparo " + (blind ? "A CIEGAS" : "asomado") + " | estado " + _cover.Exposure.State + " exposición " + _cover.Exposure.Value.ToString("0.00")
                + " | lado " + _cover.PopDir + " | borde sup. " + e.Top.ToString("0.00") + " m (baja " + e.Low + ", libre arriba " + e.OverClear + ", bordes " + e.EdgeLeft + "/" + e.EdgeRight + ")"
                + " | origen " + origin.ToString("F2") + " (pies " + transform.position.ToString("F2") + ") | ignora su cobertura " + ignoreOwn + " | golpea: " + hit
                + (blind || _cover.PopDir == PopDirection.None ? " | no se asomó: " + (_camera.Aiming ? _cover.WhyNoPop() : "no apuntaba (clic izq. sin apuntar = a ciegas)") : ""));
        }

        /// <summary>¿Hay espacio delante del cañón? Los hitboxes de zombis cuentan como libre (son el objetivo) y la propia cobertura se ignora.</summary>
        private bool HasFreeLine(Vector3 origin, Vector3 dir)
        {
            var own = _cover != null ? _cover.CoverCollider : null;
            int n = Physics.RaycastNonAlloc(origin, dir, _hits, 0.7f, ShotMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _hits[i].collider;
                if (c == own || c.gameObject.layer == HitboxLayer.Index) continue;
                return false;
            }
            return true;
        }

        private System.Collections.IEnumerator PlayAction(bool pump)
        {
            yield return new WaitForSeconds(pump ? 0.3f : 0.45f);
            ActionStart = Time.time;   // la bomba o el cerrojo ciclan (WeaponHandling lo anima) y sale el casquillo
            _audio.pitch = 1f;
            _audio.PlayOneShot(ProceduralAudio.Action(pump), 0.7f);
            var d = Current.Data;
            if (Parts != null && d.ejectsCasings) _fx.SpawnCasing(Parts.CasingPoint.position, _weaponPose.right, _weaponPose.up, pump ? 1.6f : 1.2f);
        }

        // ------------------------------------------------------------------ cuerpo a cuerpo
        /// <summary>
        /// Clic: golpe normal. Mantener al menos 0.45 s y soltar: golpe cargado (x1.75 de daño, x1.5 de empuje, más lento). El daño se aplica en el instante del impacto
        /// de la animación (tras la anticipación), no al hacer clic. Golpes seguidos alternan el lado (combo).
        /// </summary>
        private void UpdateMelee(WeaponData data)
        {
            if (_meleeImpactAt > 0f && Time.time >= _meleeImpactAt) { _meleeImpactAt = -1f; MeleeImpact(data); }
            if (_motor.IsVaulting || !Draw.Ready) { MeleeCharging = false; return; }
            if (!MeleeCharging)
            {
                if (GameInput.FirePressed && Time.time >= _meleeReadyAt) { MeleeCharging = true; _meleeHoldStart = Time.time; }
                return;
            }
            if (GameInput.FireHeld) return;   // cargando
            MeleeCharging = false;
            StartSwing(data, MeleeSwing.IsHeavy(Time.time - _meleeHoldStart));
        }

        private void StartSwing(WeaponData data, bool heavy)
        {
            SwingMirror = Time.time - SwingStart < 0.9f && !SwingMirror;
            SwingStart = Time.time;
            SwingHeavy = heavy;
            _meleeImpactAt = Time.time + MeleeSwing.ImpactTime(heavy);
            _meleeReadyAt = Time.time + Mathf.Max(data.SecondsBetweenShots * (heavy ? MeleeSwing.HeavyCooldown : 1f), MeleeSwing.Total(heavy) * 0.8f);
            _meleeScale = heavy ? MeleeSwing.HeavyDamage : 1f;
            _meleeKnock = heavy ? MeleeSwing.HeavyKnockback : 1f;
            _motor.FaceCameraUntil = Time.time + 0.6f;
            ShotFired?.Invoke();
            MeleeSwung?.Invoke(heavy, SwingMirror ? 1 : 0);
            _audio.pitch = heavy ? 0.8f : 1f;
            _audio.PlayOneShot(ProceduralAudio.Swing(), heavy ? 0.9f : 0.7f);
        }

        private void MeleeImpact(WeaponData data)
        {
            Vector3 forward = Quaternion.Euler(0f, _camera.Yaw, 0f) * Vector3.forward;
            Vector3 origin = transform.position + Vector3.up * 1.1f;
            if (Physics.SphereCast(origin, 0.45f, forward, out RaycastHit hit, data.range, ShotMask, QueryTriggerInteraction.Ignore))
            {
                if (ApplyHit(hit, forward, data, hit.distance, true)) HitConfirmed?.Invoke();
                _fx.SpawnImpact(hit.point, hit.normal);
            }
            _impulse?.Fire(data.fireShake * (SwingHeavy ? 1.8f : 1f));
        }

        /// <summary>Aplica el daño del arma a lo que tocó el rayo: con falloff por distancia y, si es un hitbox, con su zona. Devuelve true si dañó a un enemigo.</summary>
        private bool ApplyHit(RaycastHit hit, Vector3 dir, WeaponData data, float distance, bool melee)
        {
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead) return false;
            var health = (target as Hitbox)?.Owner;
            var info = new DamageInfo
            {
                Amount = data.damage * DamageModel.Falloff(data, distance) * (melee ? _meleeScale : _damageScale),
                Point = hit.point, Direction = dir, Source = gameObject,
                ArmorPenetration = data.armorPenetration, HeadshotMultiplier = data.headshotMultiplier, Knockback = data.knockback * (melee ? _meleeKnock : 1f),
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
            Parts = GunParts.Build(data, _weaponPose);   // piezas separadas: corredera, cargador, bomba, cerrojo, tambor...
            _gunVisual = Parts.gameObject;
            _muzzle.localPosition = new Vector3(0f, 0f, data.length + 0.05f);
            // mira provisional: sobre el arma, a un cuarto de su largo (cámara de primera persona al apuntar con aimFirstPerson)
            SightPoint.localPosition = new Vector3(0f, (data.IsMelee ? data.thickness : 0.14f) * 0.5f + 0.05f, data.length * 0.25f);
            if (data.HasScope)
            {
                var scope = Prim.Create(PrimitiveType.Cylinder, "Scope", new Vector3(0f, 0.11f, data.length * 0.4f), new Vector3(0.05f, 0.14f, 0.05f), new Color(0.08f, 0.08f, 0.09f), _gunVisual.transform, false);
                scope.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                scope.layer = 2;
            }
            _camera.SightPoint = SightPoint;
            _camera.RefreshBodyRenderers();
        }
    }
}
