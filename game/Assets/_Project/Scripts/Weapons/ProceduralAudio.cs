using System.Collections.Generic;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Weapons
{
    /// <summary>
    /// Sonidos provisionales generados por código (sin archivos): disparo distinto por tipo de arma, bombeo/cerrojo, recarga, golpe en vacío, pasos y respiración.
    /// Se generan una sola vez y se cachean. Cuando haya audio real basta con reemplazar estas funciones por AudioClips importados.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int Rate = 22050;
        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        public static AudioClip Shot(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Pistol: return Get("shot_pistol", () => Burst(0.22f, 900f, 0.55f, 180f, 0.5f, 18f));
                case WeaponKind.Revolver: return Get("shot_revolver", () => Burst(0.4f, 700f, 0.6f, 120f, 0.75f, 10f));
                case WeaponKind.Smg: return Get("shot_smg", () => Burst(0.13f, 1200f, 0.5f, 220f, 0.4f, 28f));
                case WeaponKind.Shotgun: return Get("shot_shotgun", () => Burst(0.5f, 500f, 0.75f, 90f, 0.85f, 8f));
                case WeaponKind.AssaultRifle: return Get("shot_rifle", () => Burst(0.25f, 1000f, 0.55f, 150f, 0.6f, 16f));
                case WeaponKind.SniperRifle: return Get("shot_sniper", () => Burst(0.7f, 800f, 0.6f, 70f, 0.95f, 6f));
                default: return Swing();
            }
        }

        /// <summary>Bombeo de escopeta / cerrojo del francotirador: dos chasquidos metálicos.</summary>
        public static AudioClip Action(bool pump) => Get(pump ? "act_pump" : "act_bolt", () => Clicks(pump ? new[] { 0f, 0.16f } : new[] { 0f, 0.22f }, pump ? 1800f : 2600f, 0.4f));
        public static AudioClip Reload() => Get("reload", () => Clicks(new[] { 0f, 0.35f, 0.5f }, 2200f, 0.7f));
        public static AudioClip Empty() => Get("empty", () => Clicks(new[] { 0f }, 3000f, 0.12f));
        public static AudioClip Swing() => Get("swing", () => Whoosh(0.3f, 0.5f));
        public static AudioClip Step(int variant) => Get("step" + variant, () => Thud(0.12f, 60f + variant * 18f, 0.5f, variant));
        /// <summary>Un ciclo de respiración (inspira y espira), para reproducir en bucle con volumen según la fatiga.</summary>
        public static AudioClip Breath() => Get("breath", () => BreathCycle(1.6f));

        private static AudioClip Get(string name, System.Func<float[]> make)
        {
            if (Cache.TryGetValue(name, out var c) && c != null) return c;
            var data = make();
            c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            Cache[name] = c;
            return c;
        }

        /// <summary>Estallido: ruido filtrado con caída rápida + golpe grave.</summary>
        private static float[] Burst(float seconds, float noiseCut, float noiseVol, float thumpHz, float thumpVol, float decay)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var d = new float[n];
            var rng = new System.Random(Mathf.RoundToInt(noiseCut + thumpHz));
            float lp = 0f, a = Mathf.Clamp01(noiseCut / Rate * 6f), phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * a;
                phase += 2f * Mathf.PI * thumpHz * Mathf.Lerp(1f, 0.4f, t / seconds) / Rate;
                float env = Mathf.Exp(-decay * t) * Mathf.Min(1f, i / 30f);
                d[i] = (lp * 2f * noiseVol + Mathf.Sin(phase) * thumpVol * Mathf.Exp(-decay * 0.6f * t)) * env * 0.8f;
            }
            return d;
        }

        private static float[] Clicks(float[] at, float hz, float seconds)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var d = new float[n];
            var rng = new System.Random(Mathf.RoundToInt(hz));
            foreach (float start in at)
            {
                int s = Mathf.RoundToInt(start * Rate);
                for (int i = 0; i < 0.04f * Rate && s + i < n; i++)
                {
                    float t = i / (float)Rate;
                    d[s + i] += (Mathf.Sin(2f * Mathf.PI * hz * t) * 0.5f + ((float)rng.NextDouble() * 2f - 1f) * 0.4f) * Mathf.Exp(-90f * t) * 0.6f;
                }
            }
            return d;
        }

        private static float[] Whoosh(float seconds, float vol)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var d = new float[n];
            var rng = new System.Random(7);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * Mathf.Lerp(0.05f, 0.3f, Mathf.Sin(t * Mathf.PI));
                d[i] = lp * vol * Mathf.Sin(t * Mathf.PI) * 2f;
            }
            return d;
        }

        private static float[] Thud(float seconds, float hz, float vol, int seed)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var d = new float[n];
            var rng = new System.Random(100 + seed);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * 0.15f;
                d[i] = (Mathf.Sin(2f * Mathf.PI * hz * t) * 0.6f + lp * 0.8f) * Mathf.Exp(-35f * t) * vol;
            }
            return d;
        }

        private static float[] BreathCycle(float seconds)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var d = new float[n];
            var rng = new System.Random(3);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * 0.08f;
                // inspira (más corta y aguda) y espira (más larga y grave), con silencio suave entre ambas: el bucle no "salta"
                float env = t < 0.4f ? Mathf.Sin(t / 0.4f * Mathf.PI) * 0.7f : (t < 0.9f ? Mathf.Sin((t - 0.4f) / 0.5f * Mathf.PI) : 0f);
                d[i] = lp * env * 0.9f;
            }
            return d;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();
    }
}
