using System;

namespace SevenDays.Procedural
{
    /// <summary>Planta rectangular (alineada a los ejes) de un edificio, en el plano XZ. La casa gira en múltiplos de 90°, así que sigue siendo un rectángulo.</summary>
    public readonly struct Footprint
    {
        public readonly float Cx, Cz, Hx, Hz;
        public Footprint(float cx, float cz, float hx, float hz) { Cx = cx; Cz = cz; Hx = hx; Hz = hz; }

        public static Footprint FromHouse(float x, float z, float width, float depth, float yawDegrees, float margin = 0f)
        {
            bool swap = Math.Abs(Math.Round(yawDegrees / 90.0)) % 2 == 1;
            float w = swap ? depth : width, d = swap ? width : depth;
            return new Footprint(x, z, w * 0.5f + margin, d * 0.5f + margin);
        }

        public bool Contains(float x, float z, float margin = 0f) => Math.Abs(x - Cx) < Hx + margin && Math.Abs(z - Cz) < Hz + margin;

        /// <summary>Si el punto está dentro (con margen), lo saca por el borde más cercano; si no, lo devuelve igual.</summary>
        public void PushOut(float x, float z, float margin, out float ox, out float oz)
        {
            ox = x; oz = z;
            float hx = Hx + margin, hz = Hz + margin;
            float dx = x - Cx, dz = z - Cz;
            if (Math.Abs(dx) >= hx || Math.Abs(dz) >= hz) return;
            float toX = hx - Math.Abs(dx), toZ = hz - Math.Abs(dz);
            if (toX < toZ) ox = Cx + (dx >= 0f ? hx : -hx);
            else oz = Cz + (dz >= 0f ? hz : -hz);
        }

        /// <summary>Punto del perímetro (a "offset" metros del muro) para rodear el edificio: 8 puntos (4 esquinas y 4 mitades).</summary>
        public void PerimeterPoint(int index, float offset, out float px, out float pz)
        {
            int i = ((index % 8) + 8) % 8;
            float hx = Hx + offset, hz = Hz + offset;
            float[] sx = { -1, 0, 1, 1, 1, 0, -1, -1 };
            float[] sz = { -1, -1, -1, 0, 1, 1, 1, 0 };
            px = Cx + sx[i] * hx; pz = Cz + sz[i] * hz;
        }

        /// <summary>Índice del punto del perímetro más cercano a una posición.</summary>
        public int NearestPerimeter(float x, float z, float offset)
        {
            int best = 0; float bestD = float.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                PerimeterPoint(i, offset, out float px, out float pz);
                float d = (px - x) * (px - x) + (pz - z) * (pz - z);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }
    }

    public enum SiegeAction { Approach, Pound, Flank, Wait }

    /// <summary>
    /// Qué hace un zombi gigante cuando el jugador está dentro de un edificio al que no puede entrar: se acerca a lo más cercano que alcanza, golpea el muro/puerta
    /// cuando está a su alcance, y si pasa tiempo parado sin alcanzarlo rodea el edificio. Con histéresis: no alterna entre estados en el borde (nada de vibrar).
    /// </summary>
    public class SiegeBrain
    {
        public float Reach = 3.5f, ReachExit = 1.35f, FlankAfterSeconds = 4f, MinFlankHold = 3f;
        public SiegeAction Action { get; private set; } = SiegeAction.Approach;
        public int FlankIndex { get; private set; }
        public float WaitTime { get; private set; }
        public float FlankHold { get; private set; }

        public void Reset(int startIndex = 0) { Action = SiegeAction.Approach; FlankIndex = startIndex; WaitTime = 0f; FlankHold = 0f; }

        /// <param name="distance">distancia en el plano al jugador</param>
        /// <param name="atGoal">ya llegó (o no puede avanzar más) hacia su destino actual</param>
        public SiegeAction Tick(float dt, float distance, bool atGoal)
        {
            float reach = Action == SiegeAction.Pound ? Reach * ReachExit : Reach;   // para dejar de golpear hay que alejarse más de lo que hizo falta para empezar
            if (distance <= reach) { Action = SiegeAction.Pound; WaitTime = 0f; FlankHold = 0f; return Action; }

            if (Action == SiegeAction.Flank)
            {
                FlankHold += dt;
                if (!atGoal || FlankHold < MinFlankHold) return Action;   // mantiene el rodeo hasta llegar y quedarse un rato
                Action = SiegeAction.Wait; WaitTime = 0f;
                return Action;
            }
            if (!atGoal) { Action = SiegeAction.Approach; WaitTime = 0f; return Action; }

            WaitTime += dt;
            if (WaitTime >= FlankAfterSeconds)
            {
                FlankIndex++; WaitTime = 0f; FlankHold = 0f;
                Action = SiegeAction.Flank;
                return Action;
            }
            Action = SiegeAction.Wait;
            return Action;
        }
    }
}
