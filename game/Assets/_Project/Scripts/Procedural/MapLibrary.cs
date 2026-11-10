using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Generadores (puros y deterministas) de los mapas 2 y 3 con el mismo kit de piezas procedurales. Su resultado se exporta a Resources/Maps/*.json y al seed del
    /// backend; el juego lee los JSON. Botín: las mismas tablas, drops y topes que el mapa 1 (escasez de BALANCE.md); solo cambian los puntos.
    /// </summary>
    public static class MapLibrary
    {
        public const string NeighborhoodId = "barrio_abandonado", ForestId = "bosque_oscuro";

        private static float[] Rgb(float r, float g, float b) => new[] { r, g, b };

        private static MapProp P(string kind, string name, float x, float y, float z, float sx, float sy, float sz, float yaw, float[] color, string surface = "", bool cover = false, int count = 0, int rank = -1)
            => new MapProp { kind = kind, name = name, pos = new[] { x, y, z }, rot = new[] { 0f, yaw, 0f }, size = new[] { sx, sy, sz }, color = color, surface = surface, cover = cover, count = count, rank = rank };

        private static PropEntry E(string type, float x, float z, float yaw, float scale = 1f, int variant = 0, string state = "")
            => new PropEntry { type = type, x = (float)Math.Round(x, 2), z = (float)Math.Round(z, 2), yaw = (float)Math.Round(yaw, 1), scale = (float)Math.Round(scale, 2), variant = variant, state = state };

        private static LootPoint L(string id, float x, float z, string container, string table, int risk, float search, bool once, bool weapon = false)
            => new LootPoint { id = id, x = x, z = z, container = container, table = table, risk = risk, searchSeconds = search, once = once, guaranteeWeapon = weapon };

        /// <summary>Casa pequeña de 2 habitaciones: puerta principal hacia la calle (frontWall), puerta trasera, ventana grande que se salta y ventanas de tiro.</summary>
        public static HouseLayoutData SmallHouse(float x, float z, string frontWall, int seed, int[] frontBlocked, int[] backBlocked, float width = 8f, float depth = 7f)
        {
            string back = frontWall == "west" ? "east" : frontWall == "east" ? "west" : frontWall == "north" ? "south" : "north";
            string side = frontWall == "west" || frontWall == "east" ? "south" : "east";
            string otherSide = side == "south" ? "north" : "west";
            return new HouseLayoutData
            {
                x = x, z = z, yaw = 0f, width = width, depth = depth, wallHeight = 2.8f, wallThickness = 0.25f, interiorX = 0.8f, roofSeed = seed, roofCollapse = 0.35f,
                openings = new[]
                {
                    new HouseOpening { id = "front_door", wall = frontWall, offset = 0.6f, width = 1.1f, height = 2.1f, kind = "door", blockedDays = frontBlocked },
                    new HouseOpening { id = "back_door", wall = back, offset = -0.8f, width = 1.0f, height = 2.0f, kind = "door", blockedDays = backBlocked },
                    new HouseOpening { id = "big_window", wall = side, offset = -1.4f, width = 1.8f, sill = 0.9f, height = 1.2f, kind = "window_large", vaultable = true },
                    new HouseOpening { id = "win_a", wall = otherSide, offset = 1.6f, width = 0.9f, sill = 1.15f, height = 0.8f, kind = "window" },
                    new HouseOpening { id = "inner_door", wall = "interior", offset = 0f, width = 1.0f, height = 2.0f, kind = "door" },
                },
                furniture = new[]
                {
                    new HouseFurniture { kind = "crate", x = -width / 2f + 1.0f, z = depth / 2f - 0.9f },
                    new HouseFurniture { kind = "shelf", x = width / 2f - 1.0f, z = depth / 2f - 0.8f },
                    new HouseFurniture { kind = "table", x = -1.4f, z = -0.6f },
                },
                torches = new[] { new HouseTorch { x = -width / 2f + 0.6f, z = 0f } },
            };
        }

        private static LootLayoutData Loot(List<LootPoint> points)
        {
            var l = LootDefaults.Create(null, null);   // mismas tablas, drops y topes que el mapa 1
            l.points = points.ToArray();
            return l;
        }

        // ================================================================== mapa 2: Barrio abandonado
        /// <summary>
        /// Cuatro casas pequeñas con entradas que el director tapa en días distintos, dos calles que se cruzan, coches, vallas con huecos (rutas de flanqueo),
        /// más vegetación y niebla más densa: combate a media distancia.
        /// </summary>
        public static MapData Neighborhood()
        {
            const float H = 40f;
            var rng = new Random(2024);
            float F() => (float)rng.NextDouble();
            var concrete = Rgb(0.5f, 0.49f, 0.46f); var dark = Rgb(0.42f, 0.41f, 0.4f); var brick = Rgb(0.47f, 0.3f, 0.24f);
            var props = new List<MapProp>
            {
                P("ground", "Ground", 0f, -0.25f, 0f, 82f, 0.5f, 82f, 0f, concrete, "concrete"),
                P("road", "Road", 0f, 0.01f, 0f, 8f, 0.04f, 78f, 0f, Rgb(0.2f, 0.2f, 0.21f), "floor"),
                P("road", "Road", 0f, 0.02f, 0f, 78f, 0.04f, 8f, 0f, Rgb(0.2f, 0.2f, 0.21f), "floor"),
                P("wall", "Wall N", 0f, 1.6f, H + 0.5f, 2f * H + 1f, 3.2f, 1f, 0f, brick, "wall"),
                P("wall", "Wall S", 0f, 1.6f, -H - 0.5f, 2f * H + 1f, 3.2f, 1f, 0f, brick, "wall"),
                P("wall", "Wall E", H + 0.5f, 1.6f, 0f, 1f, 3.2f, 2f * H + 1f, 0f, brick, "wall"),
                P("wall", "Wall W", -H - 0.5f, 1.6f, 0f, 1f, 3.2f, 2f * H + 1f, 0f, brick, "wall"),
                P("crumbles", "Crumble", 0f, 3.2f, 0f, H + 0.5f, 1f, H - 2f, 0f, brick, "wall", count: 22),
            };
            // vallas de los patios (cobertura a media altura) con huecos para flanquear
            var houses = new[]
            {
                SmallHouse(-17f, 15f, "east", 11, new[] { 3, 6 }, new[] { 5 }),
                SmallHouse(17f, 15f, "west", 12, new[] { 4 }, new[] { 2, 6 }),
                SmallHouse(-17f, -15f, "east", 13, new[] { 5, 7 }, new int[0]),
                SmallHouse(17f, -15f, "west", 14, new[] { 2 }, new[] { 4, 7 }),
            };
            int fence = 0;
            foreach (var h in houses)
            {
                float sx = Math.Sign(h.x), sz = Math.Sign(h.z);
                // valla del patio: tramo paralelo a la calle (con hueco en la entrada) y tramo trasero
                props.Add(P("fence", "Fence " + (++fence), h.x - sx * 6.8f, 0.55f, h.z + sz * 3.5f, 0.12f, 1.1f, 5f, 0f, Rgb(0.42f, 0.32f, 0.22f), cover: true));
                props.Add(P("fence", "Fence " + (++fence), h.x - sx * 6.8f, 0.55f, h.z - sz * 4.6f, 0.12f, 1.1f, 3f, 0f, Rgb(0.42f, 0.32f, 0.22f), cover: true));
                props.Add(P("fence", "Fence " + (++fence), h.x + sx * 1.5f, 0.55f, h.z + sz * 7.3f, 9f, 1.1f, 0.12f, 0f, Rgb(0.4f, 0.3f, 0.2f), cover: true));
            }
            // coberturas en la calle: barreras y cajas cerca del cruce
            props.Add(P("barrier", "Barrier 1", -6.5f, 0.6f, 7f, 3.5f, 1.2f, 0.6f, 10f, concrete, "concrete", true));
            props.Add(P("barrier", "Barrier 2", 6.5f, 0.6f, -7f, 3.5f, 1.2f, 0.6f, -12f, concrete, "concrete", true));
            props.Add(P("barrier", "Barrier 3", 7f, 0.6f, 9f, 0.6f, 1.2f, 3.5f, 0f, concrete, "concrete", true));
            props.Add(P("barrier", "Barrier 4", -9f, 0.6f, -6.5f, 0.6f, 1.2f, 3.5f, 0f, concrete, "concrete", true));
            props.Add(P("crate", "Crate 1", -5f, 0.6f, -12f, 1.2f, 1.2f, 1.2f, 25f, Rgb(0.42f, 0.3f, 0.18f), "crate", true));
            props.Add(P("crate", "Crate 2", 5.5f, 0.4f, 13f, 0.8f, 0.8f, 0.8f, -10f, Rgb(0.55f, 0.43f, 0.28f), "cardboard", false));
            props.Add(P("container", "Container A", 30f, 1.3f, 30f, 2.5f, 2.6f, 6f, 35f, Rgb(0.3f, 0.35f, 0.2f), "container", true));
            props.Add(P("container", "Container B", -31f, 1.3f, -29f, 2.5f, 2.6f, 6f, 80f, Rgb(0.5f, 0.2f, 0.15f), "container", true));
            props.Add(P("barrel", "Barrel 0", -6f, 0.5f, 18f, 0.6f, 0.5f, 0.6f, 0f, Rgb(0.4f, 0.18f, 0.12f), "metal"));
            props.Add(P("barrel", "Barrel 1", 25f, 0.5f, -5.5f, 0.6f, 0.5f, 0.6f, 0f, Rgb(0.2f, 0.3f, 0.38f), "metal"));
            props.Add(P("fire_barrel", "Fire Barrel 0", 5.5f, 0f, -20f, 0.6f, 0.5f, 0.6f, 0f, Rgb(0.22f, 0.15f, 0.12f), "metal"));
            props.Add(P("fire_barrel", "Fire Barrel 1", -25f, 0f, 5.5f, 0.6f, 0.5f, 0.6f, 0f, Rgb(0.22f, 0.15f, 0.12f), "metal"));
            props.Add(P("rubble_field", "Rubble", 0f, 0f, 0f, 76f, 4.5f, 76f, 0f, Rgb(0.42f, 0.41f, 0.39f), "concrete", count: 60));
            int[] ranks = { 0, 2, 1, 3 };
            float[][] lamps = { new[] { -5.5f, -5.5f }, new[] { 5.5f, 5.5f }, new[] { -5.5f, 25f }, new[] { 5.5f, -27f } };
            for (int i = 0; i < 4; i++) props.Add(P("lamp", "Lamp " + i, lamps[i][0], 0f, lamps[i][1], 1f, 1f, 1f, 0f, Rgb(0.36f, 0.38f, 0.4f), "metal", rank: ranks[i]));

            // vegetación: más árboles, arbustos y hierba en los patios y fuera de las calles; coches abandonados en las calles
            var veg = new List<PropEntry>();
            var spawns = MapKit.EdgeSpawns(H, 3, 2.5f, (x, z) => Math.Abs(x) < 4.5f || Math.Abs(z) < 4.5f);
            bool Free(float x, float z, float clear)
            {
                if (Math.Abs(x) < 5f || Math.Abs(z) < 5f) return false;                    // calles
                foreach (var s in spawns) if ((x - s.x) * (x - s.x) + (z - s.z) * (z - s.z) < 9f) return false;   // spawns despejados
                foreach (var h in houses) if (Math.Abs(x - h.x) < h.width / 2f + clear && Math.Abs(z - h.z) < h.depth / 2f + clear) return false;
                return true;
            }
            for (int i = 0; i < 70; i++) { float x = F() * 76f - 38f, z = F() * 76f - 38f; if (Free(x, z, 2.5f)) veg.Add(E(PropTypes.TreeDead, x, z, F() * 360f, 0.8f + F() * 0.5f, rng.Next(FoliageGeometry.TreeVariants))); }
            for (int i = 0; i < 40; i++) { double a = F() * Math.PI * 2; float r = 44f + F() * 10f; if (Math.Max(Math.Abs(Math.Cos(a) * r), Math.Abs(Math.Sin(a) * r)) < H + 2f) continue; veg.Add(E(PropTypes.TreeDead, (float)Math.Cos(a) * r, (float)Math.Sin(a) * r, F() * 360f, 1f + F() * 0.5f, rng.Next(FoliageGeometry.TreeVariants))); }
            for (int i = 0; i < 95; i++) { float x = F() * 76f - 38f, z = F() * 76f - 38f; if (Free(x, z, 1f)) veg.Add(E(PropTypes.Bush, x, z, F() * 360f, 0.8f + F() * 0.6f, rng.Next(FoliageGeometry.BushVariants))); }
            for (int i = 0; i < 380; i++) { float x = F() * 78f - 39f, z = F() * 78f - 39f; if (Math.Abs(x) > 4.2f || Math.Abs(z) > 4.2f) veg.Add(E(PropTypes.Grass, x, z, F() * 360f, 0.8f + F() * 0.9f, rng.Next(2))); }
            float[][] cars = { new[] { -2.2f, 16f, 8f }, new[] { 2.4f, -24f, 172f }, new[] { 18f, 2.2f, 95f }, new[] { -26f, -2.4f, -84f }, new[] { 1.8f, 31f, 185f }, new[] { -12f, 1.8f, 100f } };
            for (int i = 0; i < cars.Length; i++) veg.Add(E(PropTypes.CarWreck, cars[i][0], cars[i][1], cars[i][2], 1f, i % 3, i == 3 ? "burning" : ""));
            for (int i = 0; i < 14; i++) { float x = F() * 70f - 35f, z = F() * 70f - 35f; veg.Add(E(PropTypes.Tire, x, z, F() * 360f, 1f, 0)); }
            for (int i = 0; i < 16; i++) { float x = F() * 70f - 35f, z = F() * 70f - 35f; veg.Add(E(PropTypes.Bag, x, z, F() * 360f, 1f, rng.Next(3))); }
            veg.Add(E(PropTypes.SignStop, -5f, -5f, 45f, 1f, 0, "tilted")); veg.Add(E(PropTypes.SignYield, 5f, 5f, 225f, 1f, 0, "upright")); veg.Add(E(PropTypes.SignSpeed, 5f, -30f, 180f, 1f, 0, "bent"));
            veg.Add(E(PropTypes.Campfire, -24f, -24f, 0f)); veg.Add(E(PropTypes.Campfire, 26f, 22f, 0f));

            // botín: muebles de 3 casas, coches, arbustos, escombros, cajas lejanas y algo suelto (unos 20 puntos, como el mapa 1)
            var pts = new List<LootPoint>();
            for (int i = 0; i < 3; i++)
            {
                var h = houses[i];
                pts.Add(L("house" + (i + 1) + "_crate", h.x + h.furniture[0].x, h.z + h.furniture[0].z, "furniture", "mid", 1, 1.5f, true));
                pts.Add(L("house" + (i + 1) + "_shelf", h.x + h.furniture[1].x, h.z + h.furniture[1].z, "furniture", i == 0 ? "high" : "mid", 1, 2f, true, i == 0));
            }
            pts.Add(L("car_1", cars[0][0], cars[0][1], "car", "mid", 1, 2f, false)); 
            pts.Add(L("bush_1", -30f, 10f, "bush", "mid", 1, 1f, false));
            pts.Add(L("rubble_a", -10f, -32f, "rubble", "low", 0, 1.2f, false));
            pts.Add(L("far_crate_a", -35f, 35f, "crate", "high", 2, 2f, true)); pts.Add(L("far_crate_b", 35f, -35f, "crate", "high", 2, 2f, true, true));
            pts.Add(L("mid_crate_a", 12f, 28f, "crate", "mid", 1, 1.5f, false, true)); pts.Add(L("mid_crate_b", -28f, 25f, "crate", "mid", 1, 1.5f, false));
            pts.Add(L("ground_a", 2.5f, -9f, "ground", "ground", 0, 0f, true)); pts.Add(L("ground_b", -9f, 2.6f, "ground", "ground", 0, 0f, true));

            var m = new MapData
            {
                id = NeighborhoodId, name = "Barrio abandonado", baseDifficulty = 2, halfSize = H, seed = 7, playerStart = new[] { 2f, 0.1f, -4f, 0f },
                props = props.ToArray(), houses = houses, vegetation = new PropLayoutData { items = veg.ToArray() }, loot = Loot(pts),
                ambience = new MapAmbience
                {
                    hour = 16f, fogColor = Rgb(0.46f, 0.5f, 0.5f), fogDensity = 0.016f, ambientSky = Rgb(0.46f, 0.52f, 0.56f), ambientEquator = Rgb(0.38f, 0.41f, 0.42f), ambientGround = Rgb(0.2f, 0.21f, 0.2f),
                    sunPitch = 34f, sunYaw = 40f, sunColor = Rgb(0.95f, 0.93f, 0.88f), sunIntensity = 0.85f, shadowStrength = 0.7f, brightness = 1.15f,
                    groundFogLayers = 3, groundFogDensity = 1.4f, groundFogAlpha = 0.09f, hazeBanks = 12, skyDim = 0.45f, ash = 0.5f, smokeColumns = 2, smoke = 1f, sparks = 1f,
                    godRays = 4, godRayIntensity = 0.1f, redFlickerLamps = 1, cracks = 16, weedTufts = 90, puddles = 9, bloodStains = 8, skylineBuildings = 18,
                    dust = true, skyline = true, roadMarkings = false, fireflies = 0f, soundscape = "wind",
                },
            };
            m.spawns = spawns;
            return m;
        }

        // ================================================================== mapa 3: Bosque oscuro
        /// <summary>Claros sin árboles: inicio, cabaña, fogata y caravana (centro x, z y radio).</summary>
        public static readonly float[][] ForestClearings = { new[] { 0f, -32f, 7f }, new[] { 0f, 12f, 10f }, new[] { -22f, -12f, 7f }, new[] { 24f, -18f, 7f }, new[] { 20f, 26f, 6f } };

        /// <summary>
        /// Noche de luna, niebla baja y densa (se ve a 30-40 m), pinos y árboles muertos de varias alturas, troncos caídos, rocas con musgo, helechos y maleza, senderos de
        /// barro, claros, una cabaña como refugio, una caravana oxidada, fogatas y faroles como únicas luces, luciérnagas y sonido de bosque.
        /// </summary>
        public static MapData Forest()
        {
            const float H = 45f;
            var rng = new Random(777);
            float F() => (float)rng.NextDouble();
            var props = new List<MapProp>
            {
                P("soil", "Soil", 0f, -0.25f, 0f, 130f, 0.5f, 130f, 0f, Rgb(0.13f, 0.12f, 0.09f)),
                P("boundary", "Boundary N", 0f, 3f, H + 0.5f, 2f * H + 1f, 6f, 1f, 0f, Rgb(0f, 0f, 0f)),
                P("boundary", "Boundary S", 0f, 3f, -H - 0.5f, 2f * H + 1f, 6f, 1f, 0f, Rgb(0f, 0f, 0f)),
                P("boundary", "Boundary E", H + 0.5f, 3f, 0f, 1f, 6f, 2f * H + 1f, 0f, Rgb(0f, 0f, 0f)),
                P("boundary", "Boundary W", -H - 0.5f, 3f, 0f, 1f, 6f, 2f * H + 1f, 0f, Rgb(0f, 0f, 0f)),
            };
            // senderos de tierra y barro que unen los claros (tramos rectos girados)
            var trail = new List<(float x, float z)> { (0f, -42f), (0f, -32f), (-3f, -20f), (2f, -6f), (0f, 4f) };
            var branchA = new List<(float x, float z)> { (-3f, -20f), (-14f, -15f), (-22f, -12f) };
            var branchB = new List<(float x, float z)> { (2f, -6f), (14f, -11f), (24f, -18f) };
            var branchC = new List<(float x, float z)> { (0f, 18f), (9f, 22f), (20f, 26f), (34f, 30f) };
            var branchD = new List<(float x, float z)> { (-22f, -12f), (-33f, 4f), (-40f, 20f) };
            int seg = 0;
            var trailPts = new List<(float x, float z)>();
            foreach (var path in new[] { trail, branchA, branchB, branchC, branchD })
                for (int i = 0; i + 1 < path.Count; i++)
                {
                    float dx = path[i + 1].x - path[i].x, dz = path[i + 1].z - path[i].z, len = (float)Math.Sqrt(dx * dx + dz * dz);
                    float yaw = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
                    bool mud = seg % 3 == 1;
                    props.Add(P("trail", "Trail " + (++seg), (path[i].x + path[i + 1].x) / 2f, 0.012f + 0.002f * (seg % 2), (path[i].z + path[i + 1].z) / 2f, mud ? 3f : 2.2f, 0.02f, len + 1f, yaw, mud ? Rgb(0.13f, 0.1f, 0.07f) : Rgb(0.24f, 0.19f, 0.13f)));
                    for (float t = 0; t <= 1f; t += 0.1f) trailPts.Add((path[i].x + dx * t, path[i].z + dz * t));
                }
            // cabaña en el claro central: la cabaña es la casa de este mapa (entradas que el director tapa)
            var cabin = SmallHouse(0f, 12f, "south", 31, new[] { 4, 6 }, new[] { 3, 5, 7 }, 7f, 6f);
            cabin.roofCollapse = 0.25f; cabin.interiorX = -1.6f; cabin.furniture[2].x = 0.9f;   // el tabique no cruza la puerta principal
            cabin.torches = new[] { new HouseTorch { x = -2.9f, z = -1.6f } };
            props.Add(P("caravan", "Caravan", 24f, 0f, -18f, 2.3f, 2.4f, 6f, 32f, Rgb(0.42f, 0.33f, 0.24f), "metal", true));
            props.Add(P("lantern", "Lantern 0", -1.6f, 0f, 7.6f, 1f, 1f, 1f, 0f, Rgb(0.2f, 0.18f, 0.15f)));
            props.Add(P("lantern", "Lantern 1", -2.5f, 0f, -28f, 1f, 1f, 1f, 0f, Rgb(0.2f, 0.18f, 0.15f)));
            props.Add(P("lantern", "Lantern 2", 21.5f, 0f, -14.5f, 1f, 1f, 1f, 0f, Rgb(0.2f, 0.18f, 0.15f)));

            bool Clear(float x, float z, float margin)
            {
                foreach (var c in ForestClearings) { float dx = x - c[0], dz = z - c[1]; if (dx * dx + dz * dz < (c[2] + margin) * (c[2] + margin)) return false; }
                foreach (var t in trailPts) { float dx = x - t.x, dz = z - t.z; if (dx * dx + dz * dz < 2.4f * 2.4f + margin) return false; }
                return true;
            }
            var spawns = MapKit.EdgeSpawns(H, 3, 3f, (x, z) => !Clear(x, z, 0f));
            bool NearSpawn(float x, float z) { foreach (var s in spawns) { float dx = x - s.x, dz = z - s.z; if (dx * dx + dz * dz < 16f) return true; } return false; }

            // cobertura natural: troncos caídos y rocas con musgo
            int trunks = 0, rocks = 0;
            for (int i = 0; i < 60 && trunks < 22; i++)
            {
                float x = F() * 84f - 42f, z = F() * 84f - 42f;
                if (!Clear(x, z, 1.5f) || NearSpawn(x, z)) continue;
                float len = 3.5f + F() * 3f, r = 0.32f + F() * 0.2f;
                props.Add(P("trunk", "Fallen Trunk " + (++trunks), x, r * 0.85f, z, len, r, r, F() * 180f, Rgb(0.3f, 0.24f, 0.18f), "bark", true));
            }
            for (int i = 0; i < 80 && rocks < 30; i++)
            {
                float x = F() * 86f - 43f, z = F() * 86f - 43f;
                if (!Clear(x, z, 1f) || NearSpawn(x, z)) continue;
                float s = 0.7f + F() * 1.1f;
                props.Add(P("boulder", "Boulder " + (++rocks), x, s * 0.25f, z, s * 1.2f, s * 0.8f, s, F() * 360f, Rgb(0.3f, 0.33f, 0.27f), "concrete", true, rank: rng.Next(3)));
            }

            // árboles: dentro del mapa (con collider) y un anillo denso fuera del límite que tapa el borde
            var veg = new List<PropEntry>();
            int trees = 0;
            for (int i = 0; i < 1400 && trees < 430; i++)
            {
                float x = F() * 88f - 44f, z = F() * 88f - 44f;
                if (!Clear(x, z, 0.8f) || NearSpawn(x, z)) continue;
                bool dead = F() < 0.28f;
                veg.Add(E(dead ? PropTypes.TreeTall : PropTypes.Pine, x, z, F() * 360f, 0.85f + F() * 0.35f, dead ? rng.Next(ForestGeometry.TallVariants) : rng.Next(ForestGeometry.PineVariants)));
                trees++;
            }
            for (int i = 0; i < 220; i++)
            {
                double a = F() * Math.PI * 2; float r = 47f + F() * 10f;
                if (Math.Max(Math.Abs(Math.Cos(a) * r), Math.Abs(Math.Sin(a) * r)) < H + 1.5f) continue;   // solo fuera del límite (las esquinas del cuadrado)
                veg.Add(E(F() < 0.2f ? PropTypes.TreeTall : PropTypes.Pine, (float)Math.Cos(a) * r, (float)Math.Sin(a) * r, F() * 360f, 0.9f + F() * 0.4f, rng.Next(2)));
            }
            for (int i = 0; i < 160 && veg.Count < 2400; i++) { float x = F() * 88f - 44f, z = F() * 88f - 44f; if (Clear(x, z, -1f) && !NearSpawn(x, z)) veg.Add(E(PropTypes.Bush, x, z, F() * 360f, 0.8f + F() * 0.6f, rng.Next(FoliageGeometry.BushVariants))); }
            for (int i = 0; i < 520; i++) { float x = F() * 88f - 44f, z = F() * 88f - 44f; if (Clear(x, z, -1.6f)) veg.Add(E(PropTypes.Fern, x, z, F() * 360f, 0.8f + F() * 0.7f, rng.Next(2))); }
            for (int i = 0; i < 360; i++) { float x = F() * 88f - 44f, z = F() * 88f - 44f; veg.Add(E(PropTypes.Grass, x, z, F() * 360f, 0.8f + F() * 0.8f, rng.Next(2))); }
            veg.Add(E(PropTypes.Campfire, -22f, -12f, 0f)); veg.Add(E(PropTypes.Campfire, 20f, 26f, 0f)); veg.Add(E(PropTypes.Campfire, 3.5f, -31f, 0f));
            veg.Add(E(PropTypes.CarWreck, 30f, 29f, 110f, 1f, 1));   // coche oxidado al final del sendero
            for (int i = 0; i < 6; i++) veg.Add(E(PropTypes.Bag, F() * 60f - 30f, F() * 60f - 30f, F() * 360f, 1f, rng.Next(3)));

            // botín: muebles de la cabaña, coche, escondites entre arbustos, montones de rocas, cajas lejanas y algo suelto
            var f0 = cabin.furniture[0]; var f1 = cabin.furniture[1];
            var pts = new List<LootPoint>
            {
                L("cabin_crate", cabin.x + f0.x, cabin.z + f0.z, "furniture", "mid", 1, 1.5f, true),
                L("cabin_shelf", cabin.x + f1.x, cabin.z + f1.z, "furniture", "high", 1, 2f, true, true),
                L("car_1", 30f, 29f, "car", "mid", 1, 2f, false),
                L("caravan_crate", 21f, -21f, "crate", "mid", 1, 1.5f, false),
                L("bush_1", -30f, -30f, "bush", "mid", 1, 1f, false), L("bush_2", 33f, -5f, "bush", "low", 1, 1f, false), L("bush_3", -12f, 34f, "bush", "low", 1, 1f, false),
                L("rubble_a", -20f, -16f, "rubble", "low", 0, 1.2f, false), L("rubble_b", 17f, 23f, "rubble", "low", 0, 1.2f, false),
                L("far_crate_a", -38f, 38f, "crate", "high", 2, 2f, true, true), L("far_crate_b", 38f, -38f, "crate", "high", 2, 2f, true),
                L("far_crate_c", -38f, -36f, "crate", "high", 2, 2f, true),
                L("mid_crate_a", 13f, -9f, "crate", "mid", 1, 1.5f, false, true), L("mid_crate_b", -15f, 4f, "crate", "mid", 1, 1.5f, false),
                L("ground_a", 1.5f, -24f, "ground", "ground", 0, 0f, true), L("ground_b", -10f, -16f, "ground", "ground", 0, 0f, true), L("ground_c", 7f, 21f, "ground", "ground", 0, 0f, false),
            };

            return new MapData
            {
                id = ForestId, name = "Bosque oscuro", baseDifficulty = 3, halfSize = H, seed = 9, playerStart = new[] { 0f, 0.1f, -33f, 0f },
                props = props.ToArray(), houses = new[] { cabin }, spawns = spawns, vegetation = new PropLayoutData { items = veg.ToArray() }, loot = Loot(pts),
                ambience = new MapAmbience
                {
                    hour = 22f, fogColor = Rgb(0.13f, 0.17f, 0.26f), fogDensity = 0.045f,
                    ambientSky = Rgb(0.24f, 0.31f, 0.48f), ambientEquator = Rgb(0.15f, 0.19f, 0.27f), ambientGround = Rgb(0.07f, 0.07f, 0.08f),
                    sunPitch = 42f, sunYaw = 120f, sunColor = Rgb(0.6f, 0.72f, 1f), sunIntensity = 0.8f, shadowStrength = 0.6f, brightness = 1.3f,
                    groundFogLayers = 3, groundFogDensity = 1.8f, groundFogAlpha = 0.12f, hazeBanks = 6, skyDim = 0.15f, ash = 0f, smokeColumns = 0, smoke = 0.6f, sparks = 1f,
                    godRays = 6, godRayIntensity = 0.07f, redFlickerLamps = 0, cracks = 0, weedTufts = 0, puddles = 8, bloodStains = 4, skylineBuildings = 0,
                    dust = false, skyline = false, roadMarkings = false, fireflies = 1f, soundscape = "forest",
                },
            };
        }
    }
}
