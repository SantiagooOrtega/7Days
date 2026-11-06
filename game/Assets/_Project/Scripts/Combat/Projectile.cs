using UnityEngine;
using UnityEngine.Pool;
using SevenDays.Core;
using SevenDays.Player;
using SevenDays.Procedural;
using SevenDays.World;

namespace SevenDays.Combat
{
    /// <summary>Marca roja en el suelo (aviso de un impacto en arco, de la carga o del golpe de un jefe). Pool de unas pocas, pulsa y desaparece sola.</summary>
    public class GroundMarker : MonoBehaviour
    {
        private static ObjectPool<GroundMarker> _pool;
        private float _end, _start, _radius;
        private bool _line;
        private float _lineLength, _lineWidth;
        private static ObjectPool<GroundMarker> _linePool;

        public static GroundMarker Show(Vector3 center, float radius, float seconds)
        {
            _pool ??= new ObjectPool<GroundMarker>(Create, m => m.gameObject.SetActive(true), m => m.gameObject.SetActive(false), m => Destroy(m.gameObject), false, 8, 24);
            var m = _pool.Get();
            if (Physics.Raycast(center + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, ~((1 << 2) | HitboxLayer.Mask), QueryTriggerInteraction.Ignore)) center.y = hit.point.y;
            m.transform.position = center + Vector3.up * 0.05f;
            m._radius = radius; m._start = Time.time; m._end = Time.time + seconds;
            return m;
        }

        /// <summary>Franja roja en el suelo (trayectoria de una carga).</summary>
        public static GroundMarker ShowLine(Vector3 from, Vector3 to, float width, float seconds)
        {
            _linePool ??= new ObjectPool<GroundMarker>(CreateLine, m => m.gameObject.SetActive(true), m => m.gameObject.SetActive(false), m => Destroy(m.gameObject), false, 4, 8);
            var m = _linePool.Get();
            Vector3 d = to - from; d.y = 0f;
            m._line = true; m._lineLength = d.magnitude; m._lineWidth = width; m._start = Time.time; m._end = Time.time + seconds;
            Vector3 mid = (from + to) * 0.5f;
            if (Physics.Raycast(mid + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, ~((1 << 2) | HitboxLayer.Mask), QueryTriggerInteraction.Ignore)) mid.y = hit.point.y;
            m.transform.SetPositionAndRotation(mid + Vector3.up * 0.05f, d.sqrMagnitude > 0.001f ? Quaternion.LookRotation(d.normalized) : Quaternion.identity);
            m.transform.localScale = new Vector3(width, 0.01f, Mathf.Max(0.1f, m._lineLength));
            return m;
        }

        private static GroundMarker CreateLine()
        {
            var go = Prim.Create(PrimitiveType.Cube, "GroundLine", Vector3.zero, new Vector3(1f, 0.01f, 1f), Prim.GetEmissive(new Color(0.9f, 0.08f, 0.05f), 2.2f), null, false);
            go.layer = 2;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.AddComponent<GroundMarker>();
        }

        private static GroundMarker Create()
        {
            var go = Prim.Create(PrimitiveType.Cylinder, "GroundMarker", Vector3.zero, new Vector3(1f, 0.01f, 1f), Prim.GetEmissive(new Color(0.9f, 0.08f, 0.05f), 2.2f), null, false);
            go.layer = 2;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.AddComponent<GroundMarker>();
        }

        private void Update()
        {
            float now = Time.time;
            if (now >= _end) { if (_line) _linePool.Release(this); else _pool.Release(this); _line = false; return; }
            if (_line) { float w = _lineWidth * (1f + Mathf.Sin(now * 14f) * 0.05f); transform.localScale = new Vector3(w, 0.01f, _lineLength); return; }
            float t = Mathf.InverseLerp(_start, _end, now);
            float pulse = 1f + Mathf.Sin(now * 14f) * 0.04f;
            // el círculo se va llenando: el tamaño real es el del impacto; el aviso crece hasta él
            float d = _radius * 2f * Mathf.Lerp(0.55f, 1f, t) * pulse;
            transform.localScale = new Vector3(d, 0.01f, d);
        }
    }

    /// <summary>
    /// Proyectil del zombi lanzador y de los jefes (con pooling). Directo: vuela recto y lo frena la primera cobertura u obstáculo (no daña al jugador);
    /// en arco: describe una parábola y cae sobre la posición donde estaba el jugador, ignorando la cobertura frontal, con daño de área.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        private static ObjectPool<Projectile> _pool;
        private static readonly Collider[] Buffer = new Collider[8];

        private ProjectileMode _mode;
        private Vector3 _vel, _start, _target;
        private float _damage, _radius, _t, _flight, _life;
        private GameObject _source;
        private Transform _player;
        private Health _playerHealth;
        private bool _destroysCover;

        public static void Launch(ProjectileMode mode, Vector3 from, Vector3 target, float speed, float damage, float areaRadius, string kind, Transform player, Health playerHealth,
            GameObject source, bool destroysCover = false, float scale = 1f)
        {
            _pool ??= new ObjectPool<Projectile>(Create, p => p.gameObject.SetActive(true), p => p.gameObject.SetActive(false), p => Destroy(p.gameObject), false, 12, 32);
            var p = _pool.Get();
            p.Setup(mode, from, target, speed, damage, areaRadius, kind, player, playerHealth, source, destroysCover, scale);
        }

        private static Projectile Create()
        {
            var go = Prim.Create(PrimitiveType.Sphere, "Projectile", Vector3.zero, Vector3.one * 0.3f, Prim.GetEmissive(new Color(0.55f, 0.9f, 0.2f), 2f), null, false);
            go.layer = 2;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.AddComponent<Projectile>();
        }

        private void Setup(ProjectileMode mode, Vector3 from, Vector3 target, float speed, float damage, float areaRadius, string kind, Transform player, Health playerHealth, GameObject source, bool destroysCover, float scale)
        {
            _mode = mode; _start = from; _target = target; _damage = damage; _radius = areaRadius; _player = player; _playerHealth = playerHealth; _source = source;
            _destroysCover = destroysCover; _t = 0f; _life = 0f;
            transform.position = from;
            transform.localScale = Vector3.one * (kind == "grenade" ? 0.34f : kind == "rod" ? 0.22f : 0.3f) * scale;
            GetComponent<Renderer>().sharedMaterial = Prim.GetEmissive(kind == "acid" ? new Color(0.5f, 0.95f, 0.15f) : kind == "grenade" ? new Color(0.95f, 0.45f, 0.1f) : new Color(0.6f, 0.55f, 0.45f), 1.5f);
            if (mode == ProjectileMode.Arc)
            {
                float dist = Vector3.Distance(from, target);
                _flight = Ballistics.FlightTime(dist, speed);
                Ballistics.LaunchVelocity(from.x, from.y, from.z, target.x, target.y, target.z, _flight, Ballistics.Gravity, out float vx, out float vy, out float vz);
                _vel = new Vector3(vx, vy, vz);
                GroundMarker.Show(target, areaRadius, _flight);   // aviso en el suelo durante todo el vuelo (1–1.5 s)
            }
            else _vel = (target - from).normalized * speed;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _life += dt;
            if (_mode == ProjectileMode.Arc)
            {
                _t += dt;
                if (_t >= _flight) { Land(); return; }
                transform.position = _start + new Vector3(_vel.x * _t, _vel.y * _t - 0.5f * Ballistics.Gravity * _t * _t, _vel.z * _t);
                return;
            }
            Vector3 next = transform.position + _vel * dt;
            transform.position = next;
            if (_life > 5f) { Release(); return; }
            int n = Physics.OverlapSphereNonAlloc(next, 0.22f, Buffer, ~HitboxLayer.Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = Buffer[i];
                if (c.GetComponentInParent<ZombieController>() != null) continue;   // no se golpea entre ellos
                var health = c.GetComponentInParent<Health>();
                if (health != null && _playerHealth != null && health == _playerHealth) { HitPlayer(); return; }
                if (_destroysCover) { var d = c.GetComponentInParent<Destructible>(); if (d != null) d.Smash(transform.position, _vel.normalized); }
                // cualquier otra cosa sólida (incluida una CoverObject) lo frena: el jugador no recibe daño
                bool cover = c.GetComponentInParent<CoverObject>() != null;
                if (cover) HudMessages.Add("La cobertura te protegió", new Color(0.6f, 0.9f, 1f), 1.2f);
                if (BloodFx.Instance != null) BloodFx.Instance.Spray(next, -_vel.normalized, 8, new Color(0.5f, 0.8f, 0.2f), 0.6f);
                Release();
                return;
            }
        }

        private void HitPlayer()
        {
            // jugador pegado a una cobertura y proyectil que viene de frente: el daño lo absorbe la cobertura
            var cover = _player != null ? _player.GetComponent<PlayerCover>() : null;
            bool absorbed = cover != null && cover.Active && Vector3.Dot(_vel.normalized, cover.FacingDirection) > 0.1f;
            float dmg = CoverRules.DamageAfterCover(ProjectileMode.Direct, _damage, absorbed);
            if (dmg > 0f) _playerHealth.TakeDamage(new DamageInfo { Amount = dmg, Point = transform.position, Direction = _vel.normalized, Source = _source });
            else HudMessages.Add("La cobertura absorbió el golpe", new Color(0.6f, 0.9f, 1f), 1.2f);
            Release();
        }

        private void Land()
        {
            if (_player != null && _playerHealth != null && !_playerHealth.IsDead)
            {
                float d = Vector3.Distance(new Vector3(_player.position.x, 0f, _player.position.z), new Vector3(_target.x, 0f, _target.z));
                float dmg = AreaDamage.At(d, _radius, _damage);
                if (dmg > 0f) _playerHealth.TakeDamage(new DamageInfo { Amount = dmg, Point = _target, Direction = Vector3.down, Source = _source });
            }
            if (_destroysCover)
            {
                int n = Physics.OverlapSphereNonAlloc(_target, _radius, Buffer, ~HitboxLayer.Mask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++) { var d = Buffer[i].GetComponentInParent<Destructible>(); if (d != null) d.Smash(_target, Vector3.down); }
            }
            if (BloodFx.Instance != null) BloodFx.Instance.Spray(_target + Vector3.up * 0.1f, Vector3.up, 18, new Color(0.45f, 0.85f, 0.15f), 1.2f);
            Release();
        }

        private void Release() { if (gameObject.activeSelf) _pool.Release(this); }
    }
}
