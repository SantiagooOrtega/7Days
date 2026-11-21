using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Procesado de la voz de VIGÍA (puro, sobre muestras mono): un toque leve de comunicación, no un robot. Paso banda ancho (~180-6500 Hz, conserva el cuerpo
    /// y la claridad de la voz), saturación muy suave mezclada (25 %), sin compresión fuerte (solo normalización a -2 dBFS), eco muy corto y sutil, y un bip de
    /// apertura y otro de cierre suaves.
    /// </summary>
    public static class RadioFx
    {
        public const float LowCut = 180f, HighCut = 6500f, EchoSeconds = 0.045f, EchoGain = 0.08f, BeepHz = 1170f, BeepSeconds = 0.06f, Gap = 0.05f;
        public const float SaturationMix = 0.25f, BeepGain = 0.15f, Peak = 0.8f;

        public static float[] Process(float[] input, int rate)
        {
            int beep = (int)(BeepSeconds * rate), gap = (int)(Gap * rate), echo = (int)(EchoSeconds * rate);
            var voice = new float[input.Length + echo];
            Array.Copy(input, voice, input.Length);
            // paso alto y paso bajo de 2 polos (biquads RBJ), banda ancha
            Biquad(voice, rate, LowCut, true);
            Biquad(voice, rate, HighCut, false);
            // eco muy corto y bajo: da "espacio" sin sonar a lata
            for (int i = voice.Length - 1; i >= echo; i--) voice[i] += voice[i - echo] * EchoGain;
            // saturación suave MEZCLADA (la voz limpia manda) y normalización; sin compresión que aplaste la dinámica
            float peak = 1e-6f;
            for (int i = 0; i < voice.Length; i++) peak = Math.Max(peak, Math.Abs(voice[i]));
            float inv = 1f / peak, k = (float)Math.Tanh(1.2);
            for (int i = 0; i < voice.Length; i++)
            {
                float x = voice[i] * inv;
                voice[i] = x * (1f - SaturationMix) + (float)Math.Tanh(x * 1.2f) / k * SaturationMix;
            }
            float max = 1e-6f;
            for (int i = 0; i < voice.Length; i++) max = Math.Max(max, Math.Abs(voice[i]));
            float norm = Peak / max;
            for (int i = 0; i < voice.Length; i++) voice[i] *= norm;

            var o = new float[beep + gap + voice.Length + gap + beep];
            WriteBeep(o, 0, beep, rate, BeepHz);
            Array.Copy(voice, 0, o, beep + gap, voice.Length);
            WriteBeep(o, beep + gap + voice.Length + gap, beep, rate, BeepHz * 0.8f);
            return o;
        }

        private static void WriteBeep(float[] o, int start, int n, int rate, float hz)
        {
            for (int i = 0; i < n; i++)
            {
                float env = Math.Min(1f, Math.Min(i, n - i) / (0.012f * rate));   // entrada y salida suaves (sin chasquido)
                o[start + i] = BeepGain * env * (float)Math.Sin(2 * Math.PI * hz * i / rate);
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
