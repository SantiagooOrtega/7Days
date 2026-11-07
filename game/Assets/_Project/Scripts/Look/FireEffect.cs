using UnityEngine;
using UnityEngine.Rendering;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.Look
{
    /// <summary>
    /// Un fuego completo reutilizable (para el pool): llama, humo, chispas, luz puntual parpadeante sin sombras y mancha de quemadura en el suelo.
    /// Las partículas se registran en AtmosphereBudget: el humo se apaga primero y las chispas después si el FPS baja de 55; la llama nunca baja
    /// del 15%.
    /// </summary>
    public class FireEffect : MonoBehaviour
    {
        public FireKind Kind { get; private set; }
        public FireProfile Profile { get; private set; }
        public Light Light { get; private set; }
        public bool InUse { get; private set; }

        private ParticleSystem _flames, _smoke, _sparks;
        private FlickerLight _flicker;
        private GameObject _scorch;
        private float _baseLight;

        public static FireEffect Create(Transform parent)
        {
            var go = new GameObject("Fire (pool)");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<FireEffect>();
            fx.Build();
            go.SetActive(false);
            return fx;
        }

        private void Build()
        {
            _flames = NewSystem("Flames", ParticleMaterials.Soft);
            _smoke = NewSystem("Smoke", ParticleMaterials.Soft);
            _sparks = NewSystem("Sparks", ParticleMaterials.Spark);
            _sparks.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(transform, false);
            Light = lightGo.AddComponent<Light>();
            Light.type = LightType.Point;
            Light.color = new Color(1f, 0.55f, 0.2f);
            Light.shadows = LightShadows.None; // sin sombras: barato
            _flicker = lightGo.AddComponent<FlickerLight>();
        }

        private ParticleSystem NewSystem(string name, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = Atmosphere.MaxScreenSize;   // el humo de un fuego cercano tampoco cubre la pantalla entera
            return ps;
        }

        /// <summary>Enciende el fuego en "pos" con el perfil del tipo y los ajustes del mapa.</summary>
        public void Ignite(Vector3 pos, FireKind kind, FireSettings s)
        {
            Kind = kind;
            Profile = FireProfile.For(kind);
            transform.position = pos;
            float fs = Profile.FlameScale * s.FlameScale;

            Configure(_flames, ps =>
            {
                var m = ps.main;
                m.loop = true; m.simulationSpace = ParticleSystemSimulationSpace.World;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f * Mathf.Sqrt(fs), 0.9f * Mathf.Sqrt(fs));
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * fs, 1.5f * fs);
                m.startSize = new ParticleSystem.MinMaxCurve(0.25f * fs, 0.45f * fs);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.1f, 0.8f), new Color(1f, 0.8f, 0.3f, 0.9f));
                m.gravityModifier = -0.15f;
                m.maxParticles = Mathf.RoundToInt(40f * fs);
                var e = ps.emission; e.rateOverTime = 26f * fs;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8f; sh.radius = 0.18f * fs;
                transform.GetChild(0).rotation = Quaternion.Euler(-90f, 0f, 0f);
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
                var col = ps.colorOverLifetime; col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(0.9f, 0.25f, 0.05f), 0.6f), new GradientColorKey(new Color(0.15f, 0.1f, 0.1f), 1f) },
                    new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
            });

            Configure(_smoke, ps =>
            {
                var m = ps.main;
                m.loop = true; m.simulationSpace = ParticleSystemSimulationSpace.World;
                m.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 6f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.5f * Mathf.Sqrt(fs), 1.1f * Mathf.Sqrt(fs));
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.1f, 0.1f, 0.1f, 0.32f), new Color(0.2f, 0.19f, 0.18f, 0.22f));
                m.gravityModifier = -0.04f;
                m.maxParticles = Mathf.RoundToInt(Mathf.Max(8f, 60f * Profile.SmokeRate / 12f + 20f));
                var e = ps.emission; e.rateOverTime = Profile.SmokeRate * s.SmokeAmount;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 14f; sh.radius = 0.2f * fs;
                transform.GetChild(1).rotation = Quaternion.Euler(-90f, 0f, 0f);
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 3.2f));
                var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.x = 0.5f; vel.z = 0.2f;
                var col = ps.colorOverLifetime; col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
            });

            Configure(_sparks, ps =>
            {
                var m = ps.main;
                m.loop = true; m.simulationSpace = ParticleSystemSimulationSpace.World;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.7f, 0.3f, 1f), new Color(1f, 0.95f, 0.6f, 1f));
                m.gravityModifier = 0.5f;
                m.maxParticles = Mathf.RoundToInt(Mathf.Max(6f, 3f * Profile.SparkRate));
                var e = ps.emission; e.rateOverTime = Profile.SparkRate * s.SparkAmount;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 25f; sh.radius = 0.15f * fs;
                var col = ps.colorOverLifetime; col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.5f, 0.2f), 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                var r = ps.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 3f; r.velocityScale = 0.05f;
            });

            _baseLight = Profile.LightIntensity * s.LightIntensity;
            Light.range = Profile.LightRange;
            Light.intensity = _baseLight;
            _flicker.Amount = 0.35f;
            _flicker.Refresh(); // recoge la intensidad y el rango nuevos como base del parpadeo

            BuildScorch();
            InUse = true;
            gameObject.SetActive(true);
            foreach (var ps in new[] { _flames, _smoke, _sparks }) { ps.Clear(); ps.Play(true); }

            // recorte por FPS: el humo se apaga primero, luego las chispas; la llama conserva un mínimo
            AtmosphereBudget.Register(_flames);
            AtmosphereBudget.Register(_smoke, 3);
            AtmosphereBudget.Register(_sparks, 2);
        }

        public void Extinguish()
        {
            if (!InUse) return;
            InUse = false;
            AtmosphereBudget.Unregister(_flames);
            AtmosphereBudget.Unregister(_smoke);
            AtmosphereBudget.Unregister(_sparks);
            foreach (var ps in new[] { _flames, _smoke, _sparks }) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_scorch != null) _scorch.SetActive(false);
            gameObject.SetActive(false);
        }

        private static void Configure(ParticleSystem ps, System.Action<ParticleSystem> setup)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // las propiedades principales no se pueden cambiar mientras emite
            setup(ps);
        }

        /// <summary>Mancha de quemadura plana bajo el fuego (calcomanía, un quad por fuego).</summary>
        private void BuildScorch()
        {
            if (_scorch != null) { Destroy(_scorch); _scorch = null; }
            if (Profile.ScorchSize <= 0f) return;
            var b = new MeshBatch();
            b.AddFlatQuad(new Vector3(transform.position.x, 0.048f, transform.position.z), Random.value * 360f, Profile.ScorchSize, Profile.ScorchSize);
            _scorch = b.Spawn("Scorch", transform.parent, Prim.GetDecal(DecalLibrary.Get(DecalKind.Burn), Color.white, 0.05f, "Burn"));
        }

        private void OnDestroy() { if (_scorch != null) Destroy(_scorch); }
    }
}
