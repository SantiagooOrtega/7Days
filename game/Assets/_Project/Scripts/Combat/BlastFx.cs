using System.Collections;
using UnityEngine;
using SevenDays.Look;

namespace SevenDays.Combat
{
    /// <summary>
    /// Efectos de una explosión con pooling: bola de fuego, humo que sube, destello de luz que se apaga solo y sonido de estallido. Todo se reutiliza
    /// (unos pocos sistemas de partículas y una luz), nada se crea durante el juego. También genera el siseo de la mecha.
    /// </summary>
    public class BlastFx : MonoBehaviour
    {
        public static BlastFx Instance { get; private set; }
        public static AudioClip HissClip { get; private set; }

        private const int Pool = 4;
        private ParticleSystem[] _smoke, _fire;
        private int _nextSmoke, _nextFire;
        private Light _light;
        private float _lightEnd, _lightPeak;
        private AudioSource _audio;
        private AudioClip _boom;

        public static BlastFx Create(Transform parent)
        {
            var go = new GameObject("BlastFx");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<BlastFx>();
            Instance = fx;
            fx._smoke = new ParticleSystem[Pool]; fx._fire = new ParticleSystem[Pool];
            for (int i = 0; i < Pool; i++) { fx._smoke[i] = fx.MakeSystem("Smoke" + i, ParticleMaterials.Soft, new Color(0.22f, 0.21f, 0.2f, 0.7f), 1.8f, 3.2f, 0.7f, 1.4f, -0.08f); fx._fire[i] = fx.MakeSystem("Fire" + i, ParticleMaterials.SoftAdditive, new Color(1f, 0.55f, 0.15f, 0.9f), 0.25f, 0.6f, 0.5f, 1.1f, -0.2f); }
            var l = new GameObject("Flash").AddComponent<Light>();
            l.transform.SetParent(fx.transform, false);
            l.type = LightType.Point; l.range = 12f; l.color = new Color(1f, 0.6f, 0.25f); l.shadows = LightShadows.None; l.enabled = false;
            fx._light = l;
            fx._audio = go.AddComponent<AudioSource>(); fx._audio.spatialBlend = 0.6f; fx._audio.playOnAwake = false; fx._audio.maxDistance = 80f; fx._audio.rolloffMode = AudioRolloffMode.Linear;
            fx._boom = Synth("blast", 0.9f, 120f, 30f, 1f, 0.6f);
            HissClip = Synth("hiss", 1.6f, 2600f, 5200f, 0.35f, 0.97f, true);
            return fx;
        }

        private ParticleSystem MakeSystem(string name, Material mat, Color color, float lifeMin, float lifeMax, float sizeMin, float sizeMax, float gravity)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = color; main.gravityModifier = gravity; main.maxParticles = 50;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.4f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;                                    // se desvanecen al final de su vida
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            go.SetActive(true);
            return ps;
        }

        public void Explode(Vector3 center, float radius)
        {
            var s = _smoke[_nextSmoke]; _nextSmoke = (_nextSmoke + 1) % Pool;
            var f = _fire[_nextFire]; _nextFire = (_nextFire + 1) % Pool;
            s.transform.position = center; f.transform.position = center;
            var shapeS = s.shape; shapeS.radius = Mathf.Max(0.4f, radius * 0.25f);
            var shapeF = f.shape; shapeF.radius = Mathf.Max(0.3f, radius * 0.18f);
            s.Emit(28); f.Emit(36);
            _light.transform.position = center + Vector3.up * 1f;
            _light.enabled = true; _lightPeak = 6f; _light.intensity = _lightPeak; _lightEnd = Time.time + 0.45f;
            _audio.transform.position = center;
            _audio.PlayOneShot(_boom, 1f);
        }

        private void Update()
        {
            if (!_light.enabled) return;
            float left = _lightEnd - Time.time;
            if (left <= 0f) { _light.enabled = false; return; }
            _light.intensity = _lightPeak * (left / 0.45f);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Sonido procedural. loud: ruido filtrado (siseo); si no, golpe grave con barrido de frecuencia.</summary>
        private static AudioClip Synth(string name, float seconds, float f0, float f1, float volume, float noise, bool hiss = false)
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
                float white = (float)rng.NextDouble() * 2f - 1f;
                if (hiss)
                {
                    lp += (white - lp) * 0.7f;                                 // ruido agudo, como gas escapando
                    data[i] = (white - lp * 0.5f) * volume * Mathf.Lerp(0.5f, 1f, t) * (1f + 0.15f * Mathf.Sin(phase * 0.002f));
                }
                else
                {
                    lp += (white - lp) * 0.12f;
                    float env = Mathf.Min(1f, i / 200f) * Mathf.Exp(-3.2f * t);
                    data[i] = (Mathf.Sin(phase) * (1f - noise) + lp * noise * 2f) * env * volume;
                }
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
