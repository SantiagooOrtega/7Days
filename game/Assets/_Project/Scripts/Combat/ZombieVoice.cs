using UnityEngine;

namespace SevenDays.Combat
{
    /// <summary>Gruñidos de zombi sintetizados por código (sonido de relleno, sin archivos): 3 variantes de un gemido grave con vibrato y aire. Límite de voces simultáneas para no saturar.</summary>
    public static class ZombieVoice
    {
        private static AudioClip[] _clips;
        private static readonly float[] Until = new float[4];   // cuándo termina cada voz en uso

        public static AudioClip Clip(int i)
        {
            if (_clips == null)
            {
                _clips = new AudioClip[3];
                float[] f0 = { 95f, 118f, 80f }, seconds = { 1.1f, 0.85f, 1.4f };
                for (int k = 0; k < 3; k++) _clips[k] = Synth("zombi " + k, seconds[k], f0[k], k);
            }
            return _clips[Mathf.Abs(i) % _clips.Length];
        }

        /// <summary>true si hay hueco para otra voz; reserva ese hueco "seconds" segundos.</summary>
        public static bool TryReserve(float seconds)
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < Until.Length; i++) if (Until[i] <= now) { Until[i] = now + seconds; return true; }
            return false;
        }

        private static AudioClip Synth(string name, float seconds, float f0, int seed)
        {
            const int rate = 22050;
            int n = Mathf.CeilToInt(seconds * rate);
            var data = new float[n];
            var rng = new System.Random(seed * 77 + 5);
            float phase = 0f, lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                // el tono baja al final como un lamento, con vibrato lento
                float f = f0 * (1.15f - 0.3f * t) * (1f + 0.04f * Mathf.Sin(t * 22f + seed));
                phase += 2f * Mathf.PI * f / rate;
                float saw = (phase / (2f * Mathf.PI) % 1f) * 2f - 1f;
                lp += (((float)rng.NextDouble() * 2f - 1f) - lp) * 0.12f;      // aire / ronquido
                lp2 += (saw - lp2) * 0.25f;                                    // filtra los agudos de la sierra
                float env = Mathf.Min(1f, i / 1800f) * Mathf.Pow(1f - t, 0.8f) * (0.65f + 0.35f * Mathf.Sin(t * 9f));
                data[i] = (lp2 * 0.6f + lp * 0.45f) * env * 0.55f;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
