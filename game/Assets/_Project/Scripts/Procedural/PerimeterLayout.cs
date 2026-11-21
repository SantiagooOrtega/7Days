using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    public enum PerimeterPiece { Sandbag, Plank, Concrete, FencePost, FencePanel, Log, Post, Wire, Cheval, Banner, Lamp, Barrel }

    /// <summary>Una pieza del perímetro en el mundo: centro, tamaño, giro (Y) e inclinación (X local, hacia fuera).</summary>
    public struct PerimeterPart
    {
        public PerimeterPiece Kind;
        public V3 Center, Size;
        public float Yaw, Tilt;
        public int Side;
    }

    /// <summary>
    /// Barrera del límite del mapa (pura): trinchera de sacos de arena y tablones con tramos de concreto roto y de cerca metálica rota (Calle) o cerca de troncos
    /// (Bosque), postes inclinados hacia fuera con alambre de espino encima, caballos de frisa fuera, banderines y algún foco. Ocupa la franja del muro invisible
    /// (de halfSize a halfSize + 1) para que nada se meta en la zona jugable, va por tramos de 3 m con variación y cierra las esquinas.
    /// El muro sigue siendo el collider (4-6 m): bloquea jugador, zombis y balas, y no se salta.
    /// </summary>
    public static class PerimeterLayout
    {
        public const float SegmentLength = 3f, Depth = 1f, ParapetHeight = 1.15f, WireTop = 2.0f, WireRadius = 0.28f;

        public static List<PerimeterPart> Build(float halfSize, string style, int seed)
        {
            var parts = new List<PerimeterPart>();
            var rng = new Random(seed);
            bool logs = style == "logs";
            float span = halfSize * 2f + Depth * 2f;   // de esquina exterior a esquina exterior: las esquinas quedan cerradas
            int segs = Math.Max(1, (int)Math.Ceiling(span / SegmentLength));
            float segLen = span / segs;
            for (int side = 0; side < 4; side++)
            {
                for (int i = 0; i < segs; i++)
                {
                    float t = -span / 2f + segLen * (i + 0.5f);
                    int kind = logs ? 3 : Pick(rng);
                    float yaw = side < 2 ? 0f : 90f;
                    void Add(PerimeterPiece k, float along, float y, float inward, V3 size, float tilt = 0f, float yawOff = 0f)
                        => parts.Add(new PerimeterPart { Kind = k, Center = Place(side, halfSize, t + along, y, inward), Size = size, Yaw = yaw + yawOff, Tilt = tilt, Side = side });
                    switch (kind)
                    {
                        case 1: // concreto roto
                            Add(PerimeterPiece.Concrete, 0f, 0.6f, 0.5f, new V3(segLen * 0.98f, 1.2f, 0.7f));
                            Add(PerimeterPiece.Concrete, (float)(rng.NextDouble() - 0.5) * segLen * 0.5f, 1.3f, 0.45f, new V3(segLen * 0.45f, 0.3f, 0.5f), (float)(rng.NextDouble() * 16 - 8), (float)(rng.NextDouble() * 20 - 10));
                            break;
                        case 2: // cerca metálica rota sobre una fila de sacos
                            Sandbags(parts, side, halfSize, t, segLen, 2, yaw);
                            Add(PerimeterPiece.FencePanel, 0f, 1.0f, 0.75f, new V3(segLen * 0.96f, 1.0f, 0.03f), (float)(rng.NextDouble() * 18 - 4));
                            break;
                        case 3: // troncos apilados (Bosque)
                            for (int r = 0; r < 3; r++) Add(PerimeterPiece.Log, (float)(rng.NextDouble() - 0.5) * 0.3f, 0.2f + r * 0.38f, 0.5f, new V3(segLen * 1.04f, 0.38f, 0.38f));
                            break;
                        default: // sacos de arena con tablones
                            Sandbags(parts, side, halfSize, t, segLen, 4, yaw);
                            Add(PerimeterPiece.Plank, 0f, 0.95f, 0.15f, new V3(segLen * 0.95f, 0.22f, 0.05f), (float)(rng.NextDouble() * 6 - 3));
                            break;
                    }
                    // postes inclinados hacia fuera y alambre de espino encima del parapeto
                    Add(PerimeterPiece.Post, -segLen / 2f + 0.1f, 1.05f, 0.75f, new V3(0.1f, 2.1f, 0.1f), 14f);
                    Add(PerimeterPiece.Wire, 0f, ParapetHeight + WireRadius + 0.25f, 0.8f, new V3(segLen, WireRadius * 2f, WireRadius * 2f));
                    double r1 = rng.NextDouble();
                    if (r1 < 0.12) Add(PerimeterPiece.Cheval, 0f, 0.55f, 1.9f, new V3(1.6f, 1.1f, 1.1f));
                    else if (r1 < 0.24) Add(PerimeterPiece.Banner, -segLen / 2f + 0.1f, 2.0f, 0.75f, new V3(0.5f, 0.32f, 0.01f), 0f, (float)(rng.NextDouble() * 30 - 15));
                    else if (r1 < 0.29 && !logs) Add(PerimeterPiece.Barrel, 0.6f, 0.45f, -0.35f, new V3(0.58f, 0.9f, 0.58f));
                    if (i % 7 == 3) Add(PerimeterPiece.Lamp, segLen * 0.3f, ParapetHeight + 0.2f, 0.35f, new V3(0.22f, 0.18f, 0.22f));
                }
            }
            return parts;
        }

        private static int Pick(Random rng) { double r = rng.NextDouble(); return r < 0.62 ? 0 : (r < 0.82 ? 1 : 2); }

        private static void Sandbags(List<PerimeterPart> parts, int side, float halfSize, float t, float segLen, int rows, float yaw)
        {
            const float bag = 0.6f;
            for (int r = 0; r < rows; r++)
            {
                int n = (int)Math.Ceiling(segLen / bag) + 1;
                float off = r % 2 == 0 ? 0f : bag / 2f;
                for (int k = 0; k < n; k++)
                {
                    float along = -segLen / 2f + off + k * bag;
                    if (along > segLen / 2f + 0.05f) continue;
                    // dos filas en fondo para que no se vea nada a través
                    for (int d = 0; d < 2; d++)
                        parts.Add(new PerimeterPart { Kind = PerimeterPiece.Sandbag, Center = Place(side, halfSize, t + along, 0.14f + r * 0.27f, 0.25f + d * 0.4f), Size = new V3(bag, 0.28f, 0.42f), Yaw = yaw, Side = side });
                }
            }
        }

        /// <summary>Punto en el lado "side" (0 norte, 1 sur, 2 este, 3 oeste) a "along" metros a lo largo y "inward" metros por fuera de halfSize (0 = cara interior del muro).</summary>
        public static V3 Place(int side, float halfSize, float along, float y, float outward)
        {
            float d = halfSize + outward;
            switch (side)
            {
                case 0: return new V3(along, y, d);
                case 1: return new V3(along, y, -d);
                case 2: return new V3(d, y, along);
                default: return new V3(-d, y, along);
            }
        }

        /// <summary>Distancia desde la cara interior del muro hacia fuera (para validar que nada invade la zona jugable).</summary>
        public static float Outward(PerimeterPart p, float halfSize)
            => (p.Side < 2 ? Math.Abs(p.Center.Z) : Math.Abs(p.Center.X)) - halfSize;

        /// <summary>Puntos de la espiral del alambre de espino a lo largo de X (centrada en 0): radio, paso entre vueltas y puntos por vuelta.</summary>
        public static List<V3> Coil(float length, float radius, float pitch = 0.32f, int perTurn = 14)
        {
            var pts = new List<V3>();
            int turns = Math.Max(1, (int)Math.Round(length / pitch));
            int n = turns * perTurn;
            for (int i = 0; i <= n; i++)
            {
                float a = (float)(i * 2 * Math.PI / perTurn);
                float x = -length / 2f + length * i / n;
                pts.Add(new V3(x, (float)Math.Sin(a) * radius, (float)Math.Cos(a) * radius));
            }
            return pts;
        }
    }
}
