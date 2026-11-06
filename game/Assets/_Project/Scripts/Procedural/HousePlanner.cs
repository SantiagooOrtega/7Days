using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    public enum HouseBoxKind { Floor, Wall, Frame, Roof, Beam, Furniture, Barricade }
    public enum NavFlag { Default, NotWalkable, Ignore }

    /// <summary>Una caja de la casa en el espacio local de la casa (X este, Z norte, Y arriba; el piso está en y=0).</summary>
    public class HouseBox
    {
        public HouseBoxKind Kind;
        public V3 Center, Size;
        public float Yaw, TiltX, TiltZ;   // grados
        public bool Collider = true;
        public NavFlag Nav = NavFlag.Default;
        public bool Cover;                // objeto de cobertura
        public bool Vaultable;            // se puede saltar según su altura (VaultMath)
        public string OpeningId;          // abertura a la que pertenece (marcos, tablones, antepechos)
        public string Tag;                // sill | lintel | wall | jamb | header | ledge | tile | fallen | furniture:<tipo>
        public float Height => Size.Y;
    }

    public class OpeningInfo
    {
        public HouseOpening Opening;
        public V3 Center;     // centro de la abertura en el plano del muro
        public V3 Normal;     // normal exterior del muro
        public float Thickness;
    }

    public class HousePlan
    {
        public readonly List<HouseBox> Boxes = new List<HouseBox>();
        public readonly List<OpeningInfo> Openings = new List<OpeningInfo>();
        public readonly List<V3> TorchPositions = new List<V3>();
    }

    /// <summary>Geometría de muros alrededor de las aberturas.</summary>
    public static class WallPlanner
    {
        public static float WallLength(HouseLayoutData l, string wall)
        {
            switch (wall)
            {
                case "north": case "south": return l.width;
                case "interior": return l.depth - 2f * l.wallThickness;
                default: return l.depth - 2f * l.wallThickness;   // east / west entre los muros norte y sur
            }
        }

        public struct Piece { public float T0, T1, Y0, Y1; public string Tag; public string OpeningId; }

        /// <summary>Trozos sólidos de un muro de "length" x "height" una vez recortadas las aberturas (offset = centro de cada hueco).</summary>
        public static List<Piece> Segments(float length, float height, IList<HouseOpening> openings)
        {
            var pieces = new List<Piece>();
            var sorted = new List<HouseOpening>(openings);
            sorted.Sort((a, b) => a.offset.CompareTo(b.offset));
            float cursor = -length / 2f;
            foreach (var o in sorted)
            {
                float t0 = o.offset - o.width / 2f, t1 = o.offset + o.width / 2f;
                if (t0 - cursor > 0.01f) pieces.Add(new Piece { T0 = cursor, T1 = t0, Y0 = 0f, Y1 = height, Tag = "wall" });
                if (o.sill > 0.01f) pieces.Add(new Piece { T0 = t0, T1 = t1, Y0 = 0f, Y1 = o.sill, Tag = "sill", OpeningId = o.id });
                float top = o.sill + o.height;
                if (height - top > 0.01f) pieces.Add(new Piece { T0 = t0, T1 = t1, Y0 = top, Y1 = height, Tag = "lintel", OpeningId = o.id });
                cursor = t1;
            }
            if (length / 2f - cursor > 0.01f) pieces.Add(new Piece { T0 = cursor, T1 = length / 2f, Y0 = 0f, Y1 = height, Tag = "wall" });
            return pieces;
        }
    }

    /// <summary>
    /// Convierte el JSON de la casa en cajas: piso, muros con aberturas, marcos, tablones para bloquear, techo parcialmente caído con vigas, y
    /// muebles. Todo son cajas simples (cada una con su collider) para que el jugador no atraviese nada y las balas se detengan. Es C# puro:
    /// el diseño (entradas, línea de tiro, vault, techo, colliders) se prueba sin Unity.
    /// </summary>
    public static class HousePlanner
    {
        public const float FloorThickness = 0.12f;
        public const float RoofThickness = 0.25f;
        public const float DoorFramePad = 0.08f;

        public static HousePlan Plan(HouseLayoutData l)
        {
            var plan = new HousePlan();
            float W = l.width, D = l.depth, H = l.wallHeight, T = l.wallThickness;

            // piso
            plan.Boxes.Add(new HouseBox { Kind = HouseBoxKind.Floor, Center = new V3(0, FloorThickness / 2f, 0), Size = new V3(W, FloorThickness, D), Tag = "floor" });

            // muros
            foreach (var wall in HouseTypes.Walls)
            {
                var ops = new List<HouseOpening>();
                foreach (var o in l.openings) if (o.wall == wall) ops.Add(o);
                AddWall(plan, l, wall, ops);
            }

            // vigas del techo (a lo largo de X)
            foreach (float z in new[] { -D * 0.34f, 0f, D * 0.34f })
                plan.Boxes.Add(new HouseBox { Kind = HouseBoxKind.Beam, Center = new V3(0, FloorThickness + H - 0.15f, z), Size = new V3(W - T, 0.3f, 0.25f), Nav = NavFlag.Ignore, Tag = "beam" });

            AddRoof(plan, l);
            AddFurniture(plan, l);

            foreach (var t in l.torches) plan.TorchPositions.Add(new V3(t.x, t.y, t.z));
            return plan;
        }

        // ------------------------------------------------------------------ muros
        private static void AddWall(HousePlan plan, HouseLayoutData l, string wall, List<HouseOpening> ops)
        {
            float W = l.width, D = l.depth, H = l.wallHeight;
            float thick = wall == "interior" ? 0.2f : l.wallThickness;
            float length = WallPlanner.WallLength(l, wall);
            bool alongX = wall == "north" || wall == "south";
            // posición del plano medio del muro y normal exterior
            V3 origin, normal;
            switch (wall)
            {
                case "north": origin = new V3(0, 0, D / 2f - thick / 2f); normal = new V3(0, 0, 1); break;
                case "south": origin = new V3(0, 0, -D / 2f + thick / 2f); normal = new V3(0, 0, -1); break;
                case "east": origin = new V3(W / 2f - thick / 2f, 0, 0); normal = new V3(1, 0, 0); break;
                case "west": origin = new V3(-W / 2f + thick / 2f, 0, 0); normal = new V3(-1, 0, 0); break;
                default: origin = new V3(l.interiorX, 0, 0); normal = new V3(1, 0, 0); break;
            }

            foreach (var p in WallPlanner.Segments(length, H, ops))
            {
                float tc = (p.T0 + p.T1) / 2f, len = p.T1 - p.T0, yc = FloorThickness + (p.Y0 + p.Y1) / 2f, hy = p.Y1 - p.Y0;
                V3 center = alongX ? new V3(origin.X + tc, yc, origin.Z) : new V3(origin.X, yc, origin.Z + tc);
                V3 size = alongX ? new V3(len, hy, thick) : new V3(thick, hy, len);
                plan.Boxes.Add(new HouseBox
                {
                    Kind = HouseBoxKind.Wall, Center = center, Size = size, Nav = NavFlag.NotWalkable, Cover = true,
                    Tag = p.Tag, OpeningId = p.OpeningId,
                    Vaultable = p.Tag == "sill" && IsVaultableSill(l, p.OpeningId),
                });
            }

            foreach (var o in ops)
            {
                float tc = o.offset;
                V3 c = alongX ? new V3(origin.X + tc, FloorThickness + o.sill + o.height / 2f, origin.Z) : new V3(origin.X, FloorThickness + o.sill + o.height / 2f, origin.Z + tc);
                plan.Openings.Add(new OpeningInfo { Opening = o, Center = c, Normal = normal, Thickness = thick });
                AddFrame(plan, o, alongX, origin, thick);
                // tablones para cuando el director bloquea la abertura (desactivados por defecto)
                V3 bsize = alongX ? new V3(o.width, o.height, 0.14f) : new V3(0.14f, o.height, o.width);
                plan.Boxes.Add(new HouseBox { Kind = HouseBoxKind.Barricade, Center = c, Size = bsize, OpeningId = o.id, Nav = NavFlag.NotWalkable, Tag = "barricade" });
            }
        }

        private static bool IsVaultableSill(HouseLayoutData l, string openingId)
        {
            foreach (var o in l.openings) if (o.id == openingId) return o.vaultable;
            return false;
        }

        /// <summary>Marco: jambas y dintel por FUERA del hueco (no reducen el paso libre), y un reborde en las ventanas.</summary>
        private static void AddFrame(HousePlan plan, HouseOpening o, bool alongX, V3 origin, float thick)
        {
            float pad = DoorFramePad, ft = thick + 0.06f;
            float y0 = FloorThickness + o.sill, y1 = y0 + o.height, yc = (y0 + y1) / 2f;
            void Add(float t, float tw, float yCenter, float h, string tag)
            {
                V3 c = alongX ? new V3(origin.X + t, yCenter, origin.Z) : new V3(origin.X, yCenter, origin.Z + t);
                V3 s = alongX ? new V3(tw, h, ft) : new V3(ft, h, tw);
                plan.Boxes.Add(new HouseBox { Kind = HouseBoxKind.Frame, Center = c, Size = s, Nav = NavFlag.NotWalkable, OpeningId = o.id, Tag = tag });
            }
            Add(o.offset - o.width / 2f - pad / 2f, pad, yc, o.height, "jamb");
            Add(o.offset + o.width / 2f + pad / 2f, pad, yc, o.height, "jamb");
            Add(o.offset, o.width + 2f * pad, y1 + pad / 2f, pad, "header");
            if (o.sill > 0.01f) Add(o.offset, o.width + 2f * pad, y0 - 0.02f, 0.05f, "ledge");
        }

        // ------------------------------------------------------------------ techo
        private static void AddRoof(HousePlan plan, HouseLayoutData l)
        {
            var rng = new Random(l.roofSeed);
            float W = l.width, D = l.depth, H = l.wallHeight;
            int cols = Math.Max(2, (int)Math.Round(W / 2f)), rows = Math.Max(2, (int)Math.Round(D / 2f));
            float tw = W / cols, td = D / rows;
            int fallenCount = 0;
            for (int i = 0; i < cols; i++)
            {
                for (int j = 0; j < rows; j++)
                {
                    float cx = -W / 2f + tw * (i + 0.5f), cz = -D / 2f + td * (j + 0.5f);
                    // la habitación del lado de la puerta principal (oeste) está más destruida
                    float bias = cx < l.interiorX ? 0.18f : -0.12f;
                    double roll = rng.NextDouble();
                    bool missing = roll < l.roofCollapse + bias;
                    // garantía: la esquina noreste se conserva (refugio) y siempre falta al menos una losa
                    if (i == cols - 1 && j == rows - 1) missing = false;
                    if (missing)
                    {
                        if (fallenCount < 2 && rng.NextDouble() < 0.6)
                        {
                            fallenCount++;
                            plan.Boxes.Add(new HouseBox
                            {
                                Kind = HouseBoxKind.Roof, Center = new V3(cx + (float)(rng.NextDouble() - 0.5) * 0.6f, FloorThickness + 0.3f, cz + (float)(rng.NextDouble() - 0.5) * 0.6f),
                                Size = new V3(tw - 0.1f, RoofThickness, td - 0.1f), Yaw = (float)(rng.NextDouble() * 60 - 30), TiltX = (float)(rng.NextDouble() * 12 - 6), TiltZ = (float)(rng.NextDouble() * 12 - 6),
                                Cover = true, Tag = "fallen",
                            });
                        }
                        continue;
                    }
                    bool tilted = rng.NextDouble() < 0.25;
                    plan.Boxes.Add(new HouseBox
                    {
                        Kind = HouseBoxKind.Roof, Center = new V3(cx, FloorThickness + H + RoofThickness / 2f + (tilted ? 0.12f : 0f), cz),
                        Size = new V3(tw, RoofThickness, td), TiltX = tilted ? (float)(rng.NextDouble() * 14 + 8) * (rng.NextDouble() < 0.5 ? -1 : 1) : 0f,
                        Nav = NavFlag.Ignore, Tag = tilted ? "tile_tilted" : "tile",
                    });
                }
            }
            // al menos una losa caída en el piso aunque la probabilidad no la haya dado
            if (fallenCount == 0)
                plan.Boxes.Add(new HouseBox { Kind = HouseBoxKind.Roof, Center = new V3(-W * 0.25f, FloorThickness + 0.3f, -D * 0.1f), Size = new V3(tw - 0.1f, RoofThickness, td - 0.1f), Yaw = 18f, TiltX = 5f, TiltZ = -4f, Cover = true, Tag = "fallen" });
        }

        // ------------------------------------------------------------------ muebles
        private static void AddFurniture(HousePlan plan, HouseLayoutData l)
        {
            foreach (var f in l.furniture)
            {
                var s = HouseTypes.FurnitureSize(f.kind);
                bool vaultable = VaultMath.Classify(s.Y, true, new MovementSettings()) == VaultVerdict.Vault;
                plan.Boxes.Add(new HouseBox
                {
                    Kind = HouseBoxKind.Furniture, Center = new V3(f.x, FloorThickness + s.Y / 2f, f.z), Size = s, Yaw = f.yaw,
                    Cover = true, Vaultable = vaultable, Nav = NavFlag.NotWalkable, Tag = "furniture:" + f.kind,
                });
            }
        }

        // ------------------------------------------------------------------ utilidades para pruebas
        /// <summary>Segmento contra caja alineada a los ejes (método de las losas). Solo para cajas sin giro.</summary>
        public static bool SegmentHitsBox(V3 a, V3 b, V3 center, V3 size)
        {
            float tMin = 0f, tMax = 1f;
            for (int axis = 0; axis < 3; axis++)
            {
                float lo = center[axis] - size[axis] / 2f, hi = center[axis] + size[axis] / 2f;
                float d = b[axis] - a[axis];
                if (Math.Abs(d) < 1e-8f) { if (a[axis] < lo || a[axis] > hi) return false; continue; }
                float t0 = (lo - a[axis]) / d, t1 = (hi - a[axis]) / d;
                if (t0 > t1) { var t = t0; t0 = t1; t1 = t; }
                tMin = Math.Max(tMin, t0); tMax = Math.Min(tMax, t1);
                if (tMin > tMax) return false;
            }
            return true;
        }
    }
}
