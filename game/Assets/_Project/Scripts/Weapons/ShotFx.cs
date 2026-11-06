using UnityEngine;
using UnityEngine.Pool;
using SevenDays.Core;

namespace SevenDays.Weapons
{
    /// <summary>
    /// Efectos de disparo con object pooling: las balas visuales (trazadoras) y los impactos se
    /// reutilizan en vez de crearse y destruirse en cada disparo, para no generar basura ni tirones.
    /// El daño real se calcula con un raycast instantáneo en PlayerShooting.
    /// </summary>
    public class ShotFx
    {
        private readonly Transform _root;
        private readonly ObjectPool<Tracer> _tracers;
        private readonly ObjectPool<Impact> _impacts;
        private readonly ObjectPool<Casing> _casings;

        public ShotFx(Transform root)
        {
            _root = root;
            _tracers = new ObjectPool<Tracer>(CreateTracer, t => t.gameObject.SetActive(true),
                t => t.gameObject.SetActive(false), t => Object.Destroy(t.gameObject), false, 32, 64);
            _impacts = new ObjectPool<Impact>(CreateImpact, i => i.gameObject.SetActive(true),
                i => i.gameObject.SetActive(false), i => Object.Destroy(i.gameObject), false, 32, 64);
            _casings = new ObjectPool<Casing>(CreateCasing, c => c.gameObject.SetActive(true),
                c => c.gameObject.SetActive(false), c => Object.Destroy(c.gameObject), false, 24, 48);
        }

        /// <summary>Casquillo expulsado hacia la derecha del arma (con pooling: nunca más de 48 a la vez).</summary>
        public void SpawnCasing(Vector3 position, Vector3 right, Vector3 up, float size)
        {
            var c = _casings.Get();
            c.Launch(position, right * Random.Range(1.4f, 2.2f) + up * Random.Range(1.2f, 2f) + Random.insideUnitSphere * 0.3f, size);
        }

        private Casing CreateCasing()
        {
            var go = Prim.Create(PrimitiveType.Cube, "Casing", Vector3.zero, new Vector3(0.012f, 0.012f, 0.03f),
                Prim.GetMaterial(new Color(0.78f, 0.6f, 0.22f)), _root, false);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.layer = 2;
            var c = go.AddComponent<Casing>();
            c.Pool = _casings;
            return c;
        }

        public void SpawnTracer(Vector3 from, Vector3 to)
        {
            var t = _tracers.Get();
            t.Launch(from, to);
        }

        public void SpawnImpact(Vector3 point, Vector3 normal)
        {
            var i = _impacts.Get();
            i.Show(point + normal * 0.02f);
        }

        private Tracer CreateTracer()
        {
            var go = Prim.Create(PrimitiveType.Cube, "Tracer", Vector3.zero, new Vector3(0.03f, 0.03f, 0.6f),
                Prim.GetEmissive(new Color(1f, 0.8f, 0.35f), 6f), _root, false);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var t = go.AddComponent<Tracer>();
            t.Pool = _tracers;
            return t;
        }

        private Impact CreateImpact()
        {
            var go = Prim.Create(PrimitiveType.Sphere, "Impact", Vector3.zero, Vector3.one * 0.14f,
                Prim.GetEmissive(new Color(1f, 0.65f, 0.25f), 5f), _root, false);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var i = go.AddComponent<Impact>();
            i.Pool = _impacts;
            return i;
        }
    }

    public class Tracer : MonoBehaviour
    {
        private const float Speed = 260f;
        public IObjectPool<Tracer> Pool;
        private Vector3 _dir;
        private float _remaining;

        public void Launch(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            _remaining = delta.magnitude;
            _dir = _remaining > 0.001f ? delta / _remaining : transform.forward;
            transform.SetPositionAndRotation(from, Quaternion.LookRotation(_dir));
        }

        private void Update()
        {
            float step = Speed * Time.deltaTime;
            transform.position += _dir * step;
            _remaining -= step;
            if (_remaining <= 0f) Pool.Release(this);
        }
    }

    public class Impact : MonoBehaviour
    {
        public IObjectPool<Impact> Pool;
        private float _until;

        public void Show(Vector3 point)
        {
            transform.position = point;
            _until = Time.time + 0.1f;
        }

        private void Update()
        {
            if (Time.time >= _until) Pool.Release(this);
        }
    }

    /// <summary>Casquillo: vuela con gravedad, gira, rebota una vez en el suelo (buscado con un solo rayo al salir) y vuelve al pool.</summary>
    public class Casing : MonoBehaviour
    {
        public IObjectPool<Casing> Pool;
        private Vector3 _vel, _spin;
        private float _groundY, _until;
        private bool _bounced;

        public void Launch(Vector3 position, Vector3 velocity, float size)
        {
            transform.position = position;
            transform.localScale = new Vector3(0.012f, 0.012f, 0.03f) * size;
            _vel = velocity; _spin = Random.insideUnitSphere * 900f; _bounced = false;
            _until = Time.time + 1.6f;
            _groundY = Physics.Raycast(position, Vector3.down, out RaycastHit hit, 4f, ~(1 << 2), QueryTriggerInteraction.Ignore) ? hit.point.y + 0.01f : position.y - 1.2f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (Time.time >= _until) { Pool.Release(this); return; }
            _vel += Vector3.down * 14f * dt;
            var p = transform.position + _vel * dt;
            if (p.y <= _groundY)
            {
                p.y = _groundY;
                if (!_bounced) { _vel = new Vector3(_vel.x * 0.4f, -_vel.y * 0.3f, _vel.z * 0.4f); _bounced = true; _spin *= 0.4f; }
                else { _vel = Vector3.zero; _spin = Vector3.zero; }
            }
            transform.position = p;
            transform.Rotate(_spin * dt, Space.Self);
        }
    }
}
