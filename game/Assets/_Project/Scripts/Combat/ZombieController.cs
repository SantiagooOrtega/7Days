using System;
using UnityEngine;
using UnityEngine.AI;
using SevenDays.Core;
using SevenDays.Procedural;
using SevenDays.World;
using SevenDays.World.Loot;

namespace SevenDays.Combat
{
    /// <summary>
    /// Zombie: NavMeshAgent + máquina de estados (patrulla / persigue / ataca / aturdido / muerto). Si el NavMesh no está disponible camina en línea recta
    /// pegado al suelo. Vive en un pool (ZombieManager): Activate() lo configura para un tipo y Deactivate() lo devuelve.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class ZombieController : MonoBehaviour
    {
        public ZombieTypeData Type { get; private set; }
        public Health Health { get; private set; }
        public ZombieRig Rig { get; private set; }
        public ZombieState State => _sm.State;
        public bool InUse { get; private set; }
        public bool DiedByHeadshot => Health.KilledByHeadshot;
        /// <summary>La cabeza voló: el director cuenta estas muertes como habilidad (headshots).</summary>
        public bool HeadDestroyed { get; private set; }
        /// <summary>Instante del primer impacto del jugador (para medir el tiempo que cuesta matarlo); -1 si aún no lo han tocado.</summary>
        public float FirstHitTime { get; private set; } = -1f;
        public float TimeToKill => FirstHitTime < 0f ? -1f : Mathf.Max(0f, _deadAt - FirstHitTime);
        public ZombieHealthBar Bar { get; private set; }
        public BossBehaviour Boss { get; private set; }
        public Color BodyColor { get; set; }
        /// <summary>Si es >= 0 sustituye a la animación de ataque (los jefes alzan los brazos mientras avisan de un ataque).</summary>
        public float PoseAttackOverride { get; set; } = -1f;
        public Transform PlayerTransform => _player;
        public Health PlayerHealth => _playerHealth;
        public NavMeshAgent Agent => _agent;
        public bool UsesAgent => _useAgent;
        public float SpeedMultiplier { get; set; } = 1f;
        public float DamageMultiplier { get; set; } = 1f;

        public event Action<ZombieController> Killed;

        private readonly ZombieStateMachine _sm = new ZombieStateMachine();
        private NavMeshAgent _agent;
        private CapsuleCollider _body;
        private Rigidbody _rb;
        private Transform _player;
        private Health _playerHealth;
        private Vector3 _home;
        private float _nextPath, _nextSight, _nextPatrol, _nextAttack, _attackStart = -10f, _deadAt, _flashUntil, _walkPhase, _stagger;
        private bool _canSee, _damageDone, _useAgent;
        private Material _flashMat;
        private Vector3 _knock;
        private Transform _puddle;
        private Camera _cam;
        private SpitterBrain _brain;
        private Vector3 _flankTarget;
        private float _nextFlank, _nextReposition;
        private int _agentTypeId;

        public void Build()
        {
            Health = GetComponent<Health>();
            _flashMat = Prim.GetMaterial(Color.white);
            _agent = gameObject.AddComponent<NavMeshAgent>();
            _agent.acceleration = 14f; _agent.angularSpeed = 360f; _agent.stoppingDistance = 0.1f; _agent.autoBraking = true;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance; // evita que se amontonen sin costar mucho
            _agent.avoidancePriority = UnityEngine.Random.Range(30, 70);
            _agent.enabled = false;
            _rb = gameObject.AddComponent<Rigidbody>();
            _rb.isKinematic = true; _rb.useGravity = false;
            _body = gameObject.AddComponent<CapsuleCollider>();
            _body.center = new Vector3(0f, 0.9f, 0f); _body.radius = 0.32f; _body.height = 1.8f;
            gameObject.layer = 2; // "Ignore Raycast": las balas ignoran el cuerpo de movimiento; solo cuentan los hitboxes
            HitboxLayer.ConfigureCollisions();
            Rig = ZombieRig.Build(transform, Health);
            Health.Damaged += OnDamaged;
            Health.Hit += OnHit;
            Health.Died += OnDied;
            Bar = ZombieHealthBar.Create(transform, Health);
            var pud = Prim.Create(PrimitiveType.Cylinder, "Puddle", Vector3.zero, new Vector3(0.1f, 0.005f, 0.1f), Prim.GetMaterial(new Color(0.22f, 0.02f, 0.02f)), transform, false);
            pud.layer = 2; _puddle = pud.transform; pud.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pud.SetActive(false);
            gameObject.SetActive(false);
        }

        public void SetPlayer(Transform player, Health health) { _player = player; _playerHealth = health; }

        public void Activate(ZombieTypeData type, Vector3 position, float healthMultiplier = 1f, float speedMultiplier = 1f, float damageMultiplier = 1f)
        {
            Type = type;
            SpeedMultiplier = speedMultiplier; DamageMultiplier = damageMultiplier;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            _home = position;
            Rig.Visual.localScale = Vector3.one * type.scale;
            Rig.Visual.localPosition = Vector3.zero; Rig.Visual.localRotation = Quaternion.identity;
            BodyColor = new Color(type.color[0], type.color[1], type.color[2]);
            Rig.SetColor(BodyColor);
            _body.radius = 0.32f * type.scale; _body.height = 1.8f * type.scale; _body.center = new Vector3(0f, 0.9f * type.scale, 0f);
            Health.Configure(type.maxHealth * healthMultiplier, type.resistance, type.stunResistance, type.zones.Clone());
            Rig.SetHitboxesEnabled(true);
            _sm.Reset(); _sm.AggroRange = type.aggroRange; _sm.AttackRange = type.attackRange * type.scale;
            _brain = type.projectile != "none" ? SpitterBrain.For(type) : null;
            // si el jugador abusa de la cobertura (reglas del director sobre las estadísticas guardadas), los lanzadores flanquean antes (hasta la mitad del tiempo)
            if (_brain != null && ZombieManager.Instance != null) _brain.FlankAfterSeconds *= 1f - 0.5f * ZombieManager.Instance.Tuning.coverPressure;
            _fuse = type.explosionRadius > 0f ? FuseBrain.For(type) : null;
            _exploded = false; StopHiss();
            FirstHitTime = -1f;
            HeadDestroyed = false; _knock = Vector3.zero; _puddle.gameObject.SetActive(false);
            Rig.HeadPivot.localScale = Vector3.one;
            Bar.Suppressed = type.isBoss; Bar.ResetBar(2.25f * type.scale);
            _stagger = 0f; _flashUntil = 0f; _damageDone = true; _attackStart = -10f;
            _nextAttack = Time.time + 0.5f; _nextSight = 0f; _nextPath = 0f; _nextPatrol = Time.time + UnityEngine.Random.Range(0.5f, 2.5f);
            _walkPhase = UnityEngine.Random.value * 6f;
            InUse = true;
            gameObject.SetActive(true);
            PoseAttackOverride = -1f;
            if (type.isBoss)
            {
                if (Boss == null) Boss = gameObject.AddComponent<BossBehaviour>();
                Boss.Begin(this, type);
            }
            else if (Boss != null) Boss.End();

            _agent.enabled = false;
            var mgr = ZombieManager.Instance;
            bool giant = !type.canEnterBuildings && mgr != null && mgr.GiantAgentTypeId >= 0;
            _agentTypeId = giant ? mgr.GiantAgentTypeId : 0;      // 0 = agente por defecto (cabe por puertas); los gigantes usan su propio NavMesh
            _agent.agentTypeID = _agentTypeId;
            _agent.radius = giant ? Mathf.Max(type.navRadius, 0.7f) : (type.navRadius > 0f ? type.navRadius : 0.35f * type.scale);
            _agent.height = giant ? 2.8f : 1.8f * type.scale;
            _siege.Reset(); _siegeGoal = Vector3.zero; _siegeStuck = 0f; _lastSiegePos = position;
            _useAgent = false;
            var spawnFilter = new NavMeshQueryFilter { agentTypeID = _agentTypeId, areaMask = NavMesh.AllAreas };
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, spawnFilter))
            {
                transform.position = hit.position;
                _agent.enabled = true;
                _useAgent = _agent.isOnNavMesh;
            }
            if (_useAgent) { _agent.speed = type.patrolSpeed * speedMultiplier; _agent.isStopped = false; }
        }

        public void Deactivate()
        {
            InUse = false;
            if (_agent != null) _agent.enabled = false;
            gameObject.SetActive(false);
        }

        private void OnDamaged(DamageInfo info)
        {
            if (FirstHitTime < 0f) FirstHitTime = Time.time;
            _flashUntil = Time.time + 0.07f;
            Rig.SetFlash(true, _flashMat);
            // Un disparo avisa al zombie: empieza a perseguir aunque no vea al jugador.
            if (!Health.IsDead && _sm.State == ZombieState.Patrol) _canSee = true;
        }

        /// <summary>Reacción al impacto: sangre, empujón y aturdimiento ligero (más con la escopeta y en la cabeza; los jefes casi no se inmutan).</summary>
        private void OnHit(DamageInfo info, HitReport report)
        {
            if (BloodFx.Instance != null)
                BloodFx.Instance.Spray(info.Point, info.Direction, report.Headshot ? 14 : 7);
            if (report.Killed) return;
            var reaction = HitReaction.Compute(report.FinalDamage, Health.Max, report.Zone, info.Knockback, Type.stunResistance);
            Vector3 dir = info.Direction; dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) _knock += dir.normalized * (reaction.KnockbackMeters * 7f * (Type.isBoss ? 0.15f : 1f));
            if (reaction.Stagger) Health.Stun(reaction.StunSeconds, false);   // HitReaction ya descontó la resistencia
        }

        private void OnDied()
        {
            _deadAt = Time.time;
            if (Boss != null) Boss.OnDeath();
            if (Health.KilledByHeadshot)
            {
                HeadDestroyed = true;
                Rig.HeadPivot.localScale = Vector3.zero;   // la cabeza vuela
            }
            if (BloodFx.Instance != null)
                BloodFx.Instance.Spray(transform.position + Vector3.up * (Health.KilledByHeadshot ? 1.7f : 1.2f) * Type.scale, Vector3.up + (_player != null ? (transform.position - _player.position).normalized : Vector3.zero), Health.KilledByHeadshot ? 40 : 24);
            _puddle.gameObject.SetActive(true);
            if (_fuse != null) { _fuse.ForceDetonate(); Explode(); }   // también explota si lo matan, aunque sea de un headshot en plena mecha
            Rig.SetHitboxesEnabled(false);
            _body.enabled = false;
            if (_useAgent && _agent.enabled) _agent.isStopped = true;
            DropSystem.OnZombieKilled(Type.dropKind, transform.position + Vector3.up * 0.3f);
            Killed?.Invoke(this);
        }

        private void Update()
        {
            if (!InUse) return;
            float dt = Time.deltaTime;
            if (_flashUntil > 0f && Time.time >= _flashUntil)
            {
                _flashUntil = 0f;
                Rig.SetColor(BodyColor);
            }

            if (Health.IsDead) { UpdateDead(); return; }
            if (_player == null) return;

            if (_knock.sqrMagnitude > 0.0004f)
            {
                Vector3 step = _knock * dt;
                if (_useAgent && _agent.isOnNavMesh) _agent.Move(step); else transform.position += step;
                _knock = Vector3.Lerp(_knock, Vector3.zero, 1f - Mathf.Exp(-9f * dt));
            }

            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            bool playerDead = _playerHealth != null && _playerHealth.IsDead;

            if (Time.time >= _nextSight)
            {
                _nextSight = Time.time + 0.25f + UnityEngine.Random.value * 0.1f; // escalonado: no todos los zombis lanzan rayos el mismo frame
                // agachado tras una cobertura el jugador solo se ve de cerca; asomado, hasta el rango de detección
                float seen = SevenDays.Procedural.CoverProtection.VisibleRange(ZombieManager.Instance != null ? ZombieManager.Instance.PlayerExposure : 1f, Type.aggroRange);
                _canSee = dist < 3f || (dist <= seen && HasLineOfSight());
            }
            _sm.Tick(dt, dist, _canSee, Health.IsStunned, false, playerDead);
            bool bossHandled = false; float bossMove = 0f;
            if (Boss != null && Boss.Active) bossHandled = Boss.Tick(dt, toPlayer, dist, out bossMove);
            if (_sm.State == ZombieState.Chase) _canSee = _canSee || dist < _sm.AggroRange;

            float moveAmount = 0f;
            if (_fuse != null && !Health.IsStunned && UpdateFuse(dt, toPlayer, dist)) moveAmount = 0f;
            else if (bossHandled) moveAmount = bossMove;
            else if (!Health.IsStunned && GiantBlockedByBuilding(out _house)) moveAmount = Siege(dt, toPlayer, dist);
            else if (_brain != null && _sm.State == ZombieState.Chase) moveAmount = Ranged(dt, toPlayer, dist); // el lanzador mantiene distancia y lanza
            else switch (_sm.State)
            {
                case ZombieState.Patrol: moveAmount = Patrol(dt); break;
                case ZombieState.Chase: moveAmount = Chase(dt, toPlayer, dist); break;
                case ZombieState.Attack: Attack(dt, toPlayer, dist); break;
                case ZombieState.Stunned: Stop(); break;
            }
            if (!Type.canEnterBuildings) EnforceBuildingGuard();

            _walkPhase += dt * (3f + moveAmount * 4f);
            _stagger = Mathf.MoveTowards(_stagger, Health.IsStunned ? 1f : 0f, dt * 6f);
            float attackT = PoseAttackOverride >= 0f ? PoseAttackOverride : (_attackStart > 0f ? Mathf.Clamp01((Time.time - _attackStart) / 0.7f) : 0f);
            float amount = Mathf.Clamp01(moveAmount);
            if (_cam == null) _cam = Camera.main;
            Bar.Tick(dist, _cam);
            // lejos del jugador la animación se salta: nadie la ve y son 24 zombis
            if (dist < 70f) Rig.Pose(_walkPhase, amount, attackT, _stagger, Time.time + GetInstanceID() * 0.01f, _sm.State == ZombieState.Chase ? 1f : 0f);
        }

        // ------------------------------------------------------------------ comportamientos
        private float Patrol(float dt)
        {
            if (Time.time >= _nextPatrol)
            {
                _nextPatrol = Time.time + UnityEngine.Random.Range(3f, 7f);
                Vector2 r = UnityEngine.Random.insideUnitCircle * 8f;
                MoveTo(_home + new Vector3(r.x, 0f, r.y), Type.patrolSpeed * SpeedMultiplier);
            }
            return MoveAmount(Type.patrolSpeed * SpeedMultiplier);
        }

        private float Chase(float dt, Vector3 toPlayer, float dist)
        {
            float speed = Type.speed * SpeedMultiplier;
            if (Time.time >= _nextPath)
            {
                _nextPath = Time.time + 0.2f;
                MoveTo(_player.position, speed);
            }
            if (!_useAgent) DirectMove(toPlayer, speed, dt);
            return MoveAmount(speed);
        }

        internal void Attack(float dt, Vector3 toPlayer, float dist)
        {
            Stop();
            if (toPlayer.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer), 540f * dt);
            if (Time.time >= _nextAttack)
            {
                _nextAttack = Time.time + Type.attackCooldown;
                _attackStart = Time.time;
                _damageDone = false;
            }
            // el golpe cae a mitad de la animación; si el jugador se alejó, falla
            if (!_damageDone && _attackStart > 0f && Time.time - _attackStart >= 0.35f)
            {
                _damageDone = true;
                if (_playerHealth != null && !_playerHealth.IsDead && dist <= _sm.AttackRange * 1.3f)
                    _playerHealth.TakeDamage(new DamageInfo { Amount = Type.attackDamage * DamageMultiplier, Point = _player.position + Vector3.up, Direction = toPlayer.normalized, Source = gameObject, ScaledByCover = true });
            }
            if (Time.time - _attackStart > 0.8f) _attackStart = -10f;
        }

        // ------------------------------------------------------------------ lanzador
        private float Ranged(float dt, Vector3 toPlayer, float dist)
        {
            var mgr = ZombieManager.Instance;
            var action = _brain.Tick(dt, dist, _canSee, mgr != null ? mgr.PlayerCoverSeconds : 0f);
            float speed = Type.speed * SpeedMultiplier;
            Vector3 dirToPlayer = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : transform.forward;
            switch (action)
            {
                case SpitterAction.Reposition:
                    if (Time.time >= _nextReposition)
                    {
                        _nextReposition = Time.time + 0.35f;
                        MoveTo(_player.position - dirToPlayer * _brain.PreferredRange, speed);
                    }
                    if (!_useAgent) DirectMove(dist > _brain.PreferredRange ? toPlayer : -toPlayer, speed, dt);
                    return MoveAmount(speed);
                case SpitterAction.Flank:
                    if (Time.time >= _nextFlank) { _nextFlank = Time.time + 1.2f; _flankTarget = FindFlankPoint(dirToPlayer); }
                    MoveTo(_flankTarget, speed * 1.15f);
                    return MoveAmount(speed);
                case SpitterAction.Aim:
                    Stop();
                    FacePlayer(toPlayer, dt);
                    _attackStart = Time.time - Mathf.Clamp01(1f - _brain.AimRemaining / 0.9f) * 0.35f; // alza los brazos mientras apunta
                    return 0f;
                case SpitterAction.Throw:
                    Stop();
                    FacePlayer(toPlayer, dt);
                    ThrowProjectile();
                    return 0f;
                default:
                    Stop();
                    FacePlayer(toPlayer, dt);
                    return 0f;
            }
        }

        private void FacePlayer(Vector3 toPlayer, float dt)
        {
            if (toPlayer.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer), 540f * dt);
        }

        private void ThrowProjectile()
        {
            _attackStart = Time.time;
            Vector3 hand = transform.position + Vector3.up * (1.4f * Type.scale) + transform.forward * 0.4f;
            bool arc = _brain.Mode == ProjectileMode.Arc;
            Vector3 target = arc ? _player.position : _player.position + Vector3.up * 1.1f;
            Projectile.Launch(_brain.Mode, hand, target, Type.projectileSpeed, Type.projectileDamage * DamageMultiplier, Type.areaRadius, Type.projectileKind, _player, _playerHealth, gameObject);
        }

        /// <summary>Busca un punto a un lado del jugador desde el que haya línea de tiro libre y sea alcanzable por el NavMesh.</summary>
        private Vector3 FindFlankPoint(Vector3 dirToPlayer)
        {
            Vector3 perp = new Vector3(-dirToPlayer.z, 0f, dirToPlayer.x);
            Vector3 best = _player.position - dirToPlayer * _brain.PreferredRange;
            float bestScore = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                float reach = _brain.PreferredRange * (0.7f + 0.25f * (i / 2));
                Vector3 p = _player.position + perp * side * reach - dirToPlayer * _brain.PreferredRange * 0.4f;
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas)) p = hit.position; else continue;
                Vector3 from = p + Vector3.up * 1.5f, to = _player.position + Vector3.up * 1.2f;
                bool clear = !Physics.Raycast(from, (to - from).normalized, (to - from).magnitude, ~((1 << 2) | HitboxLayer.Mask), QueryTriggerInteraction.Ignore);
                float score = (p - transform.position).sqrMagnitude + (clear ? 0f : 10000f);
                if (score < bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        // ------------------------------------------------------------------ explosivo (tipo creeper)
        private FuseBrain _fuse;
        private bool _exploded;
        private AudioSource _hiss;
        private Material _fuseRed, _fuseWhite;

        /// <summary>Avanza la mecha. Devuelve true mientras arde (el zombi se queda quieto, se infla y parpadea).</summary>
        private bool UpdateFuse(float dt, Vector3 toPlayer, float dist)
        {
            var ev = _fuse.Tick(dt, dist);
            switch (ev)
            {
                case FuseEvent.Started: StartHiss(); break;
                case FuseEvent.Cancelled: StopHiss(); RestoreFuseVisuals(); break;
                case FuseEvent.Detonate: Detonate(); return false;
            }
            if (_fuse.State != FuseState.Burning) return false;
            Stop();
            FacePlayer(toPlayer, dt);
            Rig.Visual.localScale = Vector3.one * Type.scale * _fuse.Inflate(Time.time);
            _fuseRed ??= Prim.GetEmissive(new Color(1f, 0.1f, 0.05f), 2.5f);
            _fuseWhite ??= Prim.GetEmissive(Color.white, 3.5f);
            Rig.SetFlash(true, _fuse.FlashWhite(Time.time) ? _fuseWhite : _fuseRed);
            _flashUntil = 0f;   // el parpadeo de la mecha manda sobre el destello de impacto
            return true;
        }

        private void StartHiss()
        {
            if (_hiss == null)
            {
                _hiss = gameObject.AddComponent<AudioSource>();
                _hiss.spatialBlend = 1f; _hiss.maxDistance = 40f; _hiss.rolloffMode = AudioRolloffMode.Linear; _hiss.loop = true; _hiss.playOnAwake = false;
            }
            if (BlastFx.HissClip == null) return;
            _hiss.clip = BlastFx.HissClip; _hiss.pitch = 0.9f; _hiss.Play();
        }

        private void StopHiss() { if (_hiss != null && _hiss.isPlaying) _hiss.Stop(); }

        private void RestoreFuseVisuals()
        {
            Rig.Visual.localScale = Vector3.one * Type.scale;
            Rig.SetColor(BodyColor);
        }

        /// <summary>Termina la mecha: explota y muere. Si ya explotó (lo mataron antes) no hace nada.</summary>
        private void Detonate()
        {
            Explode();
            if (!Health.IsDead) Health.TakeDamage(new DamageInfo { Amount = Health.Max * 10f, Point = transform.position, Source = gameObject });
        }

        private static readonly Collider[] BlastBuf = new Collider[48];

        private void Explode()
        {
            if (_exploded) return;
            _exploded = true;
            StopHiss();
            Vector3 center = transform.position + Vector3.up * (0.9f * Type.scale);
            float radius = Type.explosionRadius;
            int solid = ~((1 << 2) | HitboxLayer.Mask);
            BlastFx.Instance?.Explode(center, radius);

            // jugador: daño con caída por distancia (una pared sólida entre los dos deja pasar solo una fracción), empuje y sacudida de cámara
            if (_player != null && _playerHealth != null && !_playerHealth.IsDead)
            {
                Vector3 chest = _player.position + Vector3.up * 1.1f;
                float d = Vector3.Distance(center, chest);
                bool blocked = d > 0.5f && Physics.Linecast(center, chest, solid, QueryTriggerInteraction.Ignore);
                float dmg = Blast.Damage(d, radius, Type.explosionDamage * DamageMultiplier, blocked, Type.explosionBlockedFactor);
                Vector3 away = _player.position - center; away.y = 0f;
                away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back;
                if (dmg > 0f) _playerHealth.TakeDamage(new DamageInfo { Amount = dmg, Point = center, Direction = away, Source = gameObject });
                float push = Blast.Knockback(d, radius, Type.explosionKnockback, blocked);
                if (push > 0f) { var motor = _player.GetComponent<SevenDays.Player.PlayerMotor>(); if (motor != null) motor.Push(away * push + Vector3.up * push * 0.25f); }
                var imp = _player.GetComponent<SevenDays.Player.PlayerImpulse>();
                if (imp != null) imp.Shake(1.1f * Mathf.Clamp01(1f - d / (radius * 4f)));
            }

            // otros zombis (en cadena) y coberturas ligeras
            var mgr = ZombieManager.Instance;
            if (mgr != null)
                foreach (var other in mgr.All)
                {
                    if (other == this || !other.InUse || other.Health.IsDead) continue;
                    Vector3 oc = other.transform.position + Vector3.up;
                    float d = Vector3.Distance(center, oc);
                    if (d >= radius) continue;
                    bool blocked = Physics.Linecast(center, oc, solid, QueryTriggerInteraction.Ignore);
                    float dmg = Blast.Damage(d, radius, Type.explosionDamage, blocked, Type.explosionBlockedFactor);
                    Vector3 dir = other.transform.position - center; dir.y = 0f;
                    other.Push(dir.normalized * Blast.Knockback(d, radius, Type.explosionKnockback, blocked));
                    if (dmg > 0f) other.Health.TakeDamage(new DamageInfo { Amount = dmg, Point = center, Direction = dir.normalized, Source = gameObject });
                }
            if (Type.explosionBreaksCover)
            {
                int n = Physics.OverlapSphereNonAlloc(center, radius, BlastBuf, ~HitboxLayer.Mask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++) { var dst = BlastBuf[i].GetComponentInParent<World.Destructible>(); if (dst != null) dst.TrySmash(0, center, (BlastBuf[i].transform.position - center).normalized); }
            }

            // fuego breve donde estalló
            var fire = World.FireManager.Instance != null ? World.FireManager.Instance.Spawn(FireKind.Campfire, transform.position) : null;
            if (fire != null && mgr != null) mgr.StartCoroutine(Extinguish(fire, 3f));
        }

        private static System.Collections.IEnumerator Extinguish(SevenDays.Look.FireEffect fx, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (World.FireManager.Instance != null) World.FireManager.Instance.Despawn(fx);
        }

        /// <summary>Empujón externo (explosiones): se suma al retroceso del zombi.</summary>
        public void Push(Vector3 velocity) { velocity.y = 0f; _knock += velocity; }

        // ------------------------------------------------------------------ gigante: asedio a edificios
        private readonly SiegeBrain _siege = new SiegeBrain();
        private float _nextSiegePath, _siegeStuck;
        private Vector3 _siegeGoal, _lastSiegePos;
        private Footprint _house;

        private bool GiantBlockedByBuilding(out Footprint f)
        {
            f = default;
            return !Type.canEnterBuildings && _player != null && World.HouseController.Inside(_player.position, 0.3f, out f);
        }

        /// <summary>El jugador está dentro de una casa a la que este zombi no cabe: se acerca a lo más cercano que alcanza, golpea el muro y, si pasa un rato sin alcanzarlo, rodea la casa.</summary>
        private float Siege(float dt, Vector3 toPlayer, float dist)
        {
            float speed = Type.speed * SpeedMultiplier;
            bool arrived = !_useAgent
                ? (transform.position - _siegeGoal).sqrMagnitude < 2.25f
                : (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.6f);
            // si lleva un rato sin avanzar sin llegar, también cuenta como "llegó" (esquina, otro zombi delante...) para no quedarse empujando
            float moved = (transform.position - _lastSiegePos).magnitude; _lastSiegePos = transform.position;
            _siegeStuck = moved < 0.01f * Mathf.Max(1f, speed) * dt * 60f ? _siegeStuck + dt : 0f;
            if (_siegeStuck > 0.8f) arrived = true;

            _siege.Reach = _sm.AttackRange + 2.2f;
            var action = _siege.Tick(dt, dist, arrived);
            switch (action)
            {
                case SiegeAction.Pound:
                    Stop();
                    FacePlayer(toPlayer, dt);
                    PoundWall(toPlayer, dist);
                    return 0f;
                case SiegeAction.Flank:
                    if (Time.time >= _nextSiegePath || _siegeGoal == Vector3.zero)
                    {
                        _nextSiegePath = Time.time + 0.6f;
                        _house.PerimeterPoint(_siege.FlankIndex, 2.2f, out float px, out float pz);
                        _siegeGoal = SampleOnMyNavMesh(new Vector3(px, transform.position.y, pz), 8f);
                        MoveTo(_siegeGoal, speed);
                    }
                    if (!_useAgent) GuardedStep((_siegeGoal - transform.position), speed, dt);
                    return MoveAmount(speed);
                case SiegeAction.Wait:
                    Stop();
                    FacePlayer(toPlayer, dt);
                    return 0f;
                default: // Approach
                    if (Time.time >= _nextSiegePath)
                    {
                        _nextSiegePath = Time.time + 0.5f;
                        Vector3 goal = SampleOnMyNavMesh(_player.position, 14f);
                        if ((goal - _siegeGoal).sqrMagnitude > 1f) { _siegeGoal = goal; MoveTo(goal, speed); }
                    }
                    if (!_useAgent) GuardedStep((_siegeGoal - transform.position), speed, dt);
                    return MoveAmount(speed);
            }
        }

        private void PoundWall(Vector3 toPlayer, float dist)
        {
            if (Time.time >= _nextAttack)
            {
                _nextAttack = Time.time + Type.attackCooldown * 1.2f;
                _attackStart = Time.time;
                _damageDone = false;
            }
            if (!_damageDone && _attackStart > 0f && Time.time - _attackStart >= 0.35f)
            {
                _damageDone = true;
                Vector3 wallPoint = transform.position + transform.forward * 1.6f + Vector3.up;
                if (BloodFx.Instance != null) BloodFx.Instance.Spray(wallPoint, -transform.forward + Vector3.up * 0.4f, 12, new Color(0.6f, 0.55f, 0.5f), 1.3f);
                var imp = _player.GetComponent<SevenDays.Player.PlayerImpulse>();
                if (imp != null) imp.Shake(0.35f * Mathf.Clamp01(1f - dist / 20f));
                // el golpe alcanza al jugador si está pegado al muro o a la puerta
                if (_playerHealth != null && !_playerHealth.IsDead && dist <= _siege.Reach + 0.3f)
                    _playerHealth.TakeDamage(new DamageInfo { Amount = Type.attackDamage * 0.7f * DamageMultiplier, Point = _player.position + Vector3.up, Direction = toPlayer.normalized, Source = gameObject });
            }
            if (Time.time - _attackStart > 0.8f) _attackStart = -10f;
        }

        /// <summary>Punto del NavMesh de ESTE zombi (según su tipo de agente) más cercano a una posición.</summary>
        private Vector3 SampleOnMyNavMesh(Vector3 p, float maxDist)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = _agentTypeId, areaMask = NavMesh.AllAreas };
            return NavMesh.SamplePosition(p, out NavMeshHit hit, maxDist, filter) ? hit.position : transform.position;
        }

        /// <summary>Barrera de seguridad: si por cualquier fallo (NavMesh ausente, empujón, enlace) el gigante acaba dentro de una casa, se le saca por el borde más cercano.</summary>
        private void EnforceBuildingGuard()
        {
            foreach (var f in World.HouseController.Footprints)
            {
                Vector3 p = transform.position;
                if (!f.Contains(p.x, p.z, 0.2f)) continue;
                f.PushOut(p.x, p.z, 0.6f, out float ox, out float oz);
                var np = new Vector3(ox, p.y, oz);
                if (_useAgent && _agent.enabled) _agent.Warp(np); else transform.position = np;
            }
        }

        /// <summary>Paso sin NavMesh que respeta los colliders: barre una esfera, desliza por las paredes y nunca entra en la planta de una casa.</summary>
        private void GuardedStep(Vector3 delta, float speed, float dt)
        {
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.01f) return;
            Vector3 dir = delta.normalized;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 360f * dt);
            float step = speed * dt;
            float radius = 0.4f * Type.scale;
            int mask = ~((1 << 2) | HitboxLayer.Mask);
            Vector3 origin = transform.position + Vector3.up * (0.6f * Type.scale + 0.3f);
            if (Physics.SphereCast(origin, radius, dir, out RaycastHit hit, step + 0.05f, mask, QueryTriggerInteraction.Ignore))
            {
                Vector3 slide = Vector3.ProjectOnPlane(dir, hit.normal); slide.y = 0f;
                if (slide.sqrMagnitude < 0.01f) return;
                dir = slide.normalized;
                if (Physics.SphereCast(origin, radius, dir, out _, step + 0.05f, mask, QueryTriggerInteraction.Ignore)) return;   // también bloqueado: se queda quieto, sin atravesar
            }
            Vector3 next = transform.position + dir * step;
            if (Physics.Raycast(next + Vector3.up * 1.5f, Vector3.down, out RaycastHit g, 4f, mask, QueryTriggerInteraction.Ignore)) next.y = g.point.y;
            transform.position = next;
        }

        internal void MoveTo(Vector3 target, float speed)
        {
            if (_useAgent && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.speed = speed;
                _agent.SetDestination(target);
            }
        }

        internal void Stop() { if (_useAgent && _agent.isOnNavMesh) { _agent.isStopped = true; _agent.velocity = Vector3.zero; } }

        private float MoveAmount(float speed)
        {
            if (_useAgent) return _agent.velocity.magnitude / Mathf.Max(0.1f, Type.speed);
            return 1f;
        }

        /// <summary>Respaldo sin NavMesh: camina hacia el jugador y se pega al suelo con un rayo.</summary>
        private void DirectMove(Vector3 toPlayer, float speed, float dt) => GuardedStep(toPlayer, speed, dt);

        private bool HasLineOfSight()
        {
            Vector3 eye = transform.position + Vector3.up * (1.6f * Type.scale);
            Vector3 target = _player.position + Vector3.up * 1.2f;
            Vector3 d = target - eye;
            // las capas 2 (jugador/zombis) y Hitbox no tapan la vista
            return !Physics.Raycast(eye, d.normalized, d.magnitude, ~(1 << 2 | HitboxLayer.Mask), QueryTriggerInteraction.Ignore);
        }

        private void UpdateDead()
        {
            float t = Time.time - _deadAt;
            // cae hacia atrás en 0.45 s, se queda en el suelo y se hunde antes de volver al pool
            float fall = Mathf.SmoothStep(0f, 1f, t / 0.45f);
            Rig.Visual.localRotation = Quaternion.Euler(-88f * fall, 0f, 0f);
            Rig.Visual.localPosition = new Vector3(0f, 0.12f * fall - Mathf.Max(0f, t - 4f) * 0.25f, 0f);
            float grow = Mathf.Clamp01(t / 1.5f);
            _puddle.localPosition = new Vector3(0f, 0.02f, 0f);
            _puddle.localScale = new Vector3(Mathf.Lerp(0.1f, 1.3f, grow) * Type.scale, 0.005f, Mathf.Lerp(0.1f, 1.3f, grow) * Type.scale);
            if (t > 8f) Deactivate();
        }
    }
}
