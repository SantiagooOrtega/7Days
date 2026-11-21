using System;

namespace SevenDays.Procedural
{
    public enum MusicPhase { Menu, Intro, Game, Dead, Victory }

    /// <summary>
    /// Volumen de la música por fase (puro, sin Unity). Los niveles son relativos al volumen del grupo "Música" (menú Esc):
    /// alto en carga, login y menú; en la intro alto, y ~40 % mientras habla VIGÍA; en partida ~18 %, que sube a ~40 % con jefe o combate intenso y baja suave
    /// mientras habla VIGÍA (ducking). Entrar al Día 1 y morir funden en 1.5 s; el ducking es más corto (0.6 s).
    /// </summary>
    public static class MusicMix
    {
        public const float High = 1f, Talk = 0.4f, Game = 0.18f, Tense = 0.4f, Dead = 0.3f, Victory = 0.8f;
        public const float DuckFactor = 0.6f;          // en partida, mientras habla VIGÍA
        public const float FadeSeconds = 1.5f, DuckSeconds = 0.6f;
        public const float CrossfadeBelow = 60f, CrossfadeSeconds = 2f;

        public static float Target(MusicPhase phase, bool vigiaSpeaking, bool tense)
        {
            switch (phase)
            {
                case MusicPhase.Intro: return vigiaSpeaking ? Talk : High;
                case MusicPhase.Game:
                {
                    float v = tense ? Tense : Game;
                    return vigiaSpeaking ? v * DuckFactor : v;
                }
                case MusicPhase.Dead: return Dead;
                case MusicPhase.Victory: return vigiaSpeaking ? Talk : Victory;
                default: return vigiaSpeaking ? Talk : High;
            }
        }

        /// <summary>Segundos del fundido al pasar de una fase a otra (o del ducking dentro de la misma fase).</summary>
        public static float TransitionSeconds(MusicPhase from, MusicPhase to) => from != to ? FadeSeconds : DuckSeconds;

        /// <summary>Un paso del fundido lineal: llega de "from" a "to" en "seconds" (la velocidad se fija al empezar el cambio).</summary>
        public static float Step(float current, float target, float rate, float dt)
        {
            float d = target - current, max = rate * dt;
            return Math.Abs(d) <= max ? target : current + Math.Sign(d) * max;
        }

        public static float Rate(float from, float to, float seconds) => Math.Max(0.02f, Math.Abs(to - from) / Math.Max(0.05f, seconds));

        /// <summary>Clip corto: dos fuentes que se cruzan antes de terminar en vez del bucle simple (evita el corte audible).</summary>
        public static bool NeedsCrossfade(float clipSeconds) => clipSeconds > 0f && clipSeconds < CrossfadeBelow;

        /// <summary>Pesos de las dos fuentes durante el cruce (t de 0 a 1): potencia constante, sin bajón en el centro.</summary>
        public static void CrossfadeGains(float t, out float outgoing, out float incoming)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            outgoing = (float)Math.Cos(t * Math.PI * 0.5);
            incoming = (float)Math.Sin(t * Math.PI * 0.5);
        }
    }
}
