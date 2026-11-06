using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Una abertura en un muro: puerta, ventana o ventana grande (para saltarla). Es parte del JSON de la casa.</summary>
    [Serializable]
    public class HouseOpening
    {
        public string id;
        public string wall;        // north (+Z), south (-Z), east (+X), west (-X), interior
        public float offset;       // metros desde el centro del muro, a lo largo de él
        public float width = 1.0f;
        public float height = 2.1f; // alto libre de la abertura
        public float sill = 0f;     // altura del borde inferior sobre el piso (0 en las puertas)
        public string kind = "door"; // door | window | window_large
        public bool vaultable;       // el antepecho de una ventana grande se puede saltar
        public int[] blockedDays = new int[0]; // días en que el director la tapa con tablones
    }

    [Serializable]
    public class HouseFurniture
    {
        public string kind;  // crate | table | shelf | sofa | bed | cabinet
        public float x, z;   // posición dentro de la casa (metros desde su centro)
        public float yaw;
    }

    [Serializable]
    public class HouseTorch
    {
        public float x, z, y = 2f; // y = altura sobre el piso
    }

    /// <summary>
    /// Casa del mapa en datos (JSON): posición en el mundo, dimensiones, aberturas con su bloqueo por día, muebles, antorchas y semilla del techo
    /// caído. Es el mismo formato que podrá traer un mapa de la base de datos.
    /// </summary>
    [Serializable]
    public class HouseLayoutData
    {
        public float x = 11f, z = 0.5f, yaw = 0f;      // centro en el mundo; yaw en múltiplos de 90
        public float width = 10f, depth = 8f;           // X y Z locales
        public float wallHeight = 3f, wallThickness = 0.3f;
        public float interiorX = 0f;                    // x local del muro que separa las 2 habitaciones
        public int roofSeed = 5;
        public float roofCollapse = 0.4f;               // fracción aproximada de losas de techo caídas (0..1)
        public HouseOpening[] openings = new HouseOpening[0];
        public HouseFurniture[] furniture = new HouseFurniture[0];
        public HouseTorch[] torches = new HouseTorch[0];

        public static HouseLayoutData Default()
        {
            return new HouseLayoutData
            {
                openings = new[]
                {
                    // 3 entradas: puerta principal (mira a la autopista), puerta trasera y ventana grande que se salta
                    new HouseOpening { id = "front_door", wall = "west", offset = 0.5f, width = 1.2f, height = 2.2f, kind = "door" },
                    new HouseOpening { id = "back_door", wall = "east", offset = -1.0f, width = 1.1f, height = 2.1f, kind = "door" },
                    new HouseOpening { id = "big_window", wall = "south", offset = -1.5f, width = 2.2f, sill = 0.9f, height = 1.3f, kind = "window_large", vaultable = true },
                    // ventanas con línea de tiro (el borde a 1.15 m deja ver y disparar, pero no se pasa)
                    new HouseOpening { id = "win_west", wall = "west", offset = -2.4f, width = 0.9f, sill = 1.15f, height = 0.8f, kind = "window" },
                    new HouseOpening { id = "win_north_a", wall = "north", offset = -2.0f, width = 0.9f, sill = 1.15f, height = 0.8f, kind = "window" },
                    new HouseOpening { id = "win_north_b", wall = "north", offset = 2.2f, width = 0.9f, sill = 1.15f, height = 0.8f, kind = "window" },
                    new HouseOpening { id = "win_east", wall = "east", offset = 2.0f, width = 0.9f, sill = 1.15f, height = 0.8f, kind = "window" },
                    // paso entre las 2 habitaciones
                    new HouseOpening { id = "inner_door", wall = "interior", offset = 0f, width = 1.1f, height = 2.1f, kind = "door" },
                },
                furniture = new[]
                {
                    new HouseFurniture { kind = "sofa", x = -4.2f, z = -2.4f, yaw = 90f },
                    new HouseFurniture { kind = "table", x = -2.2f, z = 1.6f },
                    new HouseFurniture { kind = "shelf", x = -3.6f, z = 3.55f },
                    new HouseFurniture { kind = "crate", x = -0.9f, z = 3.2f },
                    new HouseFurniture { kind = "crate", x = -0.8f, z = 2.2f, yaw = 20f },
                    new HouseFurniture { kind = "bed", x = 3.4f, z = 2.9f },
                    new HouseFurniture { kind = "cabinet", x = 4.5f, z = 1.6f, yaw = 90f },
                    new HouseFurniture { kind = "crate", x = 1.2f, z = -3.1f },
                    new HouseFurniture { kind = "crate", x = 2.1f, z = -3.2f, yaw = 35f },
                },
                torches = new[]
                {
                    new HouseTorch { x = -4.7f, z = 2.5f }, new HouseTorch { x = 4.7f, z = -2.8f },
                    new HouseTorch { x = -0.25f, z = -2.5f }, new HouseTorch { x = -5.3f, z = 1.9f, y = 2.1f }, // la última, afuera junto a la puerta principal
                },
            };
        }
    }

    public static class HouseTypes
    {
        public static readonly string[] Walls = { "north", "south", "east", "west", "interior" };
        public static readonly string[] OpeningKinds = { "door", "window", "window_large" };
        public static readonly string[] FurnitureKinds = { "crate", "table", "shelf", "sofa", "bed", "cabinet" };

        /// <summary>Dimensiones (ancho X, alto Y, fondo Z) de cada mueble.</summary>
        public static V3 FurnitureSize(string kind)
        {
            switch (kind)
            {
                case "crate": return new V3(0.9f, 0.9f, 0.9f);
                case "table": return new V3(1.4f, 0.8f, 0.8f);
                case "shelf": return new V3(1.6f, 1.8f, 0.4f);
                case "sofa": return new V3(1.8f, 0.8f, 0.8f);
                case "bed": return new V3(2.0f, 0.55f, 1.0f);
                default: return new V3(0.9f, 1.7f, 0.5f); // cabinet
            }
        }

        /// <summary>true si el director puede tapar esta abertura en el día dado.</summary>
        public static bool IsBlocked(HouseOpening o, int day)
        {
            if (o?.blockedDays == null) return false;
            return Array.IndexOf(o.blockedDays, day) >= 0;
        }
    }

    public class HouseValidation
    {
        public readonly List<string> Errors = new List<string>();
        public bool Ok => Errors.Count == 0;
    }

    /// <summary>Valida un JSON de casa: dimensiones razonables, aberturas dentro de su muro y sin solaparse, muebles dentro de la casa.</summary>
    public static class HouseValidator
    {
        public static HouseValidation Validate(HouseLayoutData l)
        {
            var v = new HouseValidation();
            if (l == null) { v.Errors.Add("casa vacía"); return v; }
            if (l.width < 6f || l.width > 20f) v.Errors.Add("width fuera de 6..20");
            if (l.depth < 5f || l.depth > 16f) v.Errors.Add("depth fuera de 5..16");
            if (l.wallHeight < 2.4f || l.wallHeight > 4.5f) v.Errors.Add("wallHeight fuera de 2.4..4.5");
            if (l.wallThickness < 0.15f || l.wallThickness > 0.6f) v.Errors.Add("wallThickness fuera de 0.15..0.6");
            if (Math.Abs(l.interiorX) > l.width / 2f - 1.5f) v.Errors.Add("interiorX demasiado cerca de un muro");
            if (Math.Abs(l.x) > 30f || Math.Abs(l.z) > 30f) v.Errors.Add("la casa está fuera del mapa");
            if (l.roofCollapse < 0f || l.roofCollapse > 1f) v.Errors.Add("roofCollapse fuera de 0..1");
            if (Math.Abs(((l.yaw % 90f) + 90f) % 90f) > 0.01f) v.Errors.Add("yaw debe ser múltiplo de 90");

            var ids = new HashSet<string>();
            var perWall = new Dictionary<string, List<HouseOpening>>();
            foreach (var o in l.openings ?? new HouseOpening[0])
            {
                if (o == null) { v.Errors.Add("abertura nula"); continue; }
                if (string.IsNullOrEmpty(o.id) || !ids.Add(o.id)) v.Errors.Add("id repetido o vacío: " + o.id);
                if (Array.IndexOf(HouseTypes.Walls, o.wall) < 0) { v.Errors.Add(o.id + ": muro desconocido '" + o.wall + "'"); continue; }
                if (Array.IndexOf(HouseTypes.OpeningKinds, o.kind) < 0) v.Errors.Add(o.id + ": tipo desconocido '" + o.kind + "'");
                if (o.width < 0.5f || o.width > 4f) v.Errors.Add(o.id + ": width fuera de 0.5..4");
                if (o.sill < 0f || o.height < 0.4f || o.sill + o.height > l.wallHeight - 0.2f) v.Errors.Add(o.id + ": alto/antepecho no caben en el muro");
                if (o.kind == "door" && (o.sill > 0.01f || o.height < 1.9f || o.width < 0.8f)) v.Errors.Add(o.id + ": una puerta necesita sill 0, alto >= 1.9 y ancho >= 0.8");
                float len = WallPlanner.WallLength(l, o.wall);
                if (Math.Abs(o.offset) + o.width / 2f > len / 2f - 0.15f) v.Errors.Add(o.id + ": se sale del muro (o toca la esquina)");
                if (!perWall.TryGetValue(o.wall, out var list)) perWall[o.wall] = list = new List<HouseOpening>();
                foreach (var other in list)
                    if (Math.Abs(o.offset - other.offset) < (o.width + other.width) / 2f + 0.2f) v.Errors.Add(o.id + " se solapa con " + other.id);
                list.Add(o);
                if (o.blockedDays != null) foreach (int d in o.blockedDays) if (d < 1 || d > 7) v.Errors.Add(o.id + ": blockedDays fuera de 1..7");
            }

            foreach (var f in l.furniture ?? new HouseFurniture[0])
            {
                if (f == null || Array.IndexOf(HouseTypes.FurnitureKinds, f.kind) < 0) { v.Errors.Add("mueble desconocido: " + f?.kind); continue; }
                var s = HouseTypes.FurnitureSize(f.kind);
                double a = f.yaw * Math.PI / 180.0;
                float hx = (float)(Math.Abs(Math.Cos(a)) * s.X / 2 + Math.Abs(Math.Sin(a)) * s.Z / 2), hz = (float)(Math.Abs(Math.Sin(a)) * s.X / 2 + Math.Abs(Math.Cos(a)) * s.Z / 2);
                float margin = l.wallThickness / 2f;
                if (Math.Abs(f.x) + hx > l.width / 2f - margin + 0.01f || Math.Abs(f.z) + hz > l.depth / 2f - margin + 0.01f) v.Errors.Add("mueble '" + f.kind + "' fuera de la casa");
            }
            return v;
        }
    }
}
