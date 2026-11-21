using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Procesado de la voz de VIGÍA (puro, sobre muestras mono): filtro de radio (paso banda ~350-3400 Hz), un poco de saturación, eco corto, compresión y
    /// normalización para que se oiga encima de armas y zombis, y un bip de apertura y otro de cierre.
    /// </summary>
    public static class RadioFx
    {
        public const float LowCut = 350f, HighCut = 3400f, EchoSeconds = 0.085f, EchoGain = 0.22f, BeepHz = 1500f, BeepSeconds = 0.07f, Gap = 0.05f;

        public static float[] Process(float[] input, int rate)
        {
            int beep = (int)(BeepSeconds * rate), gap = (int)(Gap * rate), echo = (int)(EchoSeconds * rate);
            var voice = new float[input.Length + echo];
            Array.Copy(input, voice, input.Length);
            // paso alto y paso bajo de 2 polos (biquads RBJ)
            Biquad(voice, rate, LowCut, true);
            Biquad(voice, rate, HighCut, false);
            // eco corto
            for (int i = voice.Length - 1; i >= echo; i--) voice[i] += voice[i - echo] * EchoGain;
            // saturación suave + compresión (tanh) y normalización a -1 dBFS
            float peak = 1e-6f;
            for (int i = 0; i < voice.Length; i++) peak = Math.Max(peak, Math.Abs(voice[i]));
            float drive = 2.2f / peak;
            for (int i = 0; i < voice.Length; i++) voice[i] = (float)Math.Tanh(voice[i] * drive);
            float max = 1e-6f;
            for (int i = 0; i < voice.Length; i++) max = Math.Max(max, Math.Abs(voice[i]));
            float norm = 0.89f / max;
            for (int i = 0; i < voice.Length; i++) voice[i] *= norm;

            var o = new float[beep + gap + voice.Length + gap + beep];
            WriteBeep(o, 0, beep, rate, BeepHz);
            Array.Copy(voice, 0, o, beep + gap, voice.Length);
            WriteBeep(o, beep + gap + voice.Length + gap, beep, rate, BeepHz * 0.75f);
            return o;
        }

        private static void WriteBeep(float[] o, int start, int n, int rate, float hz)
        {
            for (int i = 0; i < n; i++)
            {
                float env = Math.Min(1f, Math.Min(i, n - i) / (0.008f * rate));
                o[start + i] = 0.35f * env * (float)Math.Sin(2 * Math.PI * hz * i / rate);
            }
        }

        private static void Biquad(float[] x, int rate, float freq, bool highPass)
        {
            double w = 2 * Math.PI * freq / rate, cos = Math.Cos(w), alpha = Math.Sin(w) / (2 * 0.707);
            double b0, b1, b2;
            if (highPass) { b0 = (1 + cos) / 2; b1 = -(1 + cos); b2 = (1 + cos) / 2; }
            else { b0 = (1 - cos) / 2; b1 = 1 - cos; b2 = (1 - cos) / 2; }
            double a0 = 1 + alpha, a1 = -2 * cos, a2 = 1 - alpha;
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < x.Length; i++)
            {
                double y = (b0 * x[i] + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0;
                x2 = x1; x1 = x[i]; y2 = y1; y1 = y;
                x[i] = (float)y;
            }
        }

        /// <summary>Cuánto dura lo procesado (segundos) a partir de la duración del clip original.</summary>
        public static double ProcessedSeconds(double clipSeconds) => clipSeconds + EchoSeconds + 2 * (BeepSeconds + Gap);
    }
}
