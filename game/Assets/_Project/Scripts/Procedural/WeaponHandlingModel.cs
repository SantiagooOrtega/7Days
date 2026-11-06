using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Retroceso acumulado de cámara (grados): cada disparo sube el pitch y desvía el yaw de forma aleatoria pero acotada; al soltar el gatillo se recupera hacia 0.
    /// Menor al apuntar y agachado. Pitch positivo = la mira sube.
    /// </summary>
    public class RecoilState
    {
        public float Pitch { get; private set; }
        public float Yaw { get; private set; }
        public const float RecoveryDelay = 0.06f;
        private float _sinceShot = 10f;

        public static float Multiplier(WeaponData w, bool aiming, bool crouched) => (aiming ? w.recoilAimMultiplier : 1f) * (crouched ? w.recoilCrouchMultiplier : 1f);

        /// <param name="rand01">número aleatorio en [0,1) para la desviación lateral (el llamador pone el generador)</param>
        public void OnShot(WeaponData w, bool aiming, bool crouched, double rand01)
        {
            float m = Multiplier(w, aiming, crouched);
            Pitch = Math.Min(w.recoilMaxPitch, Pitch + w.recoil * m);
            float maxYaw = w.recoilMaxPitch * 0.5f;
            // la desviación lateral arrastra un poco la anterior (patrón que serpentea) y nunca pasa de la mitad del tope vertical
            float lateral = (float)(rand01 * 2.0 - 1.0) * w.recoilHorizontal * m;
            Yaw = Clamp(Yaw * 0.85f + lateral, -maxYaw, maxYaw);
            _sinceShot = 0f;
        }

        /// <summary>Se recupera si se soltó el gatillo (o si pasó más de 1.5 intervalos sin disparar, p. ej. en semiautomática).</summary>
        public void Tick(float dt, bool triggerHeld, WeaponData w)
        {
            _sinceShot += dt;
            if (triggerHeld && _sinceShot < w.SecondsBetweenShots * 1.5f) return;
            if (_sinceShot < RecoveryDelay) return;
            float step = w.recoilRecovery * dt;
            Pitch = MoveTowards(Pitch, 0f, step);
            Yaw = MoveTowards(Yaw, 0f, step * 0.7f);
        }

        public void Reset() { Pitch = Yaw = 0f; _sinceShot = 10f; }

        internal static float MoveTowards(float v, float target, float step) => v > target ? Math.Max(target, v - step) : Math.Min(target, v + step);
        internal static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }

    /// <summary>Dispersión (grados): base según apuntado y postura, más lo que crece al disparar en ráfaga (bloom), al moverse y en el aire; se recupera sola.</summary>
    public class SpreadState
    {
        public float Bloom { get; private set; }

        public static float Base(WeaponData w, bool aiming, bool crouched) => (aiming ? w.spreadAim : w.spreadHip) * (crouched ? w.spreadCrouchMultiplier : 1f);

        /// <param name="speed01">velocidad horizontal relativa (0 quieto, 1 corriendo)</param>
        public float Current(WeaponData w, bool aiming, bool crouched, float speed01, bool airborne)
        {
            float move = w.spreadMove * RecoilState.Clamp(speed01, 0f, 1f) * (aiming ? 0.5f : 1f);
            float v = Base(w, aiming, crouched) + Bloom + move + (airborne ? w.spreadAir : 0f);
            return Math.Min(Math.Max(w.spreadMax, Base(w, aiming, crouched)), v);
        }

        public void OnShot(WeaponData w, bool aiming) => Bloom = Math.Min(w.spreadMax, Bloom + w.spreadPerShot * (aiming ? 0.5f : 1f));
        public void Tick(float dt, WeaponData w) => Bloom = Math.Max(0f, Bloom - w.spreadRecovery * dt);
        public void Reset() => Bloom = 0f;
    }

    /// <summary>
    /// Modos de disparo: automático (mantener), semiautomático / bombeo / cerrojo (un disparo por clic), ráfaga (N disparos por clic con pausa al final).
    /// Recuerda un clic hecho un poco antes de que el arma esté lista (buffer de 0.15 s) para que el semiautomático no "se coma" pulsaciones.
    /// </summary>
    public class FireControl
    {
        public const float InputBuffer = 0.15f;
        public string Mode { get; private set; } = FireModes.Semi;
        public int BurstLeft { get; private set; }
        public float NextShot { get; private set; }
        private float _pressedAt = -10f;

        public void SetWeapon(WeaponData w) { Mode = w.DefaultFireMode; BurstLeft = 0; _pressedAt = -10f; }

        /// <summary>Pasa al siguiente modo que permita el arma (tecla X). Devuelve true si cambió.</summary>
        public bool Cycle(WeaponData w)
        {
            if (w.fireModes == null || w.fireModes.Length < 2) return false;
            int i = Array.IndexOf(w.fireModes, Mode);
            Mode = w.fireModes[(i + 1) % w.fireModes.Length];
            BurstLeft = 0;
            return true;
        }

        /// <summary>¿Quiere disparar ahora? (no consume nada: el llamador comprueba munición y recarga y después llama a Commit).</summary>
        public bool Wants(float time, bool pressed, bool held)
        {
            if (pressed) _pressedAt = time;
            if (time < NextShot) return false;
            if (BurstLeft > 0) return true;
            if (Mode == FireModes.Auto) return held;
            return time - _pressedAt <= InputBuffer;
        }

        public void Commit(float time, WeaponData w)
        {
            float interval = w.SecondsBetweenShots;
            _pressedAt = -10f;
            if (Mode == FireModes.Burst)
            {
                if (BurstLeft == 0) BurstLeft = w.burstCount;
                BurstLeft--;
                NextShot = time + (BurstLeft > 0 ? interval : interval * 3f);   // pausa al terminar la ráfaga
                return;
            }
            NextShot = time + interval;
        }

        /// <summary>Corta una ráfaga (recarga, cambio de arma, sin munición) y espera "delay" antes del siguiente disparo.</summary>
        public void Cancel(float time, float delay = 0f) { BurstLeft = 0; NextShot = Math.Max(NextShot, time + delay); }
    }
}
