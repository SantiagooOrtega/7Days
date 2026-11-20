using System.Collections.Generic;
using UnityEngine;

namespace SevenDays.UI
{
    /// <summary>
    /// Sonidos de la intro en cómic. Si en Resources/Intro hay un clip con el mismo nombre (radio_vigia, radio_ibarra, tecla, estatica, titulo) se usa ese;
    /// si no, se genera por código una sola vez y se guarda en caché, como ProceduralAudio.
    /// </summary>
    public static class IntroAudio
    {
        private const int Rate = 22050;
        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        /// <summary>VIGÍA: dos pitidos de radio.</summary>
        public static AudioClip Vigia() => Get("radio_vigia", RadioBeep);
        /// <summary>Coronel Ibarra: ráfaga de estática de radio militar que se corta con un clic.</summary>
        public static AudioClip Ibarra() => Get("radio_ibarra", RadioBurst);
        /// <summary>Golpe de tecla de la máquina de escribir (el tono cambia con el pitch según quién habla).</summary>
        public static AudioClip Key() => Get("tecla", KeyClick);
        /// <summary>Estática suave en bucle (viñetas sin imagen y fondo de la radio de Ibarra).</summary>
        public static AudioClip Static() => Get("estatica", Hiss);
        /// <summary>Golpe grave del título "7 DÍAS".</summary>
        public static AudioClip Title() => Get("titulo", Boom);

        private static AudioClip Get(string name, System.Func<float[]> make)
        {
            var file = Resources.Load<AudioClip>("Intro/" + name);
            if (file != null) return file;
            if (Cache.TryGetValue(name, out var c) && c != null) return c;
            var d = make();
            c = AudioClip.Create("intro_" + name, d.Length, 1, Rate, false);
            c.SetData(d, 0);
            Cache[name] = c;
            return c;
        }

        private static float[] RadioBeep()
        {
            float len = 0.34f;
            int n = Mathf.CeilToInt(len * Rate);
            var d = new float[n];
            var rng = new System.Random(7);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float v = Tone(t, 0f, 0.08f, 1250f) + Tone(t, 0.12f, 0.22f, 1660f);
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * 0.35f;
                v += lp * 0.06f * Edge(t, len, 0.01f);   // un soplo de radio debajo
                d[i] = v * 0.42f;
            }
            return d;
        }

        private static float[] RadioBurst()
        {
            float len = 0.55f;
            int n = Mathf.CeilToInt(len * Rate);
            var d = new float[n];
            var rng = new System.Random(11);
            float fast = 0f, slow = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float white = (float)rng.NextDouble() * 2f - 1f;
                fast += (white - fast) * 0.5f;     // quita lo más agudo
                slow += (fast - slow) * 0.06f;     // y lo más grave: queda la banda de la voz por radio
                float band = fast - slow;
                float crackle = rng.NextDouble() < 0.006 ? ((float)rng.NextDouble() * 2f - 1f) * 0.7f : 0f;
                float v = (band * 1.3f + crackle) * Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 4f);
                float tc = t - (len - 0.07f);      // clic del squelch al soltar el botón de la radio
                if (tc >= 0f) v += Mathf.Exp(-tc * 700f) * Mathf.Sin(2f * Mathf.PI * 900f * tc) * 0.45f;
                d[i] = v * Edge(t, len, 0.004f) * 0.6f;
            }
            return d;
        }

        private static float[] KeyClick()
        {
            float len = 0.045f;
            int n = Mathf.CeilToInt(len * Rate);
            var d = new float[n];
            var rng = new System.Random(3);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 170f) * 0.5f;
                float ping = Mathf.Sin(2f * Mathf.PI * 2300f * t) * Mathf.Exp(-t * 220f) * 0.22f;
                float thump = Mathf.Sin(2f * Mathf.PI * 170f * t) * Mathf.Exp(-t * 90f) * 0.3f;
                d[i] = (noise + ping + thump) * Edge(t, len, 0.003f);
            }
            return d;
        }

        private static float[] Hiss()
        {
            float len = 2f;
            int n = Mathf.CeilToInt(len * Rate);
            var d = new float[n];
            var rng = new System.Random(5);
            float fast = 0f, slow = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)rng.NextDouble() * 2f - 1f;
                fast += (white - fast) * 0.6f;
                slow += (fast - slow) * 0.04f;
                float crackle = rng.NextDouble() < 0.0015 ? ((float)rng.NextDouble() * 2f - 1f) * 0.6f : 0f;
                d[i] = (fast - slow) * 0.32f + crackle;
            }
            return d;
        }

        private static float[] Boom()
        {
            float len = 2.2f;
            int n = Mathf.CeilToInt(len * Rate);
            var d = new float[n];
            var rng = new System.Random(9);
            float phase = 0f, rumble = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float hz = Mathf.Lerp(72f, 36f, Mathf.Clamp01(t / 1.2f));   // cae de tono, como un golpe de bombo
                phase += 2f * Mathf.PI * hz / Rate;
                rumble += ((float)rng.NextDouble() * 2f - 1f - rumble) * 0.02f;
                float v = Mathf.Sin(phase) * Mathf.Exp(-t * 1.7f) * 0.85f + rumble * 2.2f * Mathf.Exp(-t * 2.5f);
                d[i] = v * Mathf.Min(1f, t / 0.005f) * Edge(t, len, 0.05f);
            }
            return d;
        }

        /// <summary>Pitido casi cuadrado (suena a radio) entre start y end, con bordes suaves para que no chasquee.</summary>
        private static float Tone(float t, float start, float end, float hz)
        {
            if (t < start || t > end) return 0f;
            float local = t - start, dur = end - start;
            float env = Mathf.Min(1f, local / 0.005f) * Mathf.Min(1f, (dur - local) / 0.008f);
            float ph = 2f * Mathf.PI * hz * local;
            return (Mathf.Sin(ph) + 0.3f * Mathf.Sin(3f * ph) + 0.15f * Mathf.Sin(5f * ph)) * 0.7f * env;
        }

        /// <summary>Fundido corto al final del clip para que no termine con un chasquido.</summary>
        private static float Edge(float t, float len, float fade) => Mathf.Clamp01((len - t) / fade);
    }
}
