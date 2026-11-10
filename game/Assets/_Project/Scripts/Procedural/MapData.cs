using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Una pieza del kit procedural del mapa: tipo (ver <see cref="MapKit.Kinds"/>), nombre, posición, giro (grados), tamaño, color sRGB, superficie (textura
    /// procedural), si es cobertura, una cantidad (piezas sueltas con semilla) y un rango (farolas: orden de las que parpadean).
    /// </summary>
    [Serializable]
    public class MapProp
    {
        public string kind;
        public string name;
        public float[] pos = { 0f, 0f, 0f };
        public float[] rot = { 0f, 0f, 0f };
        public float[] size = { 1f, 1f, 1f };
        public float[] color = { 0.5f, 0.5f, 0.5f };
        public string surface = "";
        public bool cover;
        public int count;
        public int rank = -1;
    }

    /// <summary>Punto de aparición de zombis en un borde del mapa (lado: n, s, e, o).</summary>
    [Serializable]
    public class MapSpawn
    {
        public string id;
        public float x, z;
        public string side;
    }

    /// <summary>Ambientación del mapa: hora, niebla, ambiente, sol, post-procesado básico y efectos (humo, ceniza, rayos, luces que parpadean, suciedad).</summary>
    [Serializable]
    public class MapAmbience
    {
        public float hour = 17f;                       // informativo: 0-24 (la luz la dan los valores de abajo)
        public float[] fogColor = { 0.5f, 0.55f, 0.6f };
        public float fogDensity = 0.008f;
        public float[] ambientSky = { 0.5f, 0.56f, 0.64f }, ambientEquator = { 0.4f, 0.42f, 0.46f }, ambientGround = { 0.22f, 0.22f, 0.23f };
        public float sunPitch = 52f, sunYaw = -35f, sunIntensity = 1f, shadowStrength = 0.78f;
        public float[] sunColor = { 0.96f, 0.95f, 0.93f };
        public float brightness = 1.25f;
        public float density = 1f;
        public int groundFogLayers = 2; public float groundFogDensity = 1f, groundFogAlpha = 0.075f; public int hazeBanks = 10; public float skyDim = 0.3f;
        public float ash = 1f; public int smokeColumns = 3; public float smoke = 1f, sparks = 1f; public int godRays = 7; public float godRayIntensity = 0.13f;
        public int redFlickerLamps = 2;
        public int cracks = 14, weedTufts = 70, puddles = 7, bloodStains = 8, skylineBuildings = 22;
        public bool dust = true, skyline = true, roadMarkings = true;
        public float fireflies;                        // luciérnagas alrededor del jugador (0 = ninguna)
        public string soundscape = "none";             // sonido ambiente: none | wind | forest
    }

    /// <summary>
    /// Un mapa completo como datos (JSON con JsonUtility; esquema zod en backend/src/modules/maps): perímetro, piezas del kit, casas con entradas, spawns en los bordes,
    /// vegetación y señales, botín y ambientación. Los mapas se construyen en la MISMA escena (Game) cambiando solo este contenido.
    /// </summary>
    [Serializable]
    public class MapData
    {
        public int version = 1;
        public string id = "calle_en_ruinas";
        public string name = "Calle en ruinas";
        public int baseDifficulty = 1;                 // 1..5
        public float halfSize = 30f;                   // el mapa va de -halfSize a +halfSize en X y Z
        public int seed = 42;                          // semilla de las piezas sueltas y de los tonos
        public float[] playerStart = { 0f, 0.1f, -14f, 0f };   // x, y, z, giro
        public MapProp[] props = new MapProp[0];
        public HouseLayoutData[] houses = new HouseLayoutData[0];
        public MapSpawn[] spawns = new MapSpawn[0];
        public PropLayoutData vegetation = new PropLayoutData();
        public LootLayoutData loot = new LootLayoutData();
        public MapAmbience ambience = new MapAmbience();
    }

    /// <summary>Lista de mapas (Resources/Maps y respuesta del backend).</summary>
    [Serializable]
    public class MapList { public MapData[] maps = new MapData[0]; }

    /// <summary>Tipos de pieza que entiende el constructor y validación de un mapa que viene de fuera (backend, JSON local).</summary>
    public static class MapKit
    {
        public static readonly string[] Kinds = { "ground", "road", "wall", "crumbles", "barrier", "crate", "container", "pillar", "beam", "roof", "brickwall", "pipe", "barrel", "fire_barrel", "rubble_field", "lamp",
                                                  "soil", "boundary", "fence", "trail", "boulder", "trunk", "caravan", "lantern" };
        public const int MinSpawns = 8;

        public static List<string> Validate(MapData m)
        {
            var e = new List<string>();
            if (m == null) { e.Add("mapa vacío"); return e; }
            string n = (m.id ?? "?") + ": ";
            if (string.IsNullOrEmpty(m.id) || string.IsNullOrEmpty(m.name)) e.Add(n + "falta id o nombre");
            if (m.baseDifficulty < 1 || m.baseDifficulty > 5) e.Add(n + "baseDifficulty fuera de 1..5");
            if (!(m.halfSize >= 15f && m.halfSize <= 120f)) e.Add(n + "halfSize fuera de 15..120");
            if (m.playerStart == null || m.playerStart.Length != 4) e.Add(n + "playerStart debe tener 4 valores");
            else if (Math.Abs(m.playerStart[0]) > m.halfSize || Math.Abs(m.playerStart[2]) > m.halfSize) e.Add(n + "el jugador empieza fuera del mapa");
            if (m.props == null) e.Add(n + "sin props");
            else foreach (var p in m.props)
                {
                    if (p == null || Array.IndexOf(Kinds, p.kind) < 0) { e.Add(n + "pieza de tipo desconocido '" + p?.kind + "'"); continue; }
                    if (p.pos == null || p.pos.Length != 3 || p.rot == null || p.rot.Length != 3 || p.size == null || p.size.Length != 3) { e.Add(n + p.name + ": pos/rot/size deben tener 3 valores"); continue; }
                    if (Math.Abs(p.pos[0]) > m.halfSize + 5f || Math.Abs(p.pos[2]) > m.halfSize + 5f) e.Add(n + p.name + ": fuera del mapa");
                    if (p.color == null || p.color.Length != 3) e.Add(n + p.name + ": color debe tener 3 valores");
                }
            if (m.houses == null || m.houses.Length == 0) e.Add(n + "necesita al menos una casa o refugio");
            else foreach (var h in m.houses) if (h.openings == null || h.openings.Length < 3) e.Add(n + "cada casa necesita 3 entradas o más");
            e.AddRange(ValidateSpawns(m));
            if (m.loot == null || m.loot.points == null || m.loot.points.Length == 0) e.Add(n + "sin puntos de botín");
            if (m.ambience == null) e.Add(n + "sin ambientación");
            else if (!(m.ambience.fogDensity >= 0f && m.ambience.fogDensity <= 0.2f)) e.Add(n + "densidad de niebla fuera de 0..0.2");
            return e;
        }

        /// <summary>Spawns: al menos 8, en el borde (a menos de 6 m del perímetro), nunca en el centro, sin repetir y repartidos por 3 lados o más.</summary>
        public static List<string> ValidateSpawns(MapData m)
        {
            var e = new List<string>();
            string n = (m.id ?? "?") + ": ";
            if (m.spawns == null || m.spawns.Length < MinSpawns) { e.Add(n + "necesita " + MinSpawns + " spawns o más"); return e; }
            var sides = new HashSet<string>(); var ids = new HashSet<string>();
            foreach (var s in m.spawns)
            {
                if (!ids.Add(s.id ?? "")) e.Add(n + "spawn repetido " + s.id);
                float edge = m.halfSize - Math.Max(Math.Abs(s.x), Math.Abs(s.z));
                if (edge > 6f || edge < 0.5f) e.Add(n + "spawn " + s.id + " no está en el borde");
                sides.Add(s.side);
            }
            if (sides.Count < 3) e.Add(n + "spawns en menos de 3 lados");
            return e;
        }

        /// <summary>Spawns repartidos por los 4 lados, "inset" metros dentro del muro, saltándose las zonas ocupadas (cruces de calle, edificios).</summary>
        public static MapSpawn[] EdgeSpawns(float halfSize, int perSide, float inset, Func<float, float, bool> blocked = null)
        {
            var list = new List<MapSpawn>();
            string[] names = { "n", "s", "e", "o" };
            for (int side = 0; side < 4; side++)
                for (int i = 0; i < perSide; i++)
                {
                    float t = -halfSize * 0.75f + (i + 0.5f) / perSide * halfSize * 1.5f, d = halfSize - inset;
                    float x = side < 2 ? t : (side == 2 ? d : -d), z = side < 2 ? (side == 0 ? d : -d) : t;
                    if (blocked != null && blocked(x, z)) { x += 2.5f * Math.Sign(t == 0f ? 1f : t) * (side < 2 ? 1f : 0f); z += 2.5f * Math.Sign(t == 0f ? 1f : t) * (side < 2 ? 0f : 1f); }
                    list.Add(new MapSpawn { id = names[side] + (i + 1), x = (float)Math.Round(x, 2), z = (float)Math.Round(z, 2), side = names[side] });
                }
            return list.ToArray();
        }

        // ---------------------------------------------------------------- mapa 1: Calle en ruinas
        private static MapProp P(string kind, string name, float x, float y, float z, float sx = 1f, float sy = 1f, float sz = 1f, float yaw = 0f, float[] color = null, string surface = "", bool cover = false, int count = 0, int rank = -1, float pitch = 0f, float roll = 0f)
            => new MapProp { kind = kind, name = name, pos = new[] { x, y, z }, rot = new[] { pitch, yaw, roll }, size = new[] { sx, sy, sz }, color = color ?? new[] { 0.5f, 0.49f, 0.46f }, surface = surface, cover = cover, count = count, rank = rank };

        private static float[] Rgb(float r, float g, float b) => new[] { r, g, b };

        /// <summary>
        /// Las piezas del mapa 1 en el orden exacto en que las construía el código anterior (el orden importa: los tonos y los desplazamientos de textura salen de una
        /// sola semilla que se va consumiendo pieza a pieza).
        /// </summary>
        public static MapProp[] StreetRuinsProps()
        {
            var concrete = Rgb(0.5f, 0.49f, 0.46f); var concreteDark = Rgb(0.4f, 0.4f, 0.4f); var pipe = Rgb(0.36f, 0.38f, 0.4f);
            var l = new List<MapProp>
            {
                P("ground", "Ground", 0f, -0.25f, 0f, 60f, 0.5f, 60f, color: concrete, surface: "concrete"),
                P("road", "Road", 0f, 0.01f, 0f, 8f, 0.04f, 58f, color: Rgb(0.2f, 0.2f, 0.21f), surface: "floor"),
                P("wall", "Wall N", 0f, 2f, 30.5f, 61f, 4f, 1f, color: concreteDark, surface: "concrete"),
                P("wall", "Wall S", 0f, 2f, -30.5f, 61f, 4f, 1f, color: concreteDark, surface: "concrete"),
                P("wall", "Wall E", 30.5f, 2f, 0f, 1f, 4f, 61f, color: concreteDark, surface: "concrete"),
                P("wall", "Wall W", -30.5f, 2f, 0f, 1f, 4f, 61f, color: concreteDark, surface: "concrete"),
                P("crumbles", "Crumble", 0f, 4f, 0f, 30.5f, 1f, 28f, color: concreteDark, surface: "concrete", count: 18),
                P("barrier", "Barrier 1", -4f, 0.6f, -8f, 3.5f, 1.2f, 0.6f, 0f, concrete, "concrete", true),
                P("barrier", "Barrier 2", 4f, 0.6f, -6f, 3.5f, 1.2f, 0.6f, 15f, concrete, "concrete", true),
                P("barrier", "Barrier 3", -9f, 0.6f, 0f, 0.6f, 1.2f, 4f, 0f, concrete, "concrete", true),
                P("barrier", "Barrier 4", 0f, 0.6f, 4f, 4f, 1.2f, 0.6f, 0f, concrete, "concrete", true),
                P("barrier", "Barrier 5", 12f, 0.6f, -10f, 0.6f, 1.2f, 3.5f, 0f, concrete, "concrete", true),
                P("crate", "Crate 1", 0f, 0.6f, -4f, 1.2f, 1.2f, 1.2f, 20f, Rgb(0.42f, 0.3f, 0.18f), "crate", true),
                P("crate", "Crate 3", -13f, 0.6f, -4f, 1.2f, 1.2f, 1.2f, 35f, Rgb(0.55f, 0.43f, 0.28f), "cardboard", true),
                P("crate", "Crate 4", -15f, 0.4f, -3.4f, 0.8f, 0.8f, 0.8f, 10f, Rgb(0.55f, 0.43f, 0.28f), "cardboard", false),
                P("crate", "Crate 5", 14.5f, 0.4f, 8f, 0.8f, 0.8f, 0.8f, -25f, Rgb(0.42f, 0.3f, 0.18f), "crate", false),
                P("container", "Container A", -19f, 1.3f, 4f, 2.5f, 2.6f, 6f, 0f, Rgb(0.5f, 0.2f, 0.15f), "container", true),
                P("container", "Container B", 22.7f, 1.3f, -2f, 2.5f, 2.6f, 6f, 90f, Rgb(0.2f, 0.3f, 0.4f), "container", true),
                P("container", "Container C", -14f, 1.3f, 19f, 2.5f, 2.6f, 6f, 20f, Rgb(0.3f, 0.35f, 0.2f), "container", true),
                P("container", "Container D", 17f, 1.3f, 16f, 2.5f, 2.6f, 6f, -15f, Rgb(0.45f, 0.35f, 0.15f), "container", true),
                P("container", "Container E", -19f, 3.9f, 4.4f, 2.5f, 2.6f, 6f, 6f, Rgb(0.25f, 0.28f, 0.3f), "container", true),
            };
            for (int i = -2; i <= 2; i++) l.Add(P("pillar", "Pillar " + i, i * 9f, 2.75f, 23f, 1.2f, 5.5f, 1.2f, color: Rgb(0.45f, 0.28f, 0.22f), surface: "wall"));
            for (int i = -2; i < 2; i++) l.Add(P("beam", "Beam " + i, i * 9f + 4.5f, 5.6f, 23f, 8.4f, 0.3f, 0.4f, color: pipe, surface: "metal", roll: i == 0 ? -14f : 0f));
            for (int i = -1; i <= 1; i++) l.Add(P("roof", "Roof " + i, i * 9f, 5.85f, 26.5f, 8f, 0.15f, 6.5f, color: pipe, surface: "metal"));
            l.Add(P("brickwall", "Back Wall A", -13f, 2f, 28f, 11f, 4f, 0.8f, color: Rgb(0.45f, 0.28f, 0.22f), surface: "wall"));
            l.Add(P("brickwall", "Back Wall B", 9f, 1.2f, 28f, 8f, 2.4f, 0.8f, color: Rgb(0.45f, 0.28f, 0.22f), surface: "wall"));
            for (int i = 0; i < 3; i++) l.Add(P("pipe", "Pipe " + i, -25f, 0.4f + i * 0.5f, -12f + i * 1.3f, 0.45f, 4f, 0.45f, color: pipe, surface: "metal", pitch: 90f));
            float[][] barrelColors = { Rgb(0.4f, 0.18f, 0.12f), Rgb(0.2f, 0.3f, 0.38f), Rgb(0.35f, 0.33f, 0.2f) };
            float[][] barrels = { new[] { -6f, 0.5f, 12f }, new[] { -5.2f, 0.5f, 12.4f }, new[] { 22f, 0.5f, 8f }, new[] { -22f, 0.5f, -20f } };
            for (int i = 0; i < barrels.Length; i++) l.Add(P("barrel", "Barrel " + i, barrels[i][0], barrels[i][1], barrels[i][2], 0.6f, 0.5f, 0.6f, color: barrelColors[i % 3], surface: "metal"));
            l.Add(P("fire_barrel", "Fire Barrel 0", -7f, 0f, -12f, 0.6f, 0.5f, 0.6f, color: Rgb(0.22f, 0.15f, 0.12f), surface: "metal"));
            l.Add(P("fire_barrel", "Fire Barrel 1", 10f, 0f, 14f, 0.6f, 0.5f, 0.6f, color: Rgb(0.22f, 0.15f, 0.12f), surface: "metal"));
            l.Add(P("rubble_field", "Rubble", 0f, 0f, 0f, 56f, 3.5f, 56f, color: Rgb(0.42f, 0.41f, 0.39f), surface: "concrete", count: 70));
            int[] rank = { 1, 3, 0, 2 };   // orden en que parpadean (antes: la lista {2, 0, 3, 1})
            float[][] posts = { new[] { -5.5f, 0f, -18f }, new[] { 5.5f, 0f, 0f }, new[] { -5.5f, 0f, 14f }, new[] { 5.5f, 0f, -26f } };
            for (int i = 0; i < posts.Length; i++) l.Add(P("lamp", "Lamp " + i, posts[i][0], posts[i][1], posts[i][2], color: pipe, surface: "metal", rank: rank[i]));
            return l.ToArray();
        }

        /// <summary>Zonas que la vegetación y los props no deben pisar en el mapa 1 (las de antes; la casa se añade aparte).</summary>
        public static List<Zone> StreetRuinsExclusions()
        {
            var z = new List<Zone>();
            void C(float x, float zz, float r) => z.Add(Zone.Circle(x, zz, r));
            C(-19f, 4f, 4.4f); C(22.7f, -2f, 4.4f); C(-14f, 19f, 4.4f); C(17f, 16f, 4.4f);
            C(-4f, -8f, 2.4f); C(4f, -6f, 2.4f); C(-9f, 0f, 2.6f); C(0f, 4f, 2.6f); C(12f, -10f, 2.4f);
            C(0f, -4f, 1.4f); C(-13f, -4f, 1.4f); C(-15f, -3.4f, 1.1f); C(14.5f, 8f, 1.1f);
            C(-7f, -12f, 1.7f); C(10f, 14f, 1.7f);
            C(-6f, 12f, 1.1f); C(-5.2f, 12.4f, 1.1f); C(22f, 8f, 1.1f); C(-22f, -20f, 1.1f);
            C(-25f, -11f, 3f);
            C(-5.5f, -18f, 1.3f); C(5.5f, 0f, 1.3f); C(-5.5f, 14f, 1.3f); C(5.5f, -26f, 1.3f);
            C(0f, -14f, 3.5f);
            z.Add(Zone.Rect(-14f, 8f, 10.5f, 17.5f));
            z.Add(Zone.Rect(-24f, 19f, 24f, 29f));
            return z;
        }
    }
}
