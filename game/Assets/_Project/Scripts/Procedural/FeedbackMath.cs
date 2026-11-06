using System;

namespace SevenDays.Procedural
{
    /// <summary>Reglas puras de la barra de vida sobre el zombi y de los números flotantes (probadas en EditMode).</summary>
    public static class FeedbackMath
    {
        public const float BarVisibleAfterDamage = 4f, BarVisibleDistance = 8f;

        /// <summary>La barra se ve si el zombi recibió daño hace poco o está cerca del jugador; nunca si está muerto.</summary>
        public static bool BarVisible(float secondsSinceDamage, float distanceToPlayer, bool dead, bool full)
        {
            if (dead) return false;
            if (secondsSinceDamage <= BarVisibleAfterDamage) return true;
            return distanceToPlayer <= (full ? BarVisibleDistance * 0.5f : BarVisibleDistance);
        }

        /// <summary>Verde (lleno) a amarillo (mitad) a rojo (vacío).</summary>
        public static void BarColor(float fraction, out float r, out float g, out float b)
        {
            float f = Math.Max(0f, Math.Min(1f, fraction));
            if (f >= 0.5f) { float t = (f - 0.5f) * 2f; r = 1f - 0.8f * t; g = 0.85f; b = 0.15f; }
            else { float t = f * 2f; r = 0.9f; g = 0.15f + 0.7f * t; b = 0.1f; }
        }

        /// <summary>Suavizado exponencial hacia el valor real, independiente de los FPS.</summary>
        public static float Smooth(float shown, float target, float dt, float speed = 8f)
            => shown + (target - shown) * (1f - (float)Math.Exp(-speed * Math.Max(0f, dt)));

        public enum NumberKind { Body, Head, Reduced }

        /// <summary>Tipo del número flotante: cabeza = amarillo, daño reducido (resistencia, armadura, zona dura) = gris, resto blanco.</summary>
        public static NumberKind NumberFor(HitReport r) => r.Headshot ? NumberKind.Head : (r.Reduced && r.FinalDamage < r.BaseDamage * 0.75f ? NumberKind.Reduced : NumberKind.Body);
    }
}
