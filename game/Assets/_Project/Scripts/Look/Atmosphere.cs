using UnityEngine;
using UnityEngine.Rendering;

namespace SevenDays.Look
{
    /// <summary>
    /// Ambiente apocalíptico con partículas (URP no tiene niebla volumétrica): niebla baja que se arrastra por el suelo,
    /// bancos de bruma entre los edificios lejanos, ceniza flotando, columnas de humo negro y rayos de luz falsos.
    /// Todo se registra en AtmosphereBudget para poder recortarse si el FPS baja de 55.
    /// </summary>
    public static class Atmosphere
    {
        public static void Build(Transform root, Transform player, LookSettings look)
        {
            var a = look.Atmosphere;
            var atm = new GameObject("Atmosphere").transform;
            atm.SetParent(root, false);

            GroundFog(atm, look, a);
            Haze(atm, look, a);
            Ash(atm, player, a);
            SmokeColumns(atm, a);
            GodRays(atm, look, a);
            atm.gameObject.AddComponent<AtmosphereGovernor>();
        }

        // ---------------------------------------------------------------- helpers
        private static ParticleSystem New(string name, Transform parent, Vector3 pos, Material mat, out GameObject go)
        {
            go = new GameObject(name);
            go.SetActive(false); // se configura entero antes de empezar a emitir
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        private static void FadeInOut(ParticleSystem ps, float inAt, float outAt)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inAt), new GradientAlphaKey(1f, outAt), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        private static int Max(int n) => Mathf.Max(1, n);

        // ---------------------------------------------------------------- niebla baja
        private static void GroundFog(Transform parent, LookSettings look, AtmosphereSettings a)
        {
            int layers = a.GroundFogLayers;
            if (layers <= 0 || a.GroundFogDensity <= 0f) return;
            Color fog = look.EffectiveFogColor;
            float k = a.GroundFogDensity * a.Density;

            for (int i = 0; i < layers; i++)
            {
                bool flat = i % 2 == 0; // capas planas sobre el suelo y capas verticales a la altura de la vista
                var ps = New("GroundFog " + i, parent, new Vector3(0f, flat ? 0.3f + i * 0.35f : 0.8f, 0f), ParticleMaterials.Soft, out var go);
                int count = Max(Mathf.RoundToInt((flat ? 34 : 26) * k));

                var main = ps.main;
                main.loop = true;
                main.prewarm = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(16f, 26f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
                main.startSize = flat ? new ParticleSystem.MinMaxCurve(6f, 11f) : new ParticleSystem.MinMaxCurve(3.5f, 6f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                var c0 = new Color(fog.r * 1.05f, fog.g * 1.05f, fog.b * 1.05f, a.GroundFogAlpha * 0.5f);
                var c1 = new Color(fog.r * 1.05f, fog.g * 1.05f, fog.b * 1.05f, a.GroundFogAlpha);
                main.startColor = new ParticleSystem.MinMaxGradient(c0, c1);
                main.maxParticles = count;

                var em = ps.emission;
                em.rateOverTime = count / 20f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(56f, flat ? 0.1f : 0.6f, 56f);
                shape.randomDirectionAmount = 1f;

                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.25f;
                noise.frequency = 0.12f;
                FadeInOut(ps, 0.25f, 0.75f);

                var r = go.GetComponent<ParticleSystemRenderer>();
                r.renderMode = flat ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;

                go.SetActive(true);
                AtmosphereBudget.Register(ps, i >= 1 ? 2 : 3); // las capas extra se apagan antes
            }
        }

        // ---------------------------------------------------------------- bruma lejana
        private static void Haze(Transform parent, LookSettings look, AtmosphereSettings a)
        {
            int banks = a.Scaled(a.HazeBanks);
            if (banks <= 0) return;
            Color fog = look.EffectiveFogColor;
            float[] heights = { 5f, 11f, 18f };
            int perRing = Max(Mathf.CeilToInt(banks / (float)heights.Length));

            for (int i = 0; i < heights.Length; i++)
            {
                var ps = New("Haze " + i, parent, new Vector3(0f, heights[i], 0f), ParticleMaterials.Soft, out var go);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // el círculo queda tendido en el plano XZ

                var main = ps.main;
                main.loop = true;
                main.prewarm = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = 60f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
                main.startSize = new ParticleSystem.MinMaxCurve(28f, 48f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(fog.r, fog.g, fog.b, 0.04f), new Color(fog.r, fog.g, fog.b, 0.085f));
                main.maxParticles = perRing;

                var em = ps.emission;
                em.rateOverTime = perRing / 60f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 62f;
                shape.radiusThickness = 0.15f;
                shape.randomDirectionAmount = 1f;
                FadeInOut(ps, 0.2f, 0.8f);

                go.SetActive(true);
                AtmosphereBudget.Register(ps, 2);
            }
        }

        // ---------------------------------------------------------------- ceniza
        private static void Ash(Transform parent, Transform player, AtmosphereSettings a)
        {
            float k = a.Ash * a.Density;
            if (k <= 0f) return;
            var ps = New("Ash", parent, player.position + Vector3.up * 6f, ParticleMaterials.Soft, out var go);

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.15f, 0.15f, 0.9f), new Color(0.42f, 0.38f, 0.34f, 0.75f));
            main.gravityModifier = 0.03f;
            main.maxParticles = Max(Mathf.RoundToInt(420 * k));

            var em = ps.emission;
            em.rateOverTime = 40f * k;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(34f, 14f, 34f);
            shape.randomDirectionAmount = 1f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.3f;
            FadeInOut(ps, 0.15f, 0.85f);

            go.AddComponent<FollowTarget>().Init(player, new Vector3(0f, 6f, 0f));
            go.SetActive(true);
            AtmosphereBudget.Register(ps);
        }

        // ---------------------------------------------------------------- humo negro lejano
        private static void SmokeColumns(Transform parent, AtmosphereSettings a)
        {
            int n = a.Scaled(a.SmokeColumns);
            if (n <= 0 || a.Smoke <= 0f) return;
            for (int i = 0; i < n; i++)
            {
                float ang = (i * 360f / n + 40f) * Mathf.Deg2Rad;
                float dist = 78f + 12f * (i % 2);
                var pos = new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                var ps = New("Smoke " + i, parent, pos, ParticleMaterials.Soft, out var go);
                go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // el cono apunta hacia arriba

                var main = ps.main;
                main.loop = true;
                main.prewarm = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(16f, 22f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 4.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(7f, 10f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.04f, 0.04f, 0.05f, 0.5f), new Color(0.13f, 0.12f, 0.12f, 0.4f));
                main.maxParticles = Max(Mathf.RoundToInt(160 * a.Smoke));

                var em = ps.emission;
                em.rateOverTime = 7f * a.Smoke;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 7f;
                shape.radius = 3f;

                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.8f));

                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = 1.4f; // el viento las inclina
                vel.z = 0.5f;
                FadeInOut(ps, 0.12f, 0.6f);

                go.SetActive(true);
                AtmosphereBudget.Register(ps, 3);
            }
        }

        // ---------------------------------------------------------------- rayos de luz falsos
        private static void GodRays(Transform parent, LookSettings look, AtmosphereSettings a)
        {
            int total = a.Scaled(a.GodRays);
            if (total <= 0 || a.GodRayIntensity <= 0f) return;
            Vector3 sunDir = Quaternion.Euler(look.SunPitch, look.SunYaw, 0f) * Vector3.forward;
            int systems = 3;
            int per = Max(Mathf.CeilToInt(total / (float)systems));
            float[] zs = { 8f, 15f, 22f }; // entre las barreras, los contenedores y la nave del fondo

            for (int i = 0; i < systems; i++)
            {
                var ps = New("GodRays " + i, parent, new Vector3(0f, 7f, zs[i]), ParticleMaterials.Streak, out var go);
                go.transform.rotation = Quaternion.LookRotation(sunDir); // las partículas se alinean con la dirección del sol

                var main = ps.main;
                main.loop = true;
                main.prewarm = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 13f);
                main.startSpeed = 0.02f;
                main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 3.4f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.82f, a.GodRayIntensity * 0.5f), new Color(1f, 0.95f, 0.85f, a.GodRayIntensity));
                main.maxParticles = per;

                var em = ps.emission;
                em.rateOverTime = per / 11f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(50f, 1f, 1f); // se reparten en línea horizontal (eje X local)
                shape.randomDirectionAmount = 0f;
                FadeInOut(ps, 0.3f, 0.7f);

                var r = go.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = 8f;
                r.velocityScale = 0f;
                r.cameraVelocityScale = 0f;

                go.SetActive(true);
                AtmosphereBudget.Register(ps, 2);
            }
        }
    }
}
