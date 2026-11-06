using UnityEngine;
using SevenDays.Core;
using SevenDays.Look;
using SevenDays.Procedural;

namespace SevenDays.World
{
    /// <summary>
    /// Mapa de prueba "ruinas industriales" armado solo con primitivas y texturas procedurales: contenedores,
    /// cajas, muros rotos, barreras de concreto, tuberías, barriles con fuego y farolas. Las piezas bajas con
    /// CoverObject sirven de cobertura. Las cajas usan UV por cara a escala real (no se estiran) y cada objeto
    /// varía un poco de tono y de posición de la textura para que no se vean idénticos.
    /// </summary>
    public static class IndustrialRuins
    {
        private static readonly Color Concrete = new Color(0.5f, 0.49f, 0.46f);
        private static readonly Color ConcreteDark = new Color(0.4f, 0.4f, 0.4f);
        private static readonly Color Asphalt = new Color(0.2f, 0.2f, 0.21f);
        private static readonly Color Pipe = new Color(0.36f, 0.38f, 0.4f);

        private static System.Random _rng;
        private static LookSettings _look;

        /// <summary>Posiciones de coberturas y cajas: la sangre y la maleza se colocan cerca de ellas.</summary>
        public static readonly System.Collections.Generic.List<Vector3> Anchors = new System.Collections.Generic.List<Vector3>();

        public static void Build(Transform root, LookSettings look)
        {
            _rng = new System.Random(42);
            _look = look;
            Anchors.Clear();
            BuildGround(root);
            BuildPerimeter(root);
            BuildCovers(root);
            BuildContainers(root);
            BuildWarehouse(root);
            BuildProps(root);
            BuildLights(root);
        }

        /// <summary>
        /// Zonas ocupadas por el mapa de ruinas (contenedores, coberturas, barriles, farolas, blancos y nave). La vegetación y los
        /// props no se colocan encima. Son datos fijos para que también los use el exportador de JSON del editor.
        /// </summary>
        public static System.Collections.Generic.List<SevenDays.Procedural.Zone> Exclusions()
        {
            var z = new System.Collections.Generic.List<SevenDays.Procedural.Zone>();
            void C(float x, float zz, float r) => z.Add(SevenDays.Procedural.Zone.Circle(x, zz, r));
            C(-19f, 4f, 4.4f); C(22.7f, -2f, 4.4f); C(-14f, 19f, 4.4f); C(17f, 16f, 4.4f);      // contenedores
            C(-4f, -8f, 2.4f); C(4f, -6f, 2.4f); C(-9f, 0f, 2.6f); C(0f, 4f, 2.6f); C(12f, -10f, 2.4f); // barreras
            C(0f, -4f, 1.4f); C(-13f, -4f, 1.4f); C(-15f, -3.4f, 1.1f); C(14.5f, 8f, 1.1f); // cajas
            C(-7f, -12f, 1.7f); C(10f, 14f, 1.7f);                                               // barriles con fuego
            C(-6f, 12f, 1.1f); C(-5.2f, 12.4f, 1.1f); C(22f, 8f, 1.1f); C(-22f, -20f, 1.1f);    // barriles
            C(-25f, -11f, 3f);                                                                    // tuberías
            C(-5.5f, -18f, 1.3f); C(5.5f, 0f, 1.3f); C(-5.5f, 14f, 1.3f); C(5.5f, -26f, 1.3f);  // farolas
            C(0f, -14f, 3.5f);                                                                    // aparición del jugador
            z.Add(SevenDays.Procedural.Zone.Rect(-14f, 8f, 10.5f, 17.5f));                        // blancos de práctica
            z.Add(SevenDays.Procedural.Zone.Rect(-24f, 19f, 24f, 29f));                           // nave del fondo
            z.Add(HouseIO.FootprintZone(HouseIO.Load()));                                          // casa central
            return z;
        }

        // ---------- utilidades ----------
        private static float Rand() => (float)_rng.NextDouble();
        private static Vector2 UvJitter() => new Vector2(Rand(), Rand());
        /// <summary>Solo desplaza en horizontal: la textura del contenedor tiene óxido en el borde superior/inferior que no debe moverse.</summary>
        private static Vector2 UvJitterU() => new Vector2(Rand(), 0f);

        /// <summary>Variación de brillo por objeto en pasos de 5% (pocos materiales distintos).</summary>
        private static float Tone(float range = 0.12f) => Mathf.Round((1f + (Rand() * 2f - 1f) * range) * 20f) / 20f;

        private static Material Tex(Color fallback, string name, float smooth = 0.12f, float metallic = 0f, bool tint = false, float tone = 1f)
            => Prim.GetTextured(fallback, name, Vector2.one, smooth, metallic, tint, tone);

        // ---------- Suelo ----------
        private static void BuildGround(Transform root)
        {
            // Piso fuera de la autopista: mapa de mezcla de 60 m (UV 0..1 sobre la cara superior) si está listo; si no, el concreto genérico
            if (GroundLibrary.Ready)
                Prim.CreateBox("Ground", new Vector3(0f, -0.25f, 0f), new Vector3(60f, 0.5f, 60f), GroundLibrary.Material, root, 60f, default, true, 0f);
            else
                Prim.CreateBox("Ground", new Vector3(0f, -0.25f, 0f), new Vector3(60f, 0.5f, 60f), Tex(Concrete, SurfaceLibrary.Concrete), root, 3f);

            // Franja de asfalto; las marcas viales (línea discontinua, cebras, flechas) son calcomanías: ver RoadMarkings
            Prim.CreateBox("Road", new Vector3(0f, 0.01f, 0f), new Vector3(8f, 0.04f, 58f), Tex(Asphalt, SurfaceLibrary.Floor, 0.25f), root, 3f, default, false);
        }

        // ---------- Muros perimetrales rotos ----------
        private static void BuildPerimeter(Transform root)
        {
            var wall = Tex(ConcreteDark, SurfaceLibrary.Concrete, 0.12f, 0f, true);
            Prim.CreateBox("Wall N", new Vector3(0f, 2f, 30.5f), new Vector3(61f, 4f, 1f), wall, root, 3f);
            Prim.CreateBox("Wall S", new Vector3(0f, 2f, -30.5f), new Vector3(61f, 4f, 1f), wall, root, 3f);
            Prim.CreateBox("Wall E", new Vector3(30.5f, 2f, 0f), new Vector3(1f, 4f, 61f), wall, root, 3f);
            Prim.CreateBox("Wall W", new Vector3(-30.5f, 2f, 0f), new Vector3(1f, 4f, 61f), wall, root, 3f);

            // Bloques que sobresalen arriba para romper la silueta recta
            for (int i = 0; i < 18; i++)
            {
                float t = Rand() * 56f - 28f;
                float h = 0.6f + Rand() * 1.6f;
                float w = 1.2f + Rand() * 2.5f;
                int side = i % 4;
                Vector3 pos = side switch
                {
                    0 => new Vector3(t, 4f + h / 2f, 30.5f),
                    1 => new Vector3(t, 4f + h / 2f, -30.5f),
                    2 => new Vector3(30.5f, 4f + h / 2f, t),
                    _ => new Vector3(-30.5f, 4f + h / 2f, t),
                };
                Vector3 size = side < 2 ? new Vector3(w, h, 1f) : new Vector3(1f, h, w);
                Prim.CreateBox("Crumble " + i, pos, size, wall, root, 3f, UvJitter(), false);
            }
        }

        // ---------- Coberturas bajas: barreras de concreto y cajas ----------
        private static void BuildCovers(Transform root)
        {
            Barrier(root, "Barrier 1", new Vector3(-4f, 0.6f, -8f), new Vector3(3.5f, 1.2f, 0.6f), 0f);
            Barrier(root, "Barrier 2", new Vector3(4f, 0.6f, -6f), new Vector3(3.5f, 1.2f, 0.6f), 15f);
            Barrier(root, "Barrier 3", new Vector3(-9f, 0.6f, 0f), new Vector3(0.6f, 1.2f, 4f), 0f);
            Barrier(root, "Barrier 4", new Vector3(0f, 0.6f, 4f), new Vector3(4f, 1.2f, 0.6f), 0f);
            Barrier(root, "Barrier 5", new Vector3(12f, 0.6f, -10f), new Vector3(0.6f, 1.2f, 3.5f), 0f);

            // Cajas cúbicas: la textura es de una cara completa (marco, tablas, riostra, tornillos), 1 repetición por cara
            Crate(root, "Crate 1", new Vector3(0f, 0.6f, -4f), 1.2f, 20f, SurfaceLibrary.CrateWood, true);
            Crate(root, "Crate 3", new Vector3(-13f, 0.6f, -4f), 1.2f, 35f, SurfaceLibrary.Cardboard, true);
            Crate(root, "Crate 4", new Vector3(-15f, 0.4f, -3.4f), 0.8f, 10f, SurfaceLibrary.Cardboard, false);
            Crate(root, "Crate 5", new Vector3(14.5f, 0.4f, 8f), 0.8f, -25f, SurfaceLibrary.CrateWood, false);
        }

        private static void Barrier(Transform root, string name, Vector3 pos, Vector3 size, float yaw)
        {
            Anchors.Add(new Vector3(pos.x, 0f, pos.z));
            var go = Prim.CreateBox(name, pos, size, Tex(Concrete, SurfaceLibrary.Concrete, 0.12f, 0f, false, Tone()), root, 2f, UvJitter(), true, 0.05f);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<CoverObject>();
        }

        private static void Crate(Transform root, string name, Vector3 pos, float side, float yaw, string kind, bool cover)
        {
            // tile = lado de la caja: cada cara recibe exactamente una copia de la textura, sin estirarla
            var mat = Tex(kind == SurfaceLibrary.Cardboard ? new Color(0.55f, 0.43f, 0.28f) : new Color(0.42f, 0.3f, 0.18f), kind, 0.1f, 0f, false, Tone(0.1f));
            if (cover) Anchors.Add(new Vector3(pos.x, 0f, pos.z));
            var go = Prim.CreateBox(name, pos, Vector3.one * side, mat, root, side, default, true, 0.025f);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cover) go.AddComponent<CoverObject>();
        }

        // ---------- Contenedores ----------
        private static void BuildContainers(Transform root)
        {
            ContainerAt(root, "Container A", new Vector3(-19f, 1.3f, 4f), 0f, new Color(0.5f, 0.2f, 0.15f));
            ContainerAt(root, "Container B", new Vector3(22.7f, 1.3f, -2f), 90f, new Color(0.2f, 0.3f, 0.4f)); // movido para dejar sitio a la casa
            ContainerAt(root, "Container C", new Vector3(-14f, 1.3f, 19f), 20f, new Color(0.3f, 0.35f, 0.2f));
            ContainerAt(root, "Container D", new Vector3(17f, 1.3f, 16f), -15f, new Color(0.45f, 0.35f, 0.15f));
            ContainerAt(root, "Container E", new Vector3(-19f, 3.9f, 4.4f), 6f, new Color(0.25f, 0.28f, 0.3f)); // apilado
        }

        private static void ContainerAt(Transform root, string name, Vector3 pos, float yaw, Color color)
        {
            var size = new Vector3(2.5f, 2.6f, 6f);
            // Chapa corrugada con óxido y chorreras; 2.6 m por repetición = alto real, sin estirar
            var mat = Tex(color, SurfaceLibrary.Container, 0.3f, 0.3f, true, Tone(0.1f));
            var go = Prim.CreateBox(name, pos, size, mat, root, 2.6f, UvJitterU(), true, 0.025f);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<CoverObject>();
            ContainerDetails(go.transform, size, color);
        }

        /// <summary>Marco (postes y rieles de acero oxidado), barras de la puerta y números estarcidos.</summary>
        private static void ContainerDetails(Transform c, Vector3 size, Color color)
        {
            var frame = Tex(color * 0.45f, SurfaceLibrary.Metal, 0.3f, 0.5f, true, 0.85f);
            float hx = size.x / 2f, hy = size.y / 2f, hz = size.z / 2f;

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                    Prim.CreateBox("Post", new Vector3(sx * hx, 0f, sz * hz), new Vector3(0.16f, size.y + 0.06f, 0.16f), frame, c, 1f, UvJitter(), false);
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    Prim.CreateBox("Rail", new Vector3(sx * hx, sy * hy, 0f), new Vector3(0.12f, 0.12f, size.z), frame, c, 1f, UvJitter(), false);
                    Prim.CreateBox("Rail", new Vector3(0f, sy * hy, sx * hz), new Vector3(size.x, 0.12f, 0.12f), frame, c, 1f, UvJitter(), false);
                }
            }

            // Barras de cierre de la puerta (extremo +Z)
            for (int i = -1; i <= 1; i += 2)
                Prim.CreateBox("Door Bar", new Vector3(i * 0.5f, 0f, hz + 0.03f), new Vector3(0.05f, size.y - 0.2f, 0.05f), frame, c, 1f, UvJitter(), false);

            // Número estarcido en el lado +X: bloques de pintura blanca desgastada
            var paint = Prim.GetMaterial(new Color(0.7f, 0.68f, 0.62f));
            for (int i = 0; i < 6; i++)
            {
                if (Rand() < 0.2f) continue; // pintura caída
                Prim.CreateBox("Stencil", new Vector3(hx + 0.006f, 0.85f, -1.75f + i * 0.34f), new Vector3(0.012f, 0.24f, 0.14f), paint, c, 1f, default, false);
            }
        }

        // ---------- Nave semiderruida al fondo ----------
        private static void BuildWarehouse(Transform root)
        {
            var steel = Tex(Pipe, SurfaceLibrary.Metal, 0.3f, 0.3f, true);
            for (int i = -2; i <= 2; i++)
                BrickBox(root, "Pillar " + i, new Vector3(i * 9f, 2.75f, 23f), new Vector3(1.2f, 5.5f, 1.2f), true);

            // Vigas del techo (parcialmente caído)
            for (int i = -2; i < 2; i++)
            {
                var beam = Prim.CreateBox("Beam " + i, new Vector3(i * 9f + 4.5f, 5.6f, 23f), new Vector3(8.4f, 0.3f, 0.4f), steel, root, 2f, UvJitter(), false);
                if (i == 0) beam.transform.rotation = Quaternion.Euler(0f, 0f, -14f);
            }
            for (int i = -1; i <= 1; i++)
                Prim.CreateBox("Roof " + i, new Vector3(i * 9f, 5.85f, 26.5f), new Vector3(8f, 0.15f, 6.5f), steel, root, 3f, UvJitter(), false);

            // Muro de fondo con huecos
            BrickBox(root, "Back Wall A", new Vector3(-13f, 2f, 28f), new Vector3(11f, 4f, 0.8f), true);
            BrickBox(root, "Back Wall B", new Vector3(9f, 1.2f, 28f), new Vector3(8f, 2.4f, 0.8f), true);
        }

        /// <summary>Ladrillo y revoque gastado; un ladrillo mide ~0.55 m (la textura se repite cada 2.2 m).</summary>
        private static void BrickBox(Transform root, string name, Vector3 pos, Vector3 size, bool collider)
            => Prim.CreateBox(name, pos, size, Tex(new Color(0.45f, 0.28f, 0.22f), SurfaceLibrary.Wall, 0.1f, 0f, false, Tone(0.08f)), root, 2.2f, UvJitter(), collider);

        // ---------- Props: tuberías, barriles, escombros ----------
        private static void BuildProps(Transform root)
        {
            var pipe = Tex(Pipe, SurfaceLibrary.Metal, 0.35f, 0.4f, true);
            for (int i = 0; i < 3; i++)
            {
                var p = Prim.Create(PrimitiveType.Cylinder, "Pipe " + i, new Vector3(-25f, 0.4f + i * 0.5f, -12f + i * 1.3f),
                    new Vector3(0.45f, 4f, 0.45f), pipe, root, false);
                p.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            Color[] barrelColors = { new Color(0.4f, 0.18f, 0.12f), new Color(0.2f, 0.3f, 0.38f), new Color(0.35f, 0.33f, 0.2f) };
            Vector3[] barrels = { new Vector3(-6f, 0.5f, 12f), new Vector3(-5.2f, 0.5f, 12.4f), new Vector3(22f, 0.5f, 8f), new Vector3(-22f, 0.5f, -20f) };
            for (int i = 0; i < barrels.Length; i++)
            {
                var mat = Tex(barrelColors[i % barrelColors.Length], SurfaceLibrary.Metal, 0.3f, 0.35f, true, Tone(0.1f));
                var b = Prim.Create(PrimitiveType.Cylinder, "Barrel " + i, barrels[i], new Vector3(0.6f, 0.5f, 0.6f), mat, root);
                // Aros del barril
                var ring = Tex(barrelColors[i % barrelColors.Length] * 0.6f, SurfaceLibrary.Metal, 0.3f, 0.5f, true);
                for (int r = -1; r <= 1; r += 2)
                    Prim.Create(PrimitiveType.Cylinder, "Ring", new Vector3(0f, r * 0.55f, 0f), new Vector3(1.04f, 0.04f, 1.04f), ring, b.transform, false);
            }

            // Barriles encendidos (fuego + luz parpadeante)
            Vector3[] fires = { new Vector3(-7f, 0f, -12f), new Vector3(10f, 0f, 14f) };
            for (int i = 0; i < fires.Length; i++)
            {
                var b = Prim.Create(PrimitiveType.Cylinder, "Fire Barrel " + i, fires[i] + Vector3.up * 0.5f, new Vector3(0.6f, 0.5f, 0.6f),
                    Tex(new Color(0.22f, 0.15f, 0.12f), SurfaceLibrary.Metal, 0.3f, 0.35f, true), root);
                // llama, humo, chispas, luz sin sombras y mancha de quemadura: todo desde el pool de fuegos
                FireManager.Instance?.Spawn(FireKind.Barrel, fires[i] + Vector3.up * 1.0f);
            }

            // Escombros pequeños sin colisión (no estorban al CharacterController)
            var rubble = Tex(new Color(0.42f, 0.41f, 0.39f), SurfaceLibrary.Concrete, 0.1f, 0f, true);
            for (int i = 0; i < 70; i++)
            {
                float x = Rand() * 56f - 28f;
                float z = Rand() * 56f - 28f;
                if (Mathf.Abs(x) < 3.5f) continue; // la calle queda limpia
                float s = 0.15f + Rand() * 0.5f;
                var r = Prim.CreateBox("Rubble " + i, new Vector3(x, s * 0.3f, z), new Vector3(s * 1.5f, s * 0.6f, s), rubble, root, 1f, UvJitter(), false);
                r.transform.rotation = Quaternion.Euler(Rand() * 25f, Rand() * 360f, Rand() * 25f);
            }
        }

        // ---------- Farolas ----------
        private static void BuildLights(Transform root)
        {
            Vector3[] posts = { new Vector3(-5.5f, 0f, -18f), new Vector3(5.5f, 0f, 0f), new Vector3(-5.5f, 0f, 14f), new Vector3(5.5f, 0f, -26f) };
            var metal = Tex(Pipe, SurfaceLibrary.Metal, 0.3f, 0.4f, true);
            var bulb = Prim.GetEmissive(new Color(1f, 0.82f, 0.5f), 5f);
            for (int i = 0; i < posts.Length; i++)
            {
                Vector3 p = posts[i];
                Prim.Create(PrimitiveType.Cylinder, "Lamp Pole " + i, p + Vector3.up * 2.5f, new Vector3(0.14f, 2.5f, 0.14f), metal, root, false);
                float dir = p.x < 0f ? 1f : -1f;
                Prim.Create(PrimitiveType.Cube, "Lamp Arm " + i, p + new Vector3(dir * 0.6f, 5f, 0f), new Vector3(1.3f, 0.1f, 0.1f), metal, root, false);
                var bulbGo = Prim.Create(PrimitiveType.Sphere, "Lamp Bulb " + i, p + new Vector3(dir * 1.2f, 4.92f, 0f), Vector3.one * 0.3f, bulb, root, false);

                var lightGo = new GameObject("LampLight " + i);
                lightGo.transform.SetParent(root, false);
                lightGo.transform.position = p + new Vector3(dir * 1.2f, 4.7f, 0f);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.78f, 0.48f);
                l.range = 16f;
                l.intensity = 2.6f;
                l.shadows = LightShadows.None;

                // Farolas defectuosas: parpadeo rojizo con apagones breves (las primeras RedFlickerLamps de esta lista)
                int[] defective = { 2, 0, 3, 1 };
                for (int d = 0; d < Mathf.Min(_look.Atmosphere.RedFlickerLamps, defective.Length); d++)
                {
                    if (defective[d] != i) continue;
                    var f = lightGo.AddComponent<FlickerLight>();
                    f.RedMix = 0.85f;
                    f.Blackouts = true;
                    f.Amount = 0.25f;
                    f.Bulb = bulbGo.GetComponent<Renderer>();
                }
            }
        }
    }
}
