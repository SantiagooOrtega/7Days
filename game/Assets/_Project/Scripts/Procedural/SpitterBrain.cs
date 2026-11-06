using System;

namespace SevenDays.Procedural
{
    public enum SpitterAction { Idle, Reposition, Flank, Aim, Throw }
    public enum ProjectileMode { Direct, Arc }

    /// <summary>
    /// Decisiones del zombi lanzador (puras, sin Unity): mantiene su distancia preferida, apunta con aviso, lanza y se reposiciona.
    /// Con línea de tiro libre lanza un proyectil directo; si algo lo tapa (el jugador está tras una cobertura) lanza en arco, que ignora la cobertura frontal;
    /// y si el jugador lleva demasiado tiempo cubierto busca un ángulo lateral (flanqueo).
    /// </summary>
    public class SpitterBrain
    {
        public float PreferredRange = 12f, Cooldown = 3.5f, AimSeconds = 0.9f, FlankAfterSeconds = 4f, ArcAfterCoverSeconds = 1f, MaxRangeFactor = 1.6f;
        public string Allowed = "mixed";   // direct | arc | mixed
        public ProjectileMode Mode { get; private set; }
        public float AimRemaining { get; private set; }
        public float CooldownRemaining { get; private set; }
        public bool Aiming => AimRemaining > 0f;

        public SpitterBrain() { CooldownRemaining = 1f; }

        public static SpitterBrain For(ZombieTypeData t) => new SpitterBrain { PreferredRange = t.preferredRange, Cooldown = t.projectileCooldown, FlankAfterSeconds = t.flankAfterSeconds, Allowed = t.projectile };

        /// <param name="hasLineOfFire">sin obstáculos ni coberturas entre el lanzador y el jugador</param>
        /// <param name="playerCoverSeconds">cuánto lleva el jugador seguido en cobertura</param>
        public SpitterAction Tick(float dt, float distance, bool hasLineOfFire, float playerCoverSeconds)
        {
            if (AimRemaining > 0f)
            {
                AimRemaining -= dt;
                if (AimRemaining > 0f) return SpitterAction.Aim;
                AimRemaining = 0f;
                CooldownRemaining = Cooldown;
                return SpitterAction.Throw;
            }
            CooldownRemaining = Math.Max(0f, CooldownRemaining - dt);

            if (playerCoverSeconds >= FlankAfterSeconds && !hasLineOfFire && Allowed != "arc") return SpitterAction.Flank;
            if (CooldownRemaining > 0f) return distance < PreferredRange * 0.6f || distance > PreferredRange * 1.2f ? SpitterAction.Reposition : SpitterAction.Idle;
            if (distance > PreferredRange * MaxRangeFactor || distance < PreferredRange * 0.5f) return SpitterAction.Reposition;

            ProjectileMode? mode = ChooseMode(hasLineOfFire, playerCoverSeconds);
            if (mode == null) return SpitterAction.Reposition;
            Mode = mode.Value;
            AimRemaining = AimSeconds;
            return SpitterAction.Aim;
        }

        private ProjectileMode? ChooseMode(bool hasLineOfFire, float playerCoverSeconds)
        {
            if (Allowed == "arc") return ProjectileMode.Arc;
            if (Allowed == "direct") return hasLineOfFire ? ProjectileMode.Direct : (ProjectileMode?)null;
            if (hasLineOfFire) return ProjectileMode.Direct;
            return playerCoverSeconds >= ArcAfterCoverSeconds ? ProjectileMode.Arc : (ProjectileMode?)null;
        }
    }

    /// <summary>Balística del proyectil en arco y daño de área.</summary>
    public static class Ballistics
    {
        public const float Gravity = 18f;

        /// <summary>Velocidad inicial para ir de A a B en "time" segundos con esa gravedad (g positiva hacia abajo).</summary>
        public static void LaunchVelocity(float ax, float ay, float az, float bx, float by, float bz, float time, float g, out float vx, out float vy, out float vz)
        {
            time = Math.Max(0.05f, time);
            vx = (bx - ax) / time;
            vz = (bz - az) / time;
            vy = (by - ay) / time + 0.5f * g * time;
        }

        /// <summary>Tiempo de vuelo del arco: proporcional a la distancia pero nunca menos de 1 s (el aviso en el suelo necesita 1–1.5 s).</summary>
        public static float FlightTime(float distance, float speed) => Math.Max(1.0f, Math.Min(1.5f, distance / Math.Max(1f, speed) * 1.3f));

        public static float PositionAt(float p0, float v0, float g, float t, bool vertical) => p0 + v0 * t - (vertical ? 0.5f * g * t * t : 0f);
    }

    public static class AreaDamage
    {
        /// <summary>Daño de área: máximo en el centro y cae linealmente hasta el 25 % en el borde; fuera del radio, 0.</summary>
        public static float At(float distance, float radius, float maxDamage)
        {
            if (radius <= 0f || distance >= radius) return 0f;
            float t = Math.Max(0f, distance) / radius;
            return maxDamage * (1f - 0.75f * t);
        }
    }

    /// <summary>Reglas de cobertura frente al proyectil directo (las usa Projectile y las prueba CombatTests).</summary>
    public static class CoverRules
    {
        /// <summary>El proyectil directo no daña al jugador si una cobertura lo frena antes; el arco nunca lo evita. Devuelve el daño que llega.</summary>
        public static float DamageAfterCover(ProjectileMode mode, float damage, bool coverBlocksPath) => mode == ProjectileMode.Arc || !coverBlocksPath ? damage : 0f;
    }
}
