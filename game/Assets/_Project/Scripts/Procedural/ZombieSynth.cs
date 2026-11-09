using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Eventos de sonido de un zombi. Los vocales se sintetizan con la voz del tipo; los demás (golpes, metal, burbujas, estallido) son efectos.</summary>
    public enum ZombieSound
    {
        Idle, Alert, Attack, Bite, PainHead, PainTorso, PainLimb, Headshot, Death, Emerge,
        Step, Metal, Gurgle, Retch, Fuse, Blast, Pant, Shriek, Roar,
    }

    /// <summary>Voz de un tipo: tono glotal, escala de formantes (garganta más grande = más grave), aspereza, aire, saturación y velocidad.</summary>
    public struct ZombieVoiceProfile
    {
        public float F0, Formant, Rough, Breath, Drive, Speed;

        public static ZombieVoiceProfile For(string typeId)
        {
            switch (typeId)
            {
                case ZombieIds.Runner: return new ZombieVoiceProfile { F0 = 150f, Formant = 1.1f, Rough = 0.45f, Breath = 0.7f, Drive = 2.2f, Speed = 1.35f };
                case ZombieIds.Tank: return new ZombieVoiceProfile { F0 = 58f, Formant = 0.74f, Rough = 0.85f, Breath = 0.35f, Drive = 3.2f, Speed = 0.75f };
                case ZombieIds.Armored: return new ZombieVoiceProfile { F0 = 88f, Formant = 0.9f, Rough = 0.6f, Breath = 0.4f, Drive = 2.6f, Speed = 0.95f };
                case ZombieIds.Spitter: return new ZombieVoiceProfile { F0 = 112f, Formant = 1.02f, Rough = 0.55f, Breath = 0.55f, Drive = 2f, Speed = 1f };
                case ZombieIds.Explosive: return new ZombieVoiceProfile { F0 = 100f, Formant = 0.95f, Rough = 0.5f, Breath = 0.6f, Drive = 1.8f, Speed = 0.9f };
                case ZombieIds.BossBrute: return new ZombieVoiceProfile { F0 = 48f, Formant = 0.68f, Rough = 0.95f, Breath = 0.3f, Drive = 3.6f, Speed = 0.7f };
                case ZombieIds.BossLauncher: return new ZombieVoiceProfile { F0 = 56f, Formant = 0.74f, Rough = 0.8f, Breath = 0.5f, Drive = 3f, Speed = 0.75f };
                case ZombieIds.BossFinal: return new ZombieVoiceProfile { F0 = 42f, Formant = 0.62f, Rough = 1f, Breath = 0.45f, Drive = 3.8f, Speed = 0.65f };
                default: return new ZombieVoiceProfile { F0 = 95f, Formant = 1f, Rough = 0.55f, Breath = 0.5f, Drive = 2.2f, Speed = 1f };
            }
        }
    }

    /// <summary>Qué suena en cada estado según el tipo (el corredor jadea en vez de gemir, el lanzador gorgotea, el tanque ruge...).</summary>
    public static class ZombieSoundMap
    {
        public static ZombieSound Idle(string typeId) => typeId == ZombieIds.Runner ? ZombieSound.Pant : (typeId == ZombieIds.Spitter ? ZombieSound.Gurgle : ZombieSound.Idle);
        public static ZombieSound Alert(string typeId) => typeId == ZombieIds.Runner ? ZombieSound.Shriek : (typeId == ZombieIds.Tank || ZombieAnatomies.IsBoss(typeId) ? ZombieSound.Roar : ZombieSound.Alert);
        public static ZombieSound Pain(HitZone zone) => zone == HitZone.Head ? ZombieSound.PainHead : (zone == HitZone.Torso ? ZombieSound.PainTorso : ZombieSound.PainLimb);

        /// <summary>Eventos que tiene cada tipo (los comunes a todos más los suyos).</summary>
        public static List<ZombieSound> EventsFor(string typeId)
        {
            var l = new List<ZombieSound> { ZombieSound.Idle, ZombieSound.Alert, ZombieSound.Attack, ZombieSound.Bite, ZombieSound.PainHead, ZombieSound.PainTorso, ZombieSound.PainLimb, ZombieSound.Headshot, ZombieSound.Death, ZombieSound.Emerge };
            switch (typeId)
            {
                case ZombieIds.Runner: l.Add(ZombieSound.Pant); l.Add(ZombieSound.Shriek); break;
                case ZombieIds.Tank: l.Add(ZombieSound.Step); l.Add(ZombieSound.Roar); break;
                case ZombieIds.Armored: l.Add(ZombieSound.Metal); break;
                case ZombieIds.Spitter: l.Add(ZombieSound.Gurgle); l.Add(ZombieSound.Retch); break;
                case ZombieIds.Explosive: l.Add(ZombieSound.Fuse); l.Add(ZombieSound.Blast); break;
                case ZombieIds.BossBrute: case ZombieIds.BossLauncher: case ZombieIds.BossFinal: l.Add(ZombieSound.Step); l.Add(ZombieSound.Roar); break;
            }
            return l;
        }

        /// <summary>Muestras distintas por evento (se elige una al azar, sin repetir la anterior). La mecha es un bucle único.</summary>
        public static int Variants(ZombieSound s) => s == ZombieSound.Fuse || s == ZombieSound.Emerge ? 1 : (s == ZombieSound.Idle || s == ZombieSound.Attack || s == ZombieSound.Step || s == ZombieSound.Metal ? 3 : 2);

        /// <summary>Los efectos no dependen del timbre de la garganta: se generan una vez por tipo.</summary>
        public static bool IsVocal(ZombieSound s) => s != ZombieSound.Headshot && s != ZombieSound.Step && s != ZombieSound.Metal && s != ZombieSound.Fuse && s != ZombieSound.Blast;
    }

    /// <summary>
    /// Sintetizador de voz de zombi (C# puro, sin archivos ni Unity): fuente glotal (pulso de Rosenberg) con vibrato irregular, jitter de tono y de amplitud y "fry"
    /// (periodos alternos más débiles, que da el tono roto), tres formantes resonantes que pasan de una vocal grave a otra ("aah", "oh", "uh", "ooh"), ruido de garganta
    /// mezclado con el pulso, respiración áspera, saturación suave (tanh) y una envolvente por evento. Los efectos (zarpazo, mordisco, metal, burbujas, golpe seco,
    /// estallido sordo, pisada) se hacen con ruido filtrado y senos. Todo es determinista por (tipo, evento, variante, timbre); se llama desde hilos de fondo.
    /// </summary>
    public static class ZombieSynth
    {
        public const int Rate = 22050;
        public const int Timbres = 2;

        // vocales graves (Hz): F1, F2, F3
        private static readonly float[] AA = { 730f, 1090f, 2440f }, OH = { 570f, 840f, 2410f }, UH = { 640f, 1190f, 2390f }, OO = { 320f, 870f, 2240f }, EH = { 530f, 1840f, 2480f };

        public static float[] Render(string typeId, ZombieSound ev, int variant, int timbre)
        {
            var p = ZombieVoiceProfile.For(typeId);
            // el timbre cambia el tamaño de la garganta y la aspereza (dos gargantas por tipo)
            p.Formant *= timbre == 0 ? 0.95f : 1.06f;
            p.Rough = Math.Min(1f, p.Rough * (timbre == 0 ? 1.12f : 0.9f));
            var rng = new Random(Hash(typeId) * 31 + (int)ev * 1009 + variant * 7919 + timbre * 104729);
            float[] d;
            switch (ev)
            {
                case ZombieSound.Idle: d = Voice(p, rng, 1.1f + 0.35f * variant, 1f, -0.25f, Path(variant, UH, OH, OO, AA), 0.6f, Swell(0.25f), 0.08f); break;
                case ZombieSound.Alert: d = Voice(p, rng, 0.75f, 1.35f, 0.35f, new[] { OH, AA, AA }, 1f, Attack(0.06f, 0.55f), 0.25f); break;
                case ZombieSound.Attack: d = Mix(Voice(p, rng, 0.55f, 1.25f, -0.2f, new[] { UH, AA, OH }, 1f, Attack(0.04f, 0.4f), 0.2f), Whoosh(rng, 0.32f, 0.12f + 0.05f * variant, 0.55f)); break;
                case ZombieSound.Bite: d = Mix(Voice(p, rng, 0.42f, 1.15f, 0.1f, new[] { EH, AA, UH }, 0.9f, Attack(0.03f, 0.35f), 0.3f), Crunch(rng, 0.3f, 0.18f, 0.8f)); break;
                case ZombieSound.PainHead: d = Voice(p, rng, 0.32f, 1.5f, -0.45f, new[] { AA, UH }, 1f, Attack(0.01f, 0.2f), 0.12f); break;
                case ZombieSound.PainTorso: d = Voice(p, rng, 0.42f, 1.1f, -0.3f, new[] { UH, OH }, 0.9f, Attack(0.02f, 0.25f), 0.3f); break;
                case ZombieSound.PainLimb: d = Voice(p, rng, 0.38f, 0.95f, -0.15f, new[] { OH, UH }, 0.8f, Attack(0.03f, 0.25f), 0.15f); break;
                case ZombieSound.Headshot: d = Thump(rng, 0.28f, 95f, 0.12f); break;
                case ZombieSound.Death: d = Mix(Voice(p, rng, 1.5f + 0.2f * variant, 1.05f, -0.55f, new[] { AA, OH, OO, OO }, 0.9f, Fade(0.08f), 0.1f), Bubbles(rng, 1.6f, 1.05f, 18f, 0.2f)); break;
                case ZombieSound.Emerge: d = Mix(Dirt(rng, 1.5f, 0f, 0.9f), Voice(p, rng, 1.3f, 0.85f, 0.25f, new[] { OO, UH, OH }, 0.55f, Swell(0.6f), 0.05f), 0.25f); break;
                case ZombieSound.Step: d = Mix(Thump(rng, 0.55f, 46f + 6f * variant, 0f), Dirt(rng, 0.45f, 0.01f, 0.35f)); break;
                case ZombieSound.Metal: d = Creak(rng, 0.55f + 0.15f * variant, 520f + 140f * variant); break;
                case ZombieSound.Gurgle: d = Mix(Voice(p, rng, 1.2f, 0.9f, -0.1f, new[] { UH, OO, UH }, 0.5f, Swell(0.3f), 0.05f), Bubbles(rng, 1.2f, 0f, 30f, 0.8f)); break;
                case ZombieSound.Retch: d = Mix(Voice(p, rng, 0.7f, 1.45f, 0.2f, new[] { EH, AA, UH }, 1f, Attack(0.02f, 0.5f), 0.45f), Bubbles(rng, 0.7f, 0.25f, 45f, 0.9f)); break;
                case ZombieSound.Fuse: d = FuseLoop(rng, 2f); break;
                case ZombieSound.Blast: d = Blast(rng, 1.4f); break;
                case ZombieSound.Pant: d = Pant(p, rng, 0.9f + 0.15f * variant); break;
                case ZombieSound.Shriek: d = Voice(p, rng, 0.6f, 2.6f, 0.15f, new[] { EH, AA, AA }, 1f, Attack(0.02f, 0.4f), 0.35f); break;
                case ZombieSound.Roar: d = Voice(p, rng, 1.3f + 0.2f * variant, 1.15f, -0.2f, new[] { OH, AA, AA, UH }, 1f, Attack(0.12f, 0.75f), 0.15f); break;
                default: d = new float[1]; break;
            }
            Normalize(d, Loudness(ev), ev != ZombieSound.Fuse);   // la mecha es un bucle: sin rampas en los bordes
            return d;
        }

        /// <summary>Volumen relativo de cada evento tras normalizar (el pico): gruñidos de fondo más bajos que un rugido.</summary>
        public static float Loudness(ZombieSound ev)
        {
            switch (ev)
            {
                case ZombieSound.Idle: case ZombieSound.Gurgle: case ZombieSound.Pant: return 0.6f;
                case ZombieSound.Metal: case ZombieSound.Fuse: return 0.55f;
                case ZombieSound.Step: case ZombieSound.Emerge: return 0.75f;
                default: return 0.9f;
            }
        }

        public static int Hash(string s) { unchecked { int h = 17; foreach (char c in s) h = h * 31 + c; return h & 0x7fffffff; } }

        // ------------------------------------------------------------------ voz
        private delegate float Envelope(float t);
        private static Envelope Swell(float peak) => t => Smooth(Math.Min(1f, t / peak)) * (float)Math.Pow(1f - t, 0.7) * (0.75f + 0.25f * (float)Math.Sin(t * 11f));
        private static Envelope Attack(float rise, float hold) => t => Math.Min(1f, t / rise) * (t < hold ? 1f : (float)Math.Pow(Math.Max(0f, 1f - (t - hold) / (1f - hold)), 1.6));
        private static Envelope Fade(float rise) => t => Math.Min(1f, t / rise) * (float)Math.Pow(1f - t, 1.3);
        private static float Smooth(float x) => x * x * (3f - 2f * x);

        private static float[][] Path(int variant, params float[][] v) { var r = new float[3][]; for (int i = 0; i < 3; i++) r[i] = v[(variant + i) % v.Length]; return r; }

        /// <summary>
        /// Voz: pulso glotal + ruido de garganta por tres formantes, con aire y saturación.
        /// pitch: multiplicador del tono del tipo; bend: cuánto sube (+) o cae (-) el tono a lo largo del sonido; vowels: recorrido de vocales; noiseMix: aire/aspereza extra.
        /// </summary>
        private static float[] Voice(ZombieVoiceProfile p, Random rng, float seconds, float pitch, float bend, float[][] vowels, float effort, Envelope env, float noiseMix)
        {
            seconds /= p.Speed;
            int n = (int)(seconds * Rate);
            var d = new float[n];
            var f = new Resonator[3];
            float[] bw = { 90f, 120f, 170f }, gain = { 1f, 0.55f, 0.25f };
            double phase = 0; float periodScale = 1f, periodAmp = 1f, prevG = 0f; int periodIndex = 0;
            float wander = 0f, wanderV = 0f, breathLp = 0f, aspLp = 0f, hp = 0f, hpIn = 0f;
            float vibRate = 4.5f + (float)rng.NextDouble() * 2.5f, vibPhase = (float)(rng.NextDouble() * 6.28);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                if ((i & 31) == 0)
                {
                    // vocal actual: interpolación suave entre los puntos del recorrido
                    float pos = t * (vowels.Length - 1);
                    int a = Math.Min((int)pos, vowels.Length - 2); float k = Smooth(pos - a);
                    for (int j = 0; j < 3; j++) f[j].Set((vowels[a][j] + (vowels[a + 1][j] - vowels[a][j]) * k) * p.Formant, bw[j] * (1f + p.Rough * 0.6f));
                }
                // vibrato irregular: dos senos lentos + deriva aleatoria suave
                if (i % 2205 == 0) wanderV = ((float)rng.NextDouble() * 2f - 1f) * 0.05f;       // nuevo destino cada 0.1 s, acotado a ±5 %
                wander += (wanderV - wander) * 0.0015f;
                float vib = 0.035f * (float)Math.Sin(vibPhase + t * seconds * vibRate * 6.283f) + 0.02f * (float)Math.Sin(t * seconds * 1.7f * 6.283f + 1.3f) + wander;
                float contour = 1f + bend * t + 0.12f * (float)Math.Sin(t * 3.1f) * bend;
                float f0 = Math.Max(25f, p.F0 * pitch * contour * (1f + vib) * periodScale);
                phase += f0 / Rate;
                if (phase >= 1.0)
                {
                    phase -= 1.0; periodIndex++;
                    // jitter (tono) y shimmer (amplitud) por periodo; "fry": periodos alternos más débiles
                    periodScale = 1f + (float)Gauss(rng) * 0.025f * (0.5f + p.Rough);
                    periodAmp = 1f + (float)Gauss(rng) * 0.18f * p.Rough;
                    if ((periodIndex & 1) == 1) periodAmp *= 1f - 0.55f * p.Rough;
                }
                float g = Glottal((float)phase) * periodAmp;
                float src = g - prevG; prevG = g;                                       // derivada = onda radiada por la boca
                float white = (float)rng.NextDouble() * 2f - 1f;
                aspLp += (white - aspLp) * 0.35f;
                float asp = aspLp * (0.25f + g) * (noiseMix + p.Rough * 0.25f) * 0.5f;     // ruido de garganta, más fuerte con el pulso
                float x = src * 9f + asp;
                float y = 0f;
                for (int j = 0; j < 3; j++) y += f[j].Run(x) * gain[j];
                // respiración áspera (banda 1-3 kHz) que no pasa por la voz
                breathLp += (white - breathLp) * 0.5f;
                hp = 0.92f * (hp + breathLp - hpIn); hpIn = breathLp;
                y += hp * p.Breath * 0.12f * (0.3f + noiseMix);
                float e = env(t) * effort;
                d[i] = (float)Math.Tanh(y * e * p.Drive * 0.6f);
            }
            return d;
        }

        /// <summary>Pulso glotal de Rosenberg: apertura 40 %, cierre 16 %, cerrado el resto.</summary>
        private static float Glottal(float ph)
        {
            const float open = 0.4f, close = 0.16f;
            if (ph < open) return 0.5f * (1f - (float)Math.Cos(Math.PI * ph / open));
            if (ph < open + close) return (float)Math.Cos(Math.PI * 0.5 * (ph - open) / close);
            return 0f;
        }

        private struct Resonator
        {
            private float _b1, _b2, _a0, _y1, _y2;
            public void Set(float freq, float bandwidth)
            {
                freq = Math.Min(freq, Rate * 0.45f);
                float r = (float)Math.Exp(-Math.PI * bandwidth / Rate);
                _b1 = 2f * r * (float)Math.Cos(2 * Math.PI * freq / Rate); _b2 = -r * r; _a0 = 1f - r;
            }
            public float Run(float x) { float y = _a0 * x + _b1 * _y1 + _b2 * _y2; _y2 = _y1; _y1 = y; return y; }
        }

        private static double Gauss(Random r) { double u = 1.0 - r.NextDouble(), v = r.NextDouble(); return Math.Sqrt(-2.0 * Math.Log(u)) * Math.Cos(2 * Math.PI * v); }

        // ------------------------------------------------------------------ efectos
        /// <summary>Jadeo del corredor: respiraciones rápidas (inspira/espira) ásperas con un poco de voz.</summary>
        private static float[] Pant(ZombieVoiceProfile p, Random rng, float seconds)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            float lp = 0f, bp = 0f, bpIn = 0f; double ph = 0;
            float cycle = 0.22f + (float)rng.NextDouble() * 0.04f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float c = t % cycle / cycle;
                bool exhale = (int)(t / cycle) % 2 == 0;
                float env = (float)Math.Sin(Math.PI * c) * (exhale ? 1f : 0.6f);
                float white = (float)rng.NextDouble() * 2f - 1f;
                lp += (white - lp) * (exhale ? 0.4f : 0.6f);
                bp = 0.9f * (bp + lp - bpIn); bpIn = lp;
                ph += p.F0 * 1.3f * (1f + 0.05f * (float)Math.Sin(t * 40f)) / Rate;
                float voice = exhale ? (float)Math.Tanh(Math.Sin(ph * 2 * Math.PI) * 3f) * 0.18f : 0f;
                d[i] = (bp * 0.9f + voice) * env * env;
            }
            return d;
        }

        /// <summary>Zarpazo: ráfaga de aire filtrado que barre de grave a agudo.</summary>
        private static float[] Whoosh(Random rng, float seconds, float at, float vol)
        {
            int n = (int)(seconds * Rate), s = (int)(at * Rate);
            var d = new float[n];
            float lp = 0f;
            for (int i = s; i < n; i++)
            {
                float t = (i - s) / (float)(n - s);
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * (0.05f + 0.35f * t);
                d[i] = lp * (float)Math.Sin(Math.PI * t) * vol * 2.2f;
            }
            return d;
        }

        /// <summary>Mordisco: chasquido de dientes y crujidos cortos.</summary>
        private static float[] Crunch(Random rng, float seconds, float at, float vol)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            for (int k = 0; k < 7; k++)
            {
                int s = (int)((at + k * 0.018f + (float)rng.NextDouble() * 0.01f) * Rate);
                float hz = 1500f + (float)rng.NextDouble() * 2500f;
                for (int i = 0; i < 0.03f * Rate && s + i < n; i++)
                {
                    float t = i / (float)Rate;
                    d[s + i] += ((float)rng.NextDouble() * 2f - 1f) * 0.6f + (float)Math.Sin(2 * Math.PI * hz * t) * 0.3f;
                    d[s + i] *= (float)Math.Exp(-120f * t) * vol * (k == 0 ? 1.4f : 1f);
                }
            }
            return d;
        }

        /// <summary>Golpe seco y sordo (headshot, pisada): seno grave que cae con un chasquido y ruido húmedo.</summary>
        private static float[] Thump(Random rng, float seconds, float hz, float click)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            double ph = 0; float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                ph += hz * (1f - 0.45f * t / seconds) / Rate;
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * 0.08f;
                float body = (float)Math.Sin(ph * 2 * Math.PI) * (float)Math.Exp(-12f / seconds * t * 0.5f);
                float wet = lp * 1.6f * (float)Math.Exp(-18f * t);
                float c = t < 0.006f ? ((float)rng.NextDouble() * 2f - 1f) * click * 6f : 0f;
                d[i] = (float)Math.Tanh((body + wet + c) * 1.5f);
            }
            return d;
        }

        /// <summary>Tierra que se rompe: granos cortos de ruido grave al azar, más densos al principio.</summary>
        private static float[] Dirt(Random rng, float seconds, float at, float vol)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            int grains = (int)(seconds * 40f);
            for (int k = 0; k < grains; k++)
            {
                float when = at + (float)Math.Pow(rng.NextDouble(), 1.8) * (seconds - at - 0.05f);
                int s = (int)(when * Rate); int len = (int)((0.02f + rng.NextDouble() * 0.05f) * Rate);
                float a = (0.3f + (float)rng.NextDouble() * 0.7f) * vol, lp = 0f, cut = 0.05f + (float)rng.NextDouble() * 0.2f;
                for (int i = 0; i < len && s + i < n; i++)
                {
                    lp += ((float)rng.NextDouble() * 2f - 1f - lp) * cut;
                    d[s + i] += lp * a * (float)Math.Sin(Math.PI * i / len) * 2f;
                }
            }
            return d;
        }

        /// <summary>Chirrido metálico (placas del blindado): fricción a golpes (stick-slip) sobre resonancias de chapa, y un clanc al final.</summary>
        private static float[] Creak(Random rng, float seconds, float hz)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            var r = new Resonator[3];
            r[0].Set(hz, 25f); r[1].Set(hz * 2.76f, 40f); r[2].Set(hz * 5.4f, 60f);
            float next = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float x = 0f;
                // tirones de fricción cada vez más seguidos y luego más espaciados
                if (i >= next) { x = 1f; next = i + Rate / (40f + 80f * (float)Math.Sin(Math.PI * t) + (float)rng.NextDouble() * 20f); }
                if (t > 0.85f && t < 0.86f) x += ((float)rng.NextDouble() * 2f - 1f) * 4f;           // clanc
                float y = r[0].Run(x) + r[1].Run(x) * 0.6f + r[2].Run(x) * 0.3f;
                d[i] = y * (float)Math.Sin(Math.PI * Math.Min(1f, t * 1.2f));
            }
            return d;
        }

        /// <summary>Burbujas (gorgoteo, mecha): cada una un seno corto que sube de tono al reventar.</summary>
        private static float[] Bubbles(Random rng, float seconds, float at, float perSecond, float vol)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            int count = (int)((seconds - at) * perSecond);
            for (int k = 0; k < count; k++)
            {
                int s = (int)((at + rng.NextDouble() * (seconds - at - 0.06f)) * Rate);
                float hz = 180f + (float)rng.NextDouble() * 520f, len = 0.03f + (float)rng.NextDouble() * 0.05f, a = vol * (0.3f + (float)rng.NextDouble() * 0.7f);
                double ph = 0;
                for (int i = 0; i < len * Rate && s + i < n; i++)
                {
                    float t = i / (float)Rate;
                    ph += hz * (1f + 4f * t / len) / Rate;
                    d[s + i] += (float)Math.Sin(ph * 2 * Math.PI) * a * (float)Math.Exp(-5f * t / len) * Math.Min(1f, i / 30f);
                }
            }
            return d;
        }

        /// <summary>Mecha del explosivo: siseo continuo con burbujeo, en bucle sin corte (el final se funde con el principio). El "crecer" lo hacen tono y volumen.</summary>
        private static float[] FuseLoop(Random rng, float seconds)
        {
            int n = (int)(seconds * Rate);
            var hiss = new float[n];
            float lp = 0f, prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)rng.NextDouble() * 2f - 1f;
                lp += (white - lp) * 0.6f;
                float h = lp - prev; prev = lp;                                          // paso-alto: siseo
                hiss[i] = h * (0.8f + 0.2f * (float)Math.Sin(i / (float)Rate * 2 * Math.PI * 7f)) * 0.7f;
            }
            var b = Bubbles(rng, seconds, 0f, 26f, 0.7f);
            for (int i = 0; i < n; i++) hiss[i] += b[i];
            // fundido cruzado de 0.2 s para que el bucle no haga clic
            int x = (int)(0.2f * Rate);
            var d = new float[n - x];
            for (int i = 0; i < d.Length; i++) d[i] = hiss[i + x];
            for (int i = 0; i < x; i++) { float k = i / (float)x; d[d.Length - x + i] = hiss[i + n - x] * (1f - k) + hiss[i] * k; }
            return d;
        }

        /// <summary>Estallido sordo: retumbe grave que cae de tono, ruido amortiguado y salpicón.</summary>
        private static float[] Blast(Random rng, float seconds)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            double ph = 0; float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                ph += (62f - 32f * Math.Min(1f, t / 0.5f)) / Rate;
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * 0.06f;
                lp2 += (lp - lp2) * 0.1f;
                float boom = (float)Math.Sin(ph * 2 * Math.PI) * (float)Math.Exp(-4.5f * t);
                float rumble = lp2 * 6f * (float)Math.Exp(-2.6f * t);
                float splat = t > 0.05f ? lp * 2f * (float)Math.Exp(-14f * (t - 0.05f)) : 0f;
                d[i] = (float)Math.Tanh((boom * 1.2f + rumble + splat * 0.6f) * Math.Min(1f, i / 40f) * 1.6f);
            }
            return d;
        }

        // ------------------------------------------------------------------ utilidades
        private static float[] Mix(float[] a, float[] b, float bGain = 1f)
        {
            var d = new float[Math.Max(a.Length, b.Length)];
            for (int i = 0; i < a.Length; i++) d[i] = a[i];
            for (int i = 0; i < b.Length; i++) d[i] += b[i] * bGain;
            return d;
        }

        /// <summary>Quita la continua, lleva el pico a "peak" y pone rampas de 3 ms en los bordes (sin clics).</summary>
        public static void Normalize(float[] d, float peak, bool ramps = true)
        {
            if (d.Length == 0) return;
            double mean = 0; foreach (float v in d) mean += v; mean /= d.Length;
            float max = 1e-6f;
            for (int i = 0; i < d.Length; i++) { d[i] -= (float)mean; max = Math.Max(max, Math.Abs(d[i])); }
            float k = peak / max;
            int ramp = ramps ? Math.Min(d.Length / 2, (int)(0.003f * Rate)) : 0;
            for (int i = 0; i < d.Length; i++)
            {
                float e = 1f;
                if (i < ramp) e = i / (float)ramp; else if (i >= d.Length - ramp) e = (d.Length - 1 - i) / (float)ramp;
                d[i] *= k * e;
            }
        }
    }
}
