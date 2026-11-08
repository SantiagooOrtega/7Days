using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Capa de daño progresivo (pura): al cruzar el 75, 50 y 25 % de vida se revela una herida en la zona que recibió el golpe (cabeza, torso, brazos o piernas, las mismas
    /// zonas de los hitbox). Cada zona tiene 3 etapas; si el golpe cruza varios cuartos a la vez se revelan varias etapas seguidas. Sin tabla de daño propia: solo mira la vida.
    /// </summary>
    public sealed class ZombieDamageLayer
    {
        public static readonly float[] Thresholds = { 0.75f, 0.5f, 0.25f };
        private int _stagesShown;                    // cuartos ya cruzados (0..3)
        private readonly int[] _zoneStage = new int[4];

        public int QuartersLost => _stagesShown;
        public int StageOf(HitZone zone) => _zoneStage[Slot(zone)];

        private static int Slot(HitZone z) => z == HitZone.WeakPoint ? (int)HitZone.Torso : (int)z;

        public void Reset() { _stagesShown = 0; Array.Clear(_zoneStage, 0, _zoneStage.Length); }

        /// <summary>Devuelve las heridas nuevas (zona, etapa 1..3) que deben aparecer con esta vida y esta zona golpeada.</summary>
        public void Update(float healthFraction, HitZone hitZone, List<KeyValuePair<HitZone, int>> revealed)
        {
            revealed.Clear();
            int crossed = 0;
            for (int i = 0; i < Thresholds.Length; i++) if (healthFraction <= Thresholds[i] + 1e-4f) crossed = i + 1;
            while (_stagesShown < crossed)
            {
                _stagesShown++;
                int slot = Slot(hitZone);
                if (_zoneStage[slot] >= 3) { slot = NextFreeZone(slot); if (slot < 0) continue; }
                _zoneStage[slot]++;
                revealed.Add(new KeyValuePair<HitZone, int>((HitZone)slot, _zoneStage[slot]));
            }
        }

        private int NextFreeZone(int from)
        {
            for (int k = 1; k <= 4; k++) { int s = (from + k) % 4; if (_zoneStage[s] < 3) return s; }
            return -1;
        }
    }
}
