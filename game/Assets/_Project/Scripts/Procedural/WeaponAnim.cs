using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Cómo se sostiene cada tipo de arma: posición y giro del arma (relativos al soporte junto al hombro derecho) en cadera, corriendo y en cobertura, y dónde van las manos.</summary>
    public struct WeaponHold
    {
        public V3 HipPos, HipRot;            // metros / grados
        public V3 SprintPos, SprintRot;      // arma abajo e inclinada
        public V3 CoverPos, CoverRot;        // pegada al pecho
        public bool TwoHandsHip;             // a la cadera con las dos manos (rifles) o con una (pistola)
        public bool TwoHandsAim;             // apuntando con las dos manos
        public float LeftGripFraction;       // dónde va la mano izquierda a lo largo del arma (0 empuñadura .. 1 boca)
        public bool OneHandedMelee;
    }

    public static class WeaponHolds
    {
        public static WeaponHold For(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Pistol:
                case WeaponKind.Revolver:
                    return new WeaponHold
                    {
                        HipPos = new V3(0.02f, -0.12f, 0.02f), HipRot = new V3(8f, 0f, 0f),
                        SprintPos = new V3(0.04f, -0.32f, -0.08f), SprintRot = new V3(70f, 0f, 0f),
                        CoverPos = new V3(-0.14f, -0.08f, 0.02f), CoverRot = new V3(15f, -4f, 0f),
                        TwoHandsHip = false, TwoHandsAim = true, LeftGripFraction = 0.1f,
                    };
                case WeaponKind.Smg:
                    return new WeaponHold
                    {
                        HipPos = new V3(-0.06f, -0.04f, 0f), HipRot = new V3(0f, 0f, 0f),
                        SprintPos = new V3(-0.02f, -0.2f, -0.06f), SprintRot = new V3(38f, -35f, 12f),
                        CoverPos = new V3(-0.12f, -0.05f, -0.02f), CoverRot = new V3(12f, -6f, 0f),
                        TwoHandsHip = true, TwoHandsAim = true, LeftGripFraction = 0.55f,
                    };
                case WeaponKind.Melee:
                    return new WeaponHold
                    {
                        HipPos = new V3(0.04f, -0.2f, -0.05f), HipRot = new V3(-55f, 0f, 0f),   // barra apoyada hacia arriba
                        SprintPos = new V3(0.06f, -0.3f, -0.1f), SprintRot = new V3(-25f, 0f, 0f),
                        CoverPos = new V3(-0.05f, -0.1f, -0.12f), CoverRot = new V3(-70f, -15f, 0f),
                        TwoHandsHip = false, TwoHandsAim = false, LeftGripFraction = 0f, OneHandedMelee = true,
                    };
                default:   // fusil, escopeta, francotirador: al hombro
                    return new WeaponHold
                    {
                        HipPos = new V3(-0.08f, 0.0f, -0.06f), HipRot = new V3(0f, 0f, 0f),
                        SprintPos = new V3(-0.04f, -0.22f, -0.1f), SprintRot = new V3(35f, -40f, 15f),
                        CoverPos = new V3(-0.12f, -0.04f, -0.04f), CoverRot = new V3(12f, -6f, 0f),
                        TwoHandsHip = true, TwoHandsAim = true, LeftGripFraction = 0.6f,
                    };
            }
        }
    }

    public enum ReloadStyle { Magazine, Shells, Cylinder, BoltMagazine, None }
    public enum ReloadEvent { MagOut, HandToBelt, MagIn, Slap, ShellIn, CylinderOpen, CylinderClose, BoltOpen, BoltClose, Done }

    public struct ReloadKey { public float Time; public ReloadEvent Event; }

    /// <summary>
    /// Recarga animada con eventos (pura): sincroniza la animación, el sonido y el momento en que se repone la munición. Cargador: sale, mano a la cintura, cargador
    /// nuevo (ahí se repone), golpe. Escopeta: cartucho a cartucho (+1 en cada uno; si se interrumpe se conservan los ya metidos). Revólver: abre el tambor, mete las
    /// balas, cierra. Francotirador: abre el cerrojo, cambia el cargador, cierra. Se cancela al cambiar de arma (como en el diseño actual).
    /// </summary>
    public class ReloadState
    {
        public bool Active { get; private set; }
        public ReloadStyle Style { get; private set; }
        public float Elapsed { get; private set; }
        public float Duration { get; private set; }
        public int ShellsToLoad { get; private set; }
        public int ShellsLoaded { get; private set; }
        public float ShellTime { get; private set; }
        public float Progress => Duration <= 0f ? 0f : Math.Min(1f, Elapsed / Duration);
        public ReloadEvent LastEvent { get; private set; } = ReloadEvent.Done;
        /// <summary>Tiempo desde el último evento (para la animación entre eventos).</summary>
        public float SinceEvent => Elapsed - _lastEventTime;

        private readonly List<ReloadKey> _keys = new List<ReloadKey>(8);
        private readonly List<ReloadEvent> _fired = new List<ReloadEvent>(4);
        private int _next;
        private float _lastEventTime;
        public const float ShellLead = 0.25f, ShellTail = 0.3f;

        public static ReloadStyle StyleFor(WeaponKind k) => k == WeaponKind.Shotgun ? ReloadStyle.Shells : k == WeaponKind.Revolver ? ReloadStyle.Cylinder
            : k == WeaponKind.SniperRifle ? ReloadStyle.BoltMagazine : k == WeaponKind.Melee ? ReloadStyle.None : ReloadStyle.Magazine;

        /// <param name="missing">balas que faltan en el cargador; <paramref name="reserve"/> las disponibles en la reserva</param>
        public bool Begin(WeaponData w, int missing, int reserve)
        {
            int toLoad = Math.Min(missing, reserve);
            Style = StyleFor(w.kind);
            if (Style == ReloadStyle.None || toLoad <= 0) return false;
            _keys.Clear(); _next = 0; Elapsed = 0f; _lastEventTime = 0f; ShellsLoaded = 0; ShellsToLoad = toLoad; LastEvent = ReloadEvent.Done;
            float T = w.reloadTime;
            switch (Style)
            {
                case ReloadStyle.Magazine:
                    Add(0.15f * T, ReloadEvent.MagOut); Add(0.35f * T, ReloadEvent.HandToBelt); Add(0.65f * T, ReloadEvent.MagIn); Add(0.82f * T, ReloadEvent.Slap); Add(T, ReloadEvent.Done);
                    Duration = T; break;
                case ReloadStyle.Cylinder:
                    Add(0.12f * T, ReloadEvent.CylinderOpen); Add(0.3f * T, ReloadEvent.HandToBelt); Add(0.62f * T, ReloadEvent.MagIn); Add(0.85f * T, ReloadEvent.CylinderClose); Add(T, ReloadEvent.Done);
                    Duration = T; break;
                case ReloadStyle.BoltMagazine:
                    Add(0.12f * T, ReloadEvent.BoltOpen); Add(0.28f * T, ReloadEvent.MagOut); Add(0.62f * T, ReloadEvent.MagIn); Add(0.85f * T, ReloadEvent.BoltClose); Add(T, ReloadEvent.Done);
                    Duration = T; break;
                case ReloadStyle.Shells:
                    // reloadTime es lo que dura cargar el cargador entero: cada cartucho, una parte
                    ShellTime = Math.Max(0.2f, (T - ShellLead - ShellTail) / Math.Max(1, w.magazineSize));
                    for (int i = 0; i < toLoad; i++) Add(ShellLead + ShellTime * (i + 1), ReloadEvent.ShellIn);
                    Duration = ShellLead + ShellTime * toLoad + ShellTail;
                    Add(Duration, ReloadEvent.Done);
                    break;
            }
            Active = true;
            return true;
        }

        private void Add(float t, ReloadEvent e) => _keys.Add(new ReloadKey { Time = t, Event = e });

        /// <summary>Avanza y devuelve los eventos que ocurrieron en este paso (lista reutilizada). "ammo" = balas que entran ahora en el cargador.</summary>
        public List<ReloadEvent> Tick(float dt, out int ammo)
        {
            _fired.Clear(); ammo = 0;
            if (!Active) return _fired;
            Elapsed += dt;
            while (_next < _keys.Count && _keys[_next].Time <= Elapsed)
            {
                var e = _keys[_next].Event;
                _fired.Add(e); LastEvent = e; _lastEventTime = _keys[_next].Time; _next++;
                if (e == ReloadEvent.MagIn) ammo += ShellsToLoad;
                if (e == ReloadEvent.ShellIn) { ammo += 1; ShellsLoaded++; }
                if (e == ReloadEvent.Done) { Active = false; break; }
            }
            return _fired;
        }

        /// <summary>Corta la recarga. Con cartuchos, los ya metidos se quedan (devuelve cuántos).</summary>
        public int Cancel() { Active = false; LastEvent = ReloadEvent.Done; return ShellsLoaded; }

        /// <summary>La escopeta puede interrumpir la recarga para disparar si ya tiene algún cartucho.</summary>
        public bool CanInterruptToFire => Active && Style == ReloadStyle.Shells && ShellsLoaded > 0;
    }

    /// <summary>
    /// Golpe cuerpo a cuerpo con anticipación y seguimiento (puro): preparación (el arma va atrás y arriba), golpe (trayectoria curva en diagonal) y seguimiento.
    /// El daño se aplica en el instante del impacto (mitad del golpe). Mantener el clic al menos HeavyHold segundos hace un golpe cargado: más preparación y más daño.
    /// </summary>
    public static class MeleeSwing
    {
        public const float HeavyHold = 0.45f, HeavyDamage = 1.75f, HeavyKnockback = 1.5f, HeavyCooldown = 1.6f;
        public static float Windup(bool heavy) => heavy ? 0.24f : 0.09f;
        public static float Strike(bool heavy) => heavy ? 0.13f : 0.1f;
        public static float Follow(bool heavy) => heavy ? 0.32f : 0.2f;
        public static float Total(bool heavy) => Windup(heavy) + Strike(heavy) + Follow(heavy);
        /// <summary>Instante (desde el inicio) en que el golpe toca: mitad de la fase de golpe.</summary>
        public static float ImpactTime(bool heavy) => Windup(heavy) + Strike(heavy) * 0.5f;
        public static bool IsHeavy(float holdSeconds) => holdSeconds >= HeavyHold;

        /// <summary>Ángulos del arma (pitch, yaw, roll en grados) en el instante t. "mirror" alterna el sentido (combo izquierda-derecha).</summary>
        public static V3 Pose(float t, bool heavy, bool mirror)
        {
            float w = Windup(heavy), s = Strike(heavy), f = Follow(heavy);
            float side = mirror ? -1f : 1f;
            V3 rest = new V3(0f, 0f, 0f);
            V3 back = new V3(-70f - (heavy ? 25f : 0f), 35f * side, 20f * side);   // atrás, arriba y al lado
            V3 hit = new V3(40f, -10f * side, -10f * side);                          // abajo, cruzando delante
            V3 through = new V3(75f, -45f * side, -25f * side);                      // sigue de largo
            if (t <= 0f) return rest;
            if (t < w) return Lerp(rest, back, CameraMath.EaseInOut(t / w));
            if (t < w + s) { float u = (t - w) / s; return Lerp(back, through, u * u * (3f - 2f * u) * 0.5f + u * 0.5f); }   // rápido y curvo
            if (t < w + s + f) return Lerp(through, rest, CameraMath.EaseInOut((t - w - s) / f));
            return rest;
        }

        private static V3 Lerp(V3 a, V3 b, float t) => a + (b - a) * t;
    }

    /// <summary>Sacar y guardar el arma al cambiar: 0 = guardada (abajo, a la cadera), 1 = en las manos.</summary>
    public class DrawState
    {
        public const float DrawSeconds = 0.35f;
        public float Value { get; private set; } = 1f;
        public void Holster() => Value = 0f;
        public void Tick(float dt) => Value = Math.Min(1f, Value + dt / DrawSeconds);
        public float Eased => CameraMath.EaseInOut(Value);
        public bool Ready => Value >= 0.7f;
    }
}
