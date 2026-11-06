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

        public ShotFx(Transform root)
        {
            _root = root;
            _tracers = new ObjectPool<Tracer>(CreateTracer, t => t.gameObject.SetActive(true),
                t => t.gameObject.SetActive(false), t => Object.Destroy(t.gameObject), false, 32, 64);
            _impacts = new ObjectPool<Impact>(CreateImpact, i => i.gameObject.SetActive(true),
                i => i.gameObject.SetActive(false), i => Object.Destroy(i.gameObject), false, 32, 64);
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
}
