using UnityEngine;
using SevenDays.Look;

namespace SevenDays.Combat
{
    /// <summary>Sangre con pooling: un puñado de sistemas de partículas reutilizados (chorro pequeño por impacto, grande al morir, con más fuerza si fue headshot).</summary>
    public class BloodFx : MonoBehaviour
    {
        public static BloodFx Instance { get; private set; }
        private const int PoolSize = 10;
        private ParticleSystem[] _pool;
        private int _next;

        public static BloodFx Create(Transform parent)
        {
            var go = new GameObject("BloodFx");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<BloodFx>();
            Instance = fx;
            fx._pool = new ParticleSystem[PoolSize];
            for (int i = 0; i < PoolSize; i++) fx._pool[i] = fx.CreateSystem(i);
            return fx;
        }

        private ParticleSystem CreateSystem(int i)
        {
            var go = new GameObject("Blood" + i);
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.02f, 0.02f), new Color(0.7f, 0.05f, 0.04f));
            main.gravityModifier = 1.6f;
            main.maxParticles = 60;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 28f; shape.radius = 0.04f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMaterials.Soft;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(true);
            return ps;
        }

        /// <summary>direction: hacia donde sale la sangre (normalmente la dirección de la bala). big: muerte o headshot.</summary>
        public void Spray(Vector3 point, Vector3 direction, int count, Color? tint = null, float speedScale = 1f)
        {
            if (_pool == null) return;
            var ps = _pool[_next]; _next = (_next + 1) % PoolSize;
            ps.transform.SetPositionAndRotation(point, Quaternion.LookRotation(direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.up));
            if (tint == null && Mathf.Approximately(speedScale, 1f)) { ps.Emit(count); return; }
            var p = new ParticleSystem.EmitParams();
            if (tint != null) p.startColor = tint.Value;
            p.velocity = ps.transform.forward * (3f * speedScale);
            ps.Emit(p, count);   // emisión con parámetros propios (ácido verde, explosión naranja)
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
