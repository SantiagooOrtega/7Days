using System;

namespace SevenDays.Procedural
{
    public enum PopDirection { None, Over, Left, Right }

    /// <summary>
    /// Pop-out estilo Gears of War (lógica pura): por dónde se asoma el jugador desde una cobertura. Cobertura baja: por ENCIMA. Cobertura alta o en el borde: por el LADO
    /// del hombro activo (Q). Si el lado elegido no sirve prueba el otro, y si no hay ninguno solo queda el disparo a ciegas.
    /// </summary>
    public static class PopOutRules
    {
        /// <param name="coverTop">altura del borde superior de la cobertura sobre los pies del jugador</param>
        /// <param name="shoulderSide">+1 derecho, -1 izquierdo (hombro activo)</param>
        /// <param name="edgeLeft">el jugador está junto al extremo izquierdo de la cobertura (se puede salir por ese lado)</param>
        /// <param name="overClear">hay espacio libre por encima de la cobertura</param>
        /// <param name="leftClear">hay espacio libre a la izquierda (sin otro muro pegado)</param>
        public static PopDirection Choose(float coverTop, float lowCoverMax, int shoulderSide, bool overClear, bool edgeLeft, bool edgeRight, bool leftClear, bool rightClear)
        {
            bool low = coverTop <= lowCoverMax;
            bool left = edgeLeft && leftClear, right = edgeRight && rightClear;
            PopDirection preferred = shoulderSide >= 0 ? (right ? PopDirection.Right : PopDirection.None) : (left ? PopDirection.Left : PopDirection.None);
            PopDirection other = shoulderSide >= 0 ? (left ? PopDirection.Left : PopDirection.None) : (right ? PopDirection.Right : PopDirection.None);

            if (low)
            {
                if (overClear) return PopDirection.Over;
                return preferred != PopDirection.None ? preferred : other;
            }
            if (preferred != PopDirection.None) return preferred;
            if (other != PopDirection.None) return other;
            // muy alta y sin borde cerca: solo se podría ver por encima si el jugador de pie (≈1.8 m) la supera
            return overClear && coverTop < 1.8f ? PopDirection.Over : PopDirection.None;
        }

        public static int Sign(PopDirection d) => d == PopDirection.Left ? -1 : (d == PopDirection.Right ? 1 : 0);
    }

    public enum CoverState { Out, Covered, PoppedOut, BlindFiring }

    /// <summary>
    /// Exposición de 0 (cubierto) a 1 (asomado del todo): sube y baja con ease en 0.15–0.25 s. A ciegas se queda en una exposición parcial unos instantes tras cada disparo.
    /// Acumula el tiempo cubierto, asomado y a ciegas para el director (regla de "abuso de cobertura").
    /// </summary>
    public class ExposureState
    {
        public float Raw { get; private set; }
        public float Value => CameraMath.EaseInOut(Raw);
        public float CoveredSeconds { get; private set; }
        public float PoppedSeconds { get; private set; }
        public float BlindSeconds { get; private set; }
        public CoverState State { get; private set; } = CoverState.Out;
        private float _blindUntil = -1f, _time;

        /// <summary>Un disparo a ciegas: exposición parcial durante "hold" segundos.</summary>
        public void OnBlindShot(float hold) { _blindUntil = _time + hold; }
        public bool BlindActive => _time < _blindUntil;

        public void Tick(float dt, bool inCover, bool wantsPop, bool canPop, float blindExposure, float seconds)
        {
            _time += dt;
            if (!inCover) { Raw = 0f; State = CoverState.Out; _blindUntil = -1f; return; }

            float target;
            bool popping = wantsPop && canPop;
            if (popping) target = 1f;
            else if (BlindActive) target = blindExposure;
            else target = 0f;
            float step = dt / Math.Max(0.01f, seconds);
            Raw = Raw < target ? Math.Min(target, Raw + step) : Math.Max(target, Raw - step);

            State = popping && Value > 0.5f ? CoverState.PoppedOut : (BlindActive && !popping ? CoverState.BlindFiring : (Value > 0.5f ? CoverState.PoppedOut : CoverState.Covered));
            switch (State)
            {
                case CoverState.Covered: CoveredSeconds += dt; break;
                case CoverState.PoppedOut: PoppedSeconds += dt; break;
                case CoverState.BlindFiring: BlindSeconds += dt; break;
            }
        }

        /// <summary>Se puede disparar apuntado cuando ya está suficientemente asomado.</summary>
        public bool CanShootAimed => Value >= 0.6f;
        public bool IsPoppedOut => State == CoverState.PoppedOut;
        public void Reset() { Raw = 0f; State = CoverState.Out; _blindUntil = -1f; }
        public void ResetStats() { CoveredSeconds = PoppedSeconds = BlindSeconds = 0f; }
    }

    public static class CoverProtection
    {
        /// <summary>Fracción del daño cuerpo a cuerpo que llega: el suelo (cubierto del todo) hasta 1 (asomado).</summary>
        public static float DamageMultiplier(float exposure, float floor) => CameraMath.Lerp(floor, 1f, exposure);

        /// <summary>El proyectil directo que viene de frente lo absorbe la cobertura mientras se está cubierto (exposición baja).</summary>
        public static bool AbsorbsProjectile(float exposure) => exposure < 0.5f;

        /// <summary>Alcance al que un zombi ve al jugador: cubierto solo de cerca, asomado hasta su rango de detección.</summary>
        public static float VisibleRange(float exposure, float aggroRange, float coveredRange = 6f) => CameraMath.Lerp(Math.Min(coveredRange, aggroRange), aggroRange, exposure);
    }

    public enum CoverShot { Normal, PopAndFire, Blind }

    /// <summary>
    /// Qué hace el disparo estando en cobertura: el clic izquierdo asoma al personaje solo lo justo y dispara desde ahí (PopAndFire); a ciegas solo con el
    /// modificador (Ctrl) o si de verdad no hay hueco por donde asomarse. Fuera de cobertura, o con arma cuerpo a cuerpo, disparo normal.
    /// </summary>
    public static class CoverFireRules
    {
        public static CoverShot Decide(bool inCover, bool melee, bool blindModifier, PopDirection pop)
        {
            if (!inCover || melee) return CoverShot.Normal;
            if (blindModifier || pop == PopDirection.None) return CoverShot.Blind;
            return CoverShot.PopAndFire;
        }

        /// <summary>Sigue asomado mientras se dispara y "hold" segundos después de soltar el gatillo (o mientras se apunta).</summary>
        public static bool WantsPop(bool aiming, bool triggerHeld, float time, float lastTriggerTime, float hold)
            => aiming || triggerHeld || time - lastTriggerTime < hold;
    }

    public static class BlindFire
    {
        public static float Spread(WeaponData w, CameraSettingsData c) => CameraMath.Clamp(w.spreadHip * c.blindSpreadMultiplier, c.blindMinSpread, c.blindMaxSpread);
        public static float Damage(float damage, CameraSettingsData c) => damage * c.blindDamageMultiplier;
    }

    public static class ShotOrigin
    {
        /// <summary>
        /// Si el punto está dentro de la caja de la cobertura (con margen), lo saca por la salida más corta entre las permitidas: "up" por encima, "side" (+1/-1) por
        /// el lado a lo largo de "tangent" (eje X o Z de la caja). Si el punto ya está libre lo devuelve igual. Devuelve true si hubo desplazamiento.
        /// </summary>
        public static bool PushOut(float px, float py, float pz, float minX, float minY, float minZ, float maxX, float maxY, float maxZ, float margin,
            PopDirection prefer, out float ox, out float oy, out float oz)
        {
            ox = px; oy = py; oz = pz;
            bool inside = px > minX - margin && px < maxX + margin && py > minY - margin && py < maxY + margin && pz > minZ - margin && pz < maxZ + margin;
            if (!inside) return false;

            float up = maxY + margin - py;
            float left = px - (minX - margin), right = (maxX + margin) - px;     // salir por -X / +X
            float back = pz - (minZ - margin), front = (maxZ + margin) - pz;     // salir por -Z / +Z
            float best = float.MaxValue; int axis = 0;
            void Consider(float d, int a, bool allowed) { if (allowed && d < best) { best = d; axis = a; } }

            Consider(up, 1, prefer != PopDirection.Left && prefer != PopDirection.Right);
            bool side = prefer == PopDirection.Left || prefer == PopDirection.Right || prefer == PopDirection.None;
            Consider(left, 2, side); Consider(right, 3, side); Consider(back, 4, side); Consider(front, 5, side);
            if (best == float.MaxValue) axis = 1;   // sin salida permitida: por arriba

            switch (axis)
            {
                case 1: oy = maxY + margin; break;
                case 2: ox = minX - margin; break;
                case 3: ox = maxX + margin; break;
                case 4: oz = minZ - margin; break;
                case 5: oz = maxZ + margin; break;
            }
            return true;
        }
    }

    public static class ShotGeometry
    {
        /// <summary>¿El segmento A→B atraviesa la caja (min..max)? Método de las franjas. Sirve para comprobar que el disparo asomado pasa por encima del muro.</summary>
        public static bool SegmentHitsBox(float ax, float ay, float az, float bx, float by, float bz, float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            float t0 = 0f, t1 = 1f;
            if (!Slab(ax, bx - ax, minX, maxX, ref t0, ref t1)) return false;
            if (!Slab(ay, by - ay, minY, maxY, ref t0, ref t1)) return false;
            if (!Slab(az, bz - az, minZ, maxZ, ref t0, ref t1)) return false;
            return t0 <= t1;
        }

        private static bool Slab(float a, float d, float min, float max, ref float t0, ref float t1)
        {
            if (Math.Abs(d) < 1e-7f) return a >= min && a <= max;
            float ta = (min - a) / d, tb = (max - a) / d;
            if (ta > tb) { float t = ta; ta = tb; tb = t; }
            t0 = Math.Max(t0, ta); t1 = Math.Min(t1, tb);
            return t0 <= t1;
        }
    }

    /// <summary>Regla de "abuso de cobertura" para el director: mucho tiempo cubierto y casi nada asomado ni disparando.</summary>
    public static class CoverAbuse
    {
        public static float ActiveRatio(float covered, float popped, float blind)
        {
            float total = covered + popped + blind;
            return total <= 0f ? 0f : (popped + blind * 0.5f) / total;
        }

        /// <summary>true si en una ventana de al menos "minSeconds" en cobertura el jugador pasó más del 85 % escondido.</summary>
        public static bool IsAbusing(float covered, float popped, float blind, float minSeconds = 20f)
            => covered + popped + blind >= minSeconds && covered / (covered + popped + blind) > 0.85f;
    }
}
