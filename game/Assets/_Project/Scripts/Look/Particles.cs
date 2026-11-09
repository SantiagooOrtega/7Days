using UnityEngine;
using UnityEngine.Rendering;

namespace SevenDays.Look
{
    /// <summary>Partículas ambientales creadas por código (polvo flotante y fuego), usando el material de partículas por defecto de URP.</summary>
    public static class Particles
    {
        private static Material DefaultParticleMaterial => ParticleMaterials.Soft;

        /// <summary>Polvo en suspensión alrededor del objetivo (sigue al jugador, así hay pocas partículas en total).</summary>
        public static Transform CreateDust(Transform follow)
        {
            var go = new GameObject("DustParticles");
            go.SetActive(false); // se configura antes de que empiece a emitir
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.78f, 0.65f, 0.12f), new Color(0.95f, 0.9f, 0.8f, 0.35f));
            main.maxParticles = 500;
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = 55f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 9f, 36f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.25f;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = DefaultParticleMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;

            go.AddComponent<FollowTarget>().Init(follow, new Vector3(0f, 4f, 0f));
            go.SetActive(true);
            return go.transform;
        }

        /// <summary>Vapor de la furia de un jefe: humo claro que sale de la espalda y sube (empieza sin emitir; el cuerpo sube el ritmo con la furia).</summary>
        public static ParticleSystem CreateSteam(Transform parent, Vector3 localPos, float size)
        {
            var go = new GameObject("Vapor");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * size, 1.4f * size);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f * size, 0.5f * size);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.78f, 0.74f, 0.18f), new Color(0.9f, 0.88f, 0.85f, 0.32f));
            main.maxParticles = 60;
            main.gravityModifier = -0.05f;
            var emission = ps.emission; emission.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.25f * size;
            var grow = ps.sizeOverLifetime; grow.enabled = true; grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = DefaultParticleMaterial; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            go.SetActive(true);
            return ps;
        }

        /// <summary>Chispas que saltan de un barril encendido: rayitas brillantes (aditivas, color HDR para el Bloom) que suben y caen.</summary>
        public static ParticleSystem CreateSparks(Transform parent, Vector3 pos, float amount)
        {
            if (amount <= 0f) return null;
            var go = new GameObject("Sparks");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // el cono apunta hacia arriba
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.7f, 0.3f, 1f), new Color(1f, 0.95f, 0.6f, 1f));
            main.gravityModifier = 0.5f;
            main.maxParticles = Mathf.Max(4, Mathf.RoundToInt(24 * amount));

            var emission = ps.emission;
            emission.rateOverTime = 8f * amount;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.15f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.5f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMaterials.Spark;
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 3f;
            r.velocityScale = 0.05f;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(true);
            AtmosphereBudget.Register(ps);
            return ps;
        }

        /// <summary>Llamas pequeñas para un barril encendido.</summary>
        public static void CreateFire(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("Fire");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.1f, 0.8f), new Color(1f, 0.8f, 0.3f, 0.9f));
            main.maxParticles = 40;
            main.gravityModifier = -0.15f;

            var emission = ps.emission;
            emission.rateOverTime = 26f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.18f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(0.9f, 0.25f, 0.05f), 0.6f), new GradientColorKey(new Color(0.15f, 0.1f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = DefaultParticleMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(true);
        }
    }

    /// <summary>Mantiene un objeto sobre otro (con desfase) sin emparentarlo.</summary>
    public class FollowTarget : MonoBehaviour
    {
        private Transform _target;
        private Vector3 _offset;

        public void Init(Transform target, Vector3 offset)
        {
            _target = target;
            _offset = offset;
        }

        private void LateUpdate()
        {
            if (_target != null) transform.position = _target.position + _offset;
        }
    }
}
