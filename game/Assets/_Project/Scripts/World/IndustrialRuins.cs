using UnityEngine;
using SevenDays.Core;
using SevenDays.Look;

namespace SevenDays.World
{
    /// <summary>
    /// Mapa de prueba "ruinas industriales" armado solo con primitivas: contenedores, muros rotos,
    /// barreras de concreto, tuberías, barriles con fuego y farolas. Las piezas bajas con CoverObject
    /// sirven de cobertura. Si hay texturas en Resources/Look se aplican al concreto y al metal.
    /// </summary>
    public static class IndustrialRuins
    {
        private static readonly Color Concrete = new Color(0.46f, 0.45f, 0.42f);
        private static readonly Color ConcreteDark = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color Asphalt = new Color(0.17f, 0.17f, 0.18f);
        private static readonly Color Rust = new Color(0.45f, 0.26f, 0.17f);
        private static readonly Color Pipe = new Color(0.25f, 0.27f, 0.28f);

        public static void Build(Transform root)
        {
            var rng = new System.Random(42);
            BuildGround(root);
            BuildPerimeter(root, rng);
            BuildCovers(root);
            BuildContainers(root);
            BuildWarehouse(root);
            BuildProps(root, rng);
            BuildLights(root);
        }

        // ---------- Suelo ----------
        private static void BuildGround(Transform root)
        {
            var size = new Vector3(60f, 0.5f, 60f);
            Prim.Create(PrimitiveType.Cube, "Ground", new Vector3(0f, -0.25f, 0f), size,
                Prim.GetTextured(Concrete, SurfaceLibrary.Concrete, new Vector2(20f, 20f)), root);

            // Franja de asfalto con líneas amarillas (visualmente guía al jugador hacia el frente)
            Prim.Create(PrimitiveType.Cube, "Road", new Vector3(0f, 0.01f, 0f), new Vector3(8f, 0.04f, 58f),
                Prim.GetTextured(Asphalt, SurfaceLibrary.Floor, new Vector2(3f, 19f), 0.25f), root, false);
            for (int i = -6; i <= 6; i++)
                Prim.Create(PrimitiveType.Cube, "Line " + i, new Vector3(0f, 0.04f, i * 4.4f), new Vector3(0.18f, 0.02f, 2.2f),
                    new Color(0.75f, 0.62f, 0.15f), root, false);
        }

        // ---------- Muros perimetrales rotos ----------
        private static void BuildPerimeter(Transform root, System.Random rng)
        {
            var wall = Prim.GetTextured(ConcreteDark, SurfaceLibrary.Concrete, new Vector2(10f, 2f), 0.12f, 0f, true);
            Prim.Create(PrimitiveType.Cube, "Wall N", new Vector3(0f, 2f, 30.5f), new Vector3(61f, 4f, 1f), wall, root);
            Prim.Create(PrimitiveType.Cube, "Wall S", new Vector3(0f, 2f, -30.5f), new Vector3(61f, 4f, 1f), wall, root);
            Prim.Create(PrimitiveType.Cube, "Wall E", new Vector3(30.5f, 2f, 0f), new Vector3(1f, 4f, 61f), wall, root);
            Prim.Create(PrimitiveType.Cube, "Wall W", new Vector3(-30.5f, 2f, 0f), new Vector3(1f, 4f, 61f), wall, root);

            // Bloques que sobresalen en lo alto para romper la silueta recta
            for (int i = 0; i < 18; i++)
            {
                float t = (float)rng.NextDouble() * 56f - 28f;
                float h = 0.6f + (float)rng.NextDouble() * 1.6f;
                float w = 1.2f + (float)rng.NextDouble() * 2.5f;
                int side = i % 4;
                Vector3 pos = side switch
                {
                    0 => new Vector3(t, 4f + h / 2f, 30.5f),
                    1 => new Vector3(t, 4f + h / 2f, -30.5f),
                    2 => new Vector3(30.5f, 4f + h / 2f, t),
                    _ => new Vector3(-30.5f, 4f + h / 2f, t),
                };
                Vector3 scale = side < 2 ? new Vector3(w, h, 1f) : new Vector3(1f, h, w);
                Prim.Create(PrimitiveType.Cube, "Crumble " + i, pos, scale, wall, root, false);
            }
        }

        // ---------- Coberturas bajas (concreto y cajas) ----------
        private static void BuildCovers(Transform root)
        {
            var low = Prim.GetTextured(Concrete, SurfaceLibrary.Concrete, new Vector2(2f, 1f));
            Cover(root, "Barrier 1", new Vector3(-4f, 0.6f, -8f), new Vector3(3.5f, 1.2f, 0.6f), 0f, low);
            Cover(root, "Barrier 2", new Vector3(4f, 0.6f, -6f), new Vector3(3.5f, 1.2f, 0.6f), 15f, low);
            Cover(root, "Barrier 3", new Vector3(-9f, 0.6f, 0f), new Vector3(0.6f, 1.2f, 4f), 0f, low);
            Cover(root, "Barrier 4", new Vector3(0f, 0.6f, 4f), new Vector3(4f, 1.2f, 0.6f), 0f, low);
            Cover(root, "Barrier 5", new Vector3(12f, 0.6f, -10f), new Vector3(0.6f, 1.2f, 3.5f), 0f, low);

            var crate = Prim.GetMaterial(new Color(0.42f, 0.32f, 0.2f));
            Cover(root, "Crate 1", new Vector3(0f, 0.6f, -4f), new Vector3(1.4f, 1.2f, 1.4f), 20f, crate);
            Cover(root, "Crate 2", new Vector3(8f, 0.6f, 2f), new Vector3(1.4f, 1.2f, 1.4f), -10f, crate);
            Cover(root, "Crate 3", new Vector3(-13f, 0.6f, -4f), new Vector3(1.4f, 1.2f, 1.4f), 35f, crate);
            Prim.Create(PrimitiveType.Cube, "Crate Top", new Vector3(8.1f, 1.6f, 2.1f), new Vector3(0.9f, 0.8f, 0.9f), crate, root)
                .transform.rotation = Quaternion.Euler(0f, 25f, 0f);
        }

        // ---------- Contenedores ----------
        private static void BuildContainers(Transform root)
        {
            ContainerAt(root, "Container A", new Vector3(-19f, 1.3f, 4f), 0f, new Color(0.5f, 0.2f, 0.15f));
            ContainerAt(root, "Container B", new Vector3(19f, 1.3f, -2f), 90f, new Color(0.2f, 0.3f, 0.4f));
            ContainerAt(root, "Container C", new Vector3(-14f, 1.3f, 19f), 20f, new Color(0.3f, 0.35f, 0.2f));
            ContainerAt(root, "Container D", new Vector3(17f, 1.3f, 16f), -15f, new Color(0.45f, 0.35f, 0.15f));
            // Contenedor apilado
            var top = ContainerAt(root, "Container E", new Vector3(-19f, 3.9f, 4.4f), 6f, new Color(0.25f, 0.28f, 0.3f));
            top.GetComponent<CoverObject>().enabled = true;
        }

        private static GameObject ContainerAt(Transform root, string name, Vector3 pos, float yaw, Color color)
        {
            var size = new Vector3(2.5f, 2.6f, 6f);
            var mat = Prim.GetTextured(color, SurfaceLibrary.Metal, new Vector2(2f, 1f), 0.3f, 0.3f, true);
            var go = Prim.Create(PrimitiveType.Cube, name, pos, size, mat, root);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<CoverObject>();
            // Costillas verticales para que no se vea como una caja lisa
            var rib = Prim.GetMaterial(color * 0.7f);
            for (int i = -2; i <= 2; i++)
            {
                var r = Prim.Create(PrimitiveType.Cube, "Rib", new Vector3(1.27f, 0f, i * 1.1f), new Vector3(0.08f, 2.4f, 0.12f), rib, go.transform, false);
                r.transform.localScale = new Vector3(0.08f / size.x, 2.4f / size.y, 0.12f / size.z);
                r.transform.localPosition = new Vector3(0.5f + 0.015f, 0f, i * 1.1f / size.z);
            }
            return go;
        }

        // ---------- Nave semiderruida al fondo ----------
        private static void BuildWarehouse(Transform root)
        {
            var steel = Prim.GetTextured(Pipe, SurfaceLibrary.Metal, new Vector2(4f, 1f), 0.3f, 0.3f, true);
            // Pilares de ladrillo y revoque
            for (int i = -2; i <= 2; i++)
            {
                var scale = new Vector3(1.2f, 5.5f, 1.2f);
                Prim.Create(PrimitiveType.Cube, "Pillar " + i, new Vector3(i * 9f, 2.75f, 23f), scale, BrickWall(scale), root);
            }
            // Vigas del techo (parcialmente caído)
            for (int i = -2; i < 2; i++)
            {
                var beam = Prim.Create(PrimitiveType.Cube, "Beam " + i, new Vector3(i * 9f + 4.5f, 5.6f, 23f), new Vector3(8.4f, 0.3f, 0.4f), steel, root, false);
                if (i == 0) beam.transform.rotation = Quaternion.Euler(0f, 0f, -14f);
            }
            for (int i = -1; i <= 1; i++)
                Prim.Create(PrimitiveType.Cube, "Roof " + i, new Vector3(i * 9f, 5.85f, 26.5f), new Vector3(8f, 0.15f, 6.5f), steel, root, false);
            // Muro de fondo con huecos
            var a = new Vector3(11f, 4f, 0.8f);
            Prim.Create(PrimitiveType.Cube, "Back Wall A", new Vector3(-13f, 2f, 28f), a, BrickWall(a), root);
            var b = new Vector3(8f, 2.4f, 0.8f);
            Prim.Create(PrimitiveType.Cube, "Back Wall B", new Vector3(9f, 1.2f, 28f), b, BrickWall(b), root);
        }

        /// <summary>Ladrillo y revoque gastado; un ladrillo mide ~0.55 m (la textura se repite cada 2.2 m).</summary>
        private static Material BrickWall(Vector3 scale)
            => Prim.GetTextured(new Color(0.4f, 0.22f, 0.18f), SurfaceLibrary.Wall, Prim.TilingFor(scale, 2.2f), 0.1f);

        // ---------- Props: tuberías, barriles, escombros ----------
        private static void BuildProps(Transform root, System.Random rng)
        {
            var pipe = Prim.GetTextured(Pipe, SurfaceLibrary.Metal, new Vector2(4f, 1f), 0.35f, 0.4f, true);
            for (int i = 0; i < 3; i++)
            {
                var p = Prim.Create(PrimitiveType.Cylinder, "Pipe " + i, new Vector3(-25f, 0.4f + i * 0.5f, -12f + i * 1.3f),
                    new Vector3(0.45f, 4f, 0.45f), pipe, root, false);
                p.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            var barrel = Prim.GetTextured(new Color(0.35f, 0.18f, 0.12f), SurfaceLibrary.Metal, new Vector2(2f, 1f), 0.3f, 0.35f, true);
            Vector3[] barrels = { new Vector3(-6f, 0.5f, 12f), new Vector3(-5.2f, 0.5f, 12.4f), new Vector3(22f, 0.5f, 8f), new Vector3(-22f, 0.5f, -20f) };
            for (int i = 0; i < barrels.Length; i++)
                Prim.Create(PrimitiveType.Cylinder, "Barrel " + i, barrels[i], new Vector3(0.6f, 0.5f, 0.6f), barrel, root);

            // Barriles encendidos (fuego + luz parpadeante)
            Vector3[] fires = { new Vector3(-7f, 0f, -12f), new Vector3(10f, 0f, 14f) };
            for (int i = 0; i < fires.Length; i++)
            {
                var b = Prim.Create(PrimitiveType.Cylinder, "Fire Barrel " + i, fires[i] + Vector3.up * 0.5f, new Vector3(0.6f, 0.5f, 0.6f),
                    Prim.GetTextured(new Color(0.18f, 0.12f, 0.1f), SurfaceLibrary.Metal, new Vector2(2f, 1f), 0.3f, 0.35f, true), root);
                var lightGo = new GameObject("FireLight");
                lightGo.transform.SetParent(b.transform, false);
                lightGo.transform.position = fires[i] + Vector3.up * 1.4f;
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.55f, 0.2f);
                l.range = 12f;
                l.intensity = 3.2f;
                l.shadows = LightShadows.None;
                lightGo.AddComponent<FlickerLight>();
                Particles.CreateFire(root, fires[i] + Vector3.up * 1.05f);
            }

            // Escombros pequeños sin colisión (no estorban al CharacterController)
            var rubble = Prim.GetMaterial(new Color(0.38f, 0.37f, 0.35f));
            for (int i = 0; i < 70; i++)
            {
                float x = (float)rng.NextDouble() * 56f - 28f;
                float z = (float)rng.NextDouble() * 56f - 28f;
                if (Mathf.Abs(x) < 3.5f) continue; // la calle queda limpia
                float s = 0.15f + (float)rng.NextDouble() * 0.5f;
                var r = Prim.Create(PrimitiveType.Cube, "Rubble " + i, new Vector3(x, s * 0.3f, z), new Vector3(s * 1.5f, s * 0.6f, s), rubble, root, false);
                r.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 25f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 25f);
            }
        }

        // ---------- Farolas ----------
        private static void BuildLights(Transform root)
        {
            Vector3[] posts = { new Vector3(-5.5f, 0f, -18f), new Vector3(5.5f, 0f, 0f), new Vector3(-5.5f, 0f, 14f), new Vector3(5.5f, 0f, -26f) };
            var metal = Prim.GetTextured(Pipe, SurfaceLibrary.Metal, new Vector2(1f, 4f), 0.3f, 0.4f, true);
            var bulb = Prim.GetEmissive(new Color(1f, 0.82f, 0.5f), 5f);
            for (int i = 0; i < posts.Length; i++)
            {
                Vector3 p = posts[i];
                Prim.Create(PrimitiveType.Cylinder, "Lamp Pole " + i, p + Vector3.up * 2.5f, new Vector3(0.14f, 2.5f, 0.14f), metal, root, false);
                float dir = p.x < 0f ? 1f : -1f;
                Prim.Create(PrimitiveType.Cube, "Lamp Arm " + i, p + new Vector3(dir * 0.6f, 5f, 0f), new Vector3(1.3f, 0.1f, 0.1f), metal, root, false);
                Prim.Create(PrimitiveType.Sphere, "Lamp Bulb " + i, p + new Vector3(dir * 1.2f, 4.92f, 0f), Vector3.one * 0.3f, bulb, root, false);

                var lightGo = new GameObject("LampLight " + i);
                lightGo.transform.SetParent(root, false);
                lightGo.transform.position = p + new Vector3(dir * 1.2f, 4.7f, 0f);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.78f, 0.48f);
                l.range = 16f;
                l.intensity = 2.6f;
                l.shadows = LightShadows.None;
                if (i == 2) lightGo.AddComponent<FlickerLight>(); // una farola defectuosa
            }
        }

        private static void Cover(Transform root, string name, Vector3 pos, Vector3 scale, float yaw, Material mat)
        {
            var go = Prim.Create(PrimitiveType.Cube, name, pos, scale, mat, root);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<CoverObject>();
        }
    }
}
