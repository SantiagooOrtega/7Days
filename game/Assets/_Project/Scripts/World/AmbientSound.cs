using UnityEngine;
using SevenDays.Core;

namespace SevenDays.World
{
    /// <summary>
    /// Sonido ambiente del mapa, sintetizado (sin archivos) y en el grupo "Ambiente": "wind" = viento con ráfagas; "forest" = viento entre los árboles, grillos y
    /// crujidos de madera sueltos alrededor del jugador. "none" no suena (el mapa 1, como antes).
    /// </summary>
    public class AmbientSound : MonoBehaviour
    {
        private const int Rate = 22050;
        private AudioSource _wind, _crickets, _creak;
        private AudioClip[] _creaks;
        private Transform _listener;
        private float _nextCreak;

        public static AmbientSound Create(Transform parent, string soundscape, Transform listener)
        {
            if (string.IsNullOrEmpty(soundscape) || soundscape == "none") return null;
            var go = new GameObject("Sonido ambiente");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AmbientSound>();
            a._listener = listener;
            bool forest = soundscape == "forest";
            a._wind = a.Loop("Viento", Wind(8f, forest ? 0.55f : 0.8f), forest ? 0.32f : 0.4f);
            if (forest)
            {
                a._crickets = a.Loop("Grillos", Crickets(4f), 0.22f);
                a._creaks = new[] { Creak(0.9f, 1), Creak(1.2f, 2), Creak(0.7f, 3) };
                a._creak = go.AddComponent<AudioSource>();
                a._creak.spatialBlend = 1f; a._creak.rolloffMode = AudioRolloffMode.Logarithmic; a._creak.minDistance = 4f; a._creak.maxDistance = 40f; a._creak.playOnAwake = false;
                AudioMix.Route(a._creak, AudioGroup.Ambient, 0.6f);
                a._nextCreak = Time.time + 4f;
            }
            return a;
        }

        private AudioSource Loop(string name, AudioClip clip, float volume)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip; s.loop = true; s.spatialBlend = 0f; s.playOnAwake = false;
            AudioMix.Route(s, AudioGroup.Ambient, volume);
            s.Play();
            return s;
        }

        private void Update()
        {
            if (_creak == null || _listener == null || Time.time < _nextCreak) return;
            _nextCreak = Time.time + Random.Range(5f, 14f);
            // crujido de una rama o un tronco en algún sitio a 8-20 m
            Vector2 c = Random.insideUnitCircle.normalized * Random.Range(8f, 20f);
            _creak.transform.position = _listener.position + new Vector3(c.x, 3f, c.y);
            _creak.pitch = Random.Range(0.85f, 1.15f);
            _creak.PlayOneShot(_creaks[Random.Range(0, _creaks.Length)], Random.Range(0.6f, 1f));
        }

        private static AudioClip Make(string name, float[] d) { var c = AudioClip.Create(name, d.Length, 1, Rate, false); c.SetData(d, 0); return c; }

        /// <summary>Viento: ruido grave filtrado con ráfagas lentas; el final se funde con el principio (bucle sin corte).</summary>
        private static AudioClip Wind(float seconds, float gust)
        {
            int n = (int)(seconds * Rate); var raw = new float[n];
            var rng = new System.Random(5); float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * 0.025f; lp2 += (lp - lp2) * 0.05f;
                float g = 0.55f + gust * 0.45f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 2f) * Mathf.Sin(t * Mathf.PI * 2f * 3f + 1f));
                raw[i] = (lp * 6f + lp2 * 10f) * g;
            }
            return Make("viento", Seamless(raw, 0.6f));
        }

        /// <summary>Grillos: trinos de pulsos agudos a ritmos distintos (tres grillos), bucle de pocos segundos.</summary>
        private static AudioClip Crickets(float seconds)
        {
            int n = (int)(seconds * Rate); var d = new float[n];
            float[] hz = { 4300f, 4700f, 3900f }, rate = { 2.6f, 3.3f, 1.9f }, vol = { 0.18f, 0.12f, 0.09f };
            for (int k = 0; k < 3; k++)
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float chirp = Mathf.Repeat(t * rate[k] + k * 0.31f, 1f);
                    if (chirp > 0.22f) continue;
                    float pulse = Mathf.Repeat(chirp * 60f, 1f) < 0.5f ? 1f : 0.25f;
                    d[i] += Mathf.Sin(2f * Mathf.PI * hz[k] * t) * vol[k] * pulse * Mathf.Sin(chirp / 0.22f * Mathf.PI);
                }
            return Make("grillos", d);
        }

        /// <summary>Crujido de madera: fricción a tirones sobre resonancias graves.</summary>
        private static AudioClip Creak(float seconds, int seed)
        {
            int n = (int)(seconds * Rate); var d = new float[n];
            var rng = new System.Random(seed * 41);
            float f0 = 180f + seed * 40f, ph = 0f, next = 0f, env = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                if (i >= next) { env = 1f; next = i + Rate / (35f + 50f * Mathf.Sin(Mathf.PI * t) + (float)rng.NextDouble() * 15f); }
                env *= 0.996f;
                ph += 2f * Mathf.PI * f0 * (1f + 0.3f * t) / Rate;
                d[i] = (Mathf.Sin(ph) * 0.6f + Mathf.Sin(ph * 2.7f) * 0.25f) * env * Mathf.Sin(Mathf.PI * t) * 0.5f;
            }
            return Make("crujido " + seed, d);
        }

        private static float[] Seamless(float[] raw, float fadeSeconds)
        {
            int x = (int)(fadeSeconds * Rate), n = raw.Length;
            var d = new float[n - x];
            for (int i = 0; i < d.Length; i++) d[i] = raw[i + x];
            for (int i = 0; i < x; i++) { float k = i / (float)x; d[d.Length - x + i] = raw[i + n - x] * (1f - k) + raw[i] * k; }
            float peak = 1e-4f; foreach (float v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < d.Length; i++) d[i] *= 0.6f / peak;
            return d;
        }
    }
}
