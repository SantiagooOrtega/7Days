using UnityEngine;
using UnityEngine.AI;
using SevenDays.Core;
using SevenDays.Player;
using SevenDays.Procedural;
using SevenDays.World;

namespace SevenDays.Combat
{
    /// <summary>
    /// Ejecuta lo que decide BossBrain: avisa de cada ataque (animación, marca en el suelo, sonido y texto) y luego lo ejecuta. Carga recta contra cobertura y muros,
    /// lanzamiento de objetos que destruyen la cobertura, invocaciones, onda expansiva y puntos débiles. Cambia de color y de ritmo en cada fase de furia.
    /// </summary>
    public class BossBehaviour : MonoBehaviour
    {
        public bool Active { get; private set; }
        public BossBrain Brain { get; private set; }
        public ZombieTypeData Type { get; private set; }
        public string TelegraphLabel { get; private set; } = "";
        public bool Raging => Brain != null && Brain.Phase > 0;

        private ZombieController _z;
        private Health _health;
        private AudioSource _audio;
        private static AudioClip _roar, _warn, _boom, _whoosh;

        private enum Exec { None, Charging }
        private Exec _exec;
        private Vector3 _chargeDir;
        private float _chargeLeft, _chargeSpeed;
        private bool _chargeHitPlayer;
        private float _chargeDamageMult = 1f;
        private float _telegraphTotal;
        private BossAbilityData _current;
        private float _baseSpeedMult;
        private int _lastPhaseShown;
        private float _labelUntil;
        // animación de la habilidad: telegraph (0..0.5) y recuperación tras el golpe (0.5..1, ReleaseSeconds)
        private const float ReleaseSeconds = 0.6f, RoarSeconds = 1.3f;
        private BossPose _pose;
        private float _released = -1f, _roarStart = -1f;

        public void Begin(ZombieController z, ZombieTypeData type)
        {
            _z = z; Type = type; _health = z.Health;
            Brain = new BossBrain(type.boss, Random.Range(1, 100000));
            Active = true; _exec = Exec.None; _lastPhaseShown = 0; TelegraphLabel = "";
            _pose = BossPose.None; _released = -1f; _roarStart = -1f; z.BossPose = BossPose.None;
            _z.Rig.SetFury(0);
            _baseSpeedMult = z.SpeedMultiplier;
            _health.ZoneFilter = Brain.ResolveZone;
            _z.Rig.SetWeakPoint(false);
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.spatialBlend = 1f; _audio.maxDistance = 80f; _audio.rolloffMode = AudioRolloffMode.Linear; _audio.playOnAwake = false;
                AudioMix.Route(_audio, AudioGroup.Zombies);
            }
            _roar ??= Synth("roar", 1.3f, 85f, 38f, 0.9f, 0.55f);
            _warn ??= Synth("warn", 0.5f, 260f, 640f, 0.55f, 0.08f);
            _boom ??= Synth("boom", 0.6f, 95f, 28f, 0.95f, 0.3f);
            _whoosh ??= Synth("whoosh", 0.4f, 700f, 200f, 0.4f, 0.8f);
            HudMessages.Add(type.displayName + " ha aparecido", new Color(1f, 0.4f, 0.3f), 3.5f);
        }

        public void End()
        {
            Active = false;
            if (_health != null) _health.ZoneFilter = null;
        }

        public void OnDeath()
        {
            Active = false;
            _exec = Exec.None;
            TelegraphLabel = "";
            _z.PoseAttackOverride = -1f;
            _z.BossPose = BossPose.None;
            _z.Rig.SetWeakPoint(false);
            if (_health != null) _health.ZoneFilter = null;
            Roar(1f);
            if (ZombieManager.Instance != null) HudMessages.Add(Type.displayName + " derrotado", new Color(1f, 0.85f, 0.3f), 4f);
        }

        /// <summary>Devuelve true si el jefe toma el control del movimiento este frame (aviso, carga, aturdido); si no, el zombi usa su comportamiento normal de persecución.</summary>
        public bool Tick(float dt, Vector3 toPlayer, float dist, out float moveAmount)
        {
            moveAmount = 0f;
            _health.Combatant.VulnerabilityMultiplier = Brain.VulnerabilityMultiplier;
            var events = Brain.Tick(dt, _health.Fraction, dist, !_health.IsDead);
            for (int i = 0; i < events.Count; i++) Handle(events[i], toPlayer);

            _z.SpeedMultiplier = _baseSpeedMult * Brain.SpeedMultiplier;
            if (Time.time > _labelUntil && !Brain.Busy) TelegraphLabel = Brain.WallStunned ? "¡ATURDIDO! ¡DISPARA!" : "";

            UpdatePose();
            if (_exec == Exec.Charging) { Charge(dt); moveAmount = 1f; return true; }

            if (Brain.WallStunned) { _z.Stop(); _z.PoseAttackOverride = -1f; return true; }

            if (Brain.Busy)
            {
                // de pie, mirando al jugador y preparando el golpe: el aviso (la pose avanza con el telegraph y el golpe cae al dispararse la habilidad)
                _z.Stop();
                Face(toPlayer, dt, _current != null && _current.id == "charge" ? 0f : 360f);
                float p = Mathf.Clamp01(1f - Brain.TelegraphRemaining / Mathf.Max(0.1f, _telegraphTotal));
                if (_z.Procedural) { _z.BossPose = _pose; _z.BossPoseT = p * ZombieMotion.BossImpactT; }
                else _z.PoseAttackOverride = p * 0.5f;
                return true;
            }
            _z.PoseAttackOverride = -1f;
            return _released >= 0f && _pose == BossPose.Roar;   // tras invocar se queda rugiendo un momento
        }

        /// <summary>Recuperación tras el golpe (t de 0.5 a 1) y rugido de furia; cuando termina, vuelve a la locomoción normal.</summary>
        private void UpdatePose()
        {
            if (!_z.Procedural) return;
            if (_exec == Exec.Charging) { _z.BossPose = BossPose.ChargeRun; _z.BossPoseT = 0.5f; return; }
            if (Brain.Busy) return;
            if (_roarStart >= 0f)
            {
                float k = (Time.time - _roarStart) / RoarSeconds;
                if (k >= 1f) { _roarStart = -1f; _z.BossPose = BossPose.None; }
                else { _z.BossPose = BossPose.Roar; _z.BossPoseT = k; }
                return;
            }
            if (_released >= 0f)
            {
                float k = (Time.time - _released) / ReleaseSeconds;
                if (k >= 1f) { _released = -1f; _z.BossPose = BossPose.None; }
                else { _z.BossPose = _pose; _z.BossPoseT = ZombieMotion.BossImpactT + (1f - ZombieMotion.BossImpactT) * k; }
                return;
            }
            _z.BossPose = BossPose.None;
        }

        private static BossPose PoseFor(string abilityId)
        {
            switch (abilityId)
            {
                case "charge": return BossPose.ChargeWindup;
                case "slam": return BossPose.Slam;
                case "throw": return BossPose.Throw;
                case "shockwave": return BossPose.Shockwave;
                default: return BossPose.Roar;   // summon, summon_wave
            }
        }

        /// <summary>Rugido del jefe con su voz sintetizada (si aún no está generada, el sonido de respaldo).</summary>
        private void Roar(float volume)
        {
            if (!_z.Sounds.Play(ZombieSound.Roar, volume, true)) _audio.PlayOneShot(_roar, volume);
        }

        // ------------------------------------------------------------------ eventos del cerebro
        private void Handle(BossEvent e, Vector3 toPlayer)
        {
            switch (e.Type)
            {
                case BossEventType.PhaseChanged: EnterFury(e.Phase); break;
                case BossEventType.TelegraphStarted: StartTelegraph(e, toPlayer); break;
                case BossEventType.AbilityFired: Execute(e.AbilityId); break;
                case BossEventType.WeakPointExposed:
                    _z.Rig.SetWeakPoint(true);
                    HudMessages.Add("¡Punto débil expuesto!", new Color(1f, 0.7f, 0.2f), 2f);
                    break;
                case BossEventType.WeakPointHidden: _z.Rig.SetWeakPoint(false); break;
            }
        }

        private void EnterFury(int phase)
        {
            if (phase <= _lastPhaseShown) return;
            _lastPhaseShown = phase;
            Roar(1f);
            Shake(0.6f);
            // ojos y venas brillantes, pose agresiva y vapor (cuerpo procedural); el de respaldo se pone más rojo y algo más grande
            Color baseColor = new Color(Type.color[0], Type.color[1], Type.color[2]);
            _z.BodyColor = Color.Lerp(baseColor, new Color(0.9f, 0.1f, 0.08f), 0.28f * phase);
            _z.Rig.SetColor(_z.BodyColor);
            _z.Rig.SetFury(phase);
            if (!Brain.Busy && _exec == Exec.None) _roarStart = Time.time;   // ruge con los brazos abiertos (si no está avisando de otro ataque)
            TelegraphLabel = phase == 1 ? "¡FURIA!" : "¡FURIA TOTAL!";
            _labelUntil = Time.time + 2f;
            HudMessages.Add(Type.displayName + (phase == 1 ? " se enfurece" : " entra en furia total"), new Color(1f, 0.3f, 0.25f), 3f);
            if (BloodFx.Instance != null) BloodFx.Instance.Spray(transform.position + Vector3.up * 2f, Vector3.up, 30, new Color(0.9f, 0.15f, 0.1f), 1.5f);
        }

        private void StartTelegraph(BossEvent e, Vector3 toPlayer)
        {
            _current = System.Array.Find(Type.boss.abilities, a => a.id == e.AbilityId);
            _telegraphTotal = e.Seconds;
            _pose = PoseFor(e.AbilityId); _released = -1f; _roarStart = -1f;
            _audio.PlayOneShot(_warn, 0.9f);
            Vector3 pos = transform.position;
            Vector3 dir = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : transform.forward;
            switch (e.AbilityId)
            {
                case "charge":
                    _chargeDir = dir;   // la dirección se fija al empezar el aviso: el jugador puede esquivarla
                    TelegraphLabel = "¡CARGA!";
                    GroundMarker.ShowLine(pos, pos + dir * Mathf.Min(_current.maxRange, 26f), 2.6f * Type.scale * 0.6f, e.Seconds);
                    break;
                case "slam": TelegraphLabel = "¡GOLPE!"; GroundMarker.Show(pos, _current.a, e.Seconds); break;
                case "throw": TelegraphLabel = "¡LANZA UN OBJETO!"; break;
                case "summon": TelegraphLabel = "¡INVOCA REFUERZOS!"; GroundMarker.Show(pos, 3.5f, e.Seconds); break;
                case "shockwave": TelegraphLabel = "¡ONDA EXPANSIVA! ¡SALTA!"; GroundMarker.Show(pos, _current.a, e.Seconds); break;
                case "summon_wave": TelegraphLabel = "¡OLEADA!"; GroundMarker.Show(pos, 6f, e.Seconds); break;
            }
            _labelUntil = Time.time + e.Seconds + 0.5f;
        }

        // ------------------------------------------------------------------ ejecución
        private void Execute(string id)
        {
            _z.PoseAttackOverride = -1f;
            _released = id == "charge" ? -1f : Time.time;   // el golpe ya cayó (t = 0.5): ahora la recuperación
            var a = System.Array.Find(Type.boss.abilities, x => x.id == id);
            if (a == null) return;
            var player = _z.PlayerTransform; var ph = _z.PlayerHealth;
            switch (id)
            {
                case "charge": BeginCharge(a); break;
                case "slam": Slam(a, player, ph); break;
                case "throw": Throw(a, player, ph); break;
                case "summon": Summon(Type.boss.summonCount, 3.5f); break;
                case "summon_wave": Summon(Type.boss.summonCount + 2 * Brain.Phase, 7f); break;
                case "shockwave": Shockwave(a, player, ph); break;
            }
        }

        private void BeginCharge(BossAbilityData a)
        {
            _exec = Exec.Charging;
            _chargeLeft = a.maxRange; _chargeSpeed = a.a * Brain.SpeedMultiplier; _chargeDamageMult = a.b; _chargeHitPlayer = false;
            if (_z.Agent != null && _z.Agent.enabled) _z.Agent.enabled = false;
            _audio.PlayOneShot(_whoosh, 1f);
            TelegraphLabel = "";
        }

        private void Charge(float dt)
        {
            float step = _chargeSpeed * dt;
            Vector3 pos = transform.position;
            float radius = 0.7f * Type.scale;
            Vector3 chest = pos + Vector3.up * (1f * Type.scale);
            transform.rotation = Quaternion.LookRotation(_chargeDir);
            _z.Rig.Pose(Time.time * 14f, 1f, 0f, 0f, Time.time, 1f);

            var player = _z.PlayerTransform; var ph = _z.PlayerHealth;
            if (!_chargeHitPlayer && player != null && ph != null && !ph.IsDead)
            {
                Vector3 d = player.position - pos; d.y = 0f;
                if (d.magnitude < radius + 0.6f)
                {
                    _chargeHitPlayer = true;
                    ph.TakeDamage(new DamageInfo { Amount = Type.attackDamage * _chargeDamageMult * _z.DamageMultiplier, Point = player.position, Direction = _chargeDir, Source = gameObject });
                    Shake(0.5f);
                }
            }

            if (Physics.SphereCast(chest, radius, _chargeDir, out RaycastHit hit, step + 0.2f, ~((1 << 2) | HitboxLayer.Mask), QueryTriggerInteraction.Ignore))
            {
                var d = hit.collider.GetComponentInParent<Destructible>();
                if (d != null && d.TrySmash(0, hit.point, _chargeDir))
                {
                    _chargeSpeed *= 0.92f;   // atraviesa la cobertura ligera frenándose un poco
                }
                else
                {
                    EndChargeOnWall(hit.point);
                    return;
                }
            }
            Vector3 next = pos + _chargeDir * step;
            if (Physics.Raycast(next + Vector3.up * 2f, Vector3.down, out RaycastHit g, 5f, ~((1 << 2) | HitboxLayer.Mask), QueryTriggerInteraction.Ignore)) next.y = g.point.y;
            transform.position = next;
            _chargeLeft -= step;
            if (_chargeLeft <= 0f) { _exec = Exec.None; _z.BossPose = BossPose.None; ResumeAgent(); }
        }

        private void EndChargeOnWall(Vector3 point)
        {
            _exec = Exec.None;
            _z.BossPose = BossPose.None;
            ResumeAgent();
            Brain.WallStun();
            _health.Stun(Type.boss.wallStunSeconds, false);     // se queda aturdido: ventana de daño extra
            _audio.PlayOneShot(_boom, 1f);
            Shake(1f);
            if (BloodFx.Instance != null) BloodFx.Instance.Spray(point + Vector3.up * 0.5f, -_chargeDir + Vector3.up, 36, new Color(0.6f, 0.55f, 0.48f), 1.6f);
            TelegraphLabel = "¡ATURDIDO! ¡DISPARA!";
            _labelUntil = Time.time + Type.boss.wallStunSeconds;
        }

        private void Slam(BossAbilityData a, Transform player, Health ph)
        {
            _audio.PlayOneShot(_boom, 1f);
            Shake(0.5f);
            if (BloodFx.Instance != null) BloodFx.Instance.Spray(transform.position + Vector3.up * 0.2f, Vector3.up, 24, new Color(0.55f, 0.5f, 0.45f), 1.4f);
            if (player != null && ph != null && !ph.IsDead)
            {
                Vector3 d = player.position - transform.position; d.y = 0f;
                if (d.magnitude <= a.a) ph.TakeDamage(new DamageInfo { Amount = a.b * _z.DamageMultiplier, Point = player.position, Direction = d.normalized, Source = gameObject });
            }
            SmashNearby(transform.position, a.a, 0);
        }

        private void Throw(BossAbilityData a, Transform player, Health ph)
        {
            _audio.PlayOneShot(_whoosh, 1f);
            Vector3 hand = transform.position + Vector3.up * (2.3f * Type.scale / 2f) + transform.forward * 0.6f;
            if (_z.Rig is ZombieBody zb) hand = zb.Rig.Bone(BoneId.RightHand).position + transform.forward * 0.3f;   // sale de la mano del brazo grande, que está delante en t = 0.5
            Projectile.Launch(ProjectileMode.Arc, hand, player.position, Type.projectileSpeed > 0f ? Type.projectileSpeed : 14f, a.b * _z.DamageMultiplier, a.a, "debris", player, ph, gameObject, true, 4f);
        }

        private void Summon(int count, float ring)
        {
            var mgr = ZombieManager.Instance;
            if (mgr == null) return;
            Roar(0.8f);
            for (int i = 0; i < count; i++)
            {
                float ang = (i + Random.value * 0.4f) / Mathf.Max(1, count) * Mathf.PI * 2f;
                Vector3 p = transform.position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * ring;
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas)) p = hit.position;
                var s = mgr.Spawn(Type.boss.summonType, p, 1f, 1f, 1f, true);
                if (s != null && BloodFx.Instance != null) BloodFx.Instance.Spray(p + Vector3.up * 0.2f, Vector3.up, 10, new Color(0.35f, 0.3f, 0.3f), 1.0f);
            }
        }

        private void Shockwave(BossAbilityData a, Transform player, Health ph)
        {
            _audio.PlayOneShot(_boom, 1f);
            Shake(1f);
            if (BloodFx.Instance != null)
                for (int i = 0; i < 8; i++)
                {
                    float ang = i / 8f * Mathf.PI * 2f;
                    BloodFx.Instance.Spray(transform.position + Vector3.up * 0.2f, new Vector3(Mathf.Cos(ang), 0.15f, Mathf.Sin(ang)), 12, new Color(0.6f, 0.5f, 0.45f), 2.4f);
                }
            if (player != null && ph != null && !ph.IsDead)
            {
                Vector3 d = player.position - transform.position; d.y = 0f;
                if (d.magnitude <= a.a)
                {
                    var motor = player.GetComponent<PlayerMotor>();
                    if (motor != null && !motor.IsGrounded) HudMessages.Add("¡Esquivaste la onda!", new Color(0.6f, 1f, 0.7f), 1.5f);   // saltar la esquiva
                    else ph.TakeDamage(new DamageInfo { Amount = a.b * _z.DamageMultiplier, Point = player.position, Direction = d.normalized, Source = gameObject });
                }
            }
            SmashNearby(transform.position, a.a, 0);
            Brain.ExposeWeakPoint();                    // tras el esfuerzo queda expuesto un momento
            _z.Rig.SetWeakPoint(Brain.WeakPointExposed);
        }

        private static readonly Collider[] Buf = new Collider[64];
        private void SmashNearby(Vector3 center, float radius, int power)
        {
            int n = Physics.OverlapSphereNonAlloc(center, radius, Buf, ~HitboxLayer.Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) { var d = Buf[i].GetComponentInParent<Destructible>(); if (d != null) d.TrySmash(power, center, (Buf[i].transform.position - center).normalized); }
        }

        private void ResumeAgent()
        {
            if (_z.Agent == null) return;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas)) { transform.position = hit.position; _z.Agent.enabled = true; }
        }

        private void Face(Vector3 toPlayer, float dt, float speed)
        {
            if (speed <= 0f || toPlayer.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer), speed * dt);
        }

        private void Shake(float strength)
        {
            var p = _z.PlayerTransform;
            if (p == null) return;
            var imp = p.GetComponent<PlayerImpulse>();
            if (imp != null) imp.Shake(strength * Mathf.Clamp01(1f - Vector3.Distance(p.position, transform.position) / 40f));
        }

        /// <summary>Sonido procedural corto (sin archivos): barrido de frecuencia con ruido.</summary>
        private static AudioClip Synth(string name, float seconds, float f0, float f1, float volume, float noise)
        {
            const int rate = 22050;
            int n = Mathf.CeilToInt(seconds * rate);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float phase = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                phase += 2f * Mathf.PI * Mathf.Lerp(f0, f1, t) / rate;
                lp += (((float)rng.NextDouble() * 2f - 1f) - lp) * 0.2f;   // ruido suavizado: más grave y rugoso
                float env = Mathf.Min(1f, i / 400f) * Mathf.Exp(-2.2f * t);
                data[i] = (Mathf.Sign(Mathf.Sin(phase)) * 0.35f + Mathf.Sin(phase) * 0.65f) * (1f - noise) * env * volume + lp * noise * env * volume;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
