using UnityEngine;
using SevenDays.Core;
using SevenDays.Look;
using SevenDays.Procedural;

namespace SevenDays.World
{
    /// <summary>
    /// Kit de piezas del mapa (lo que antes era el mapa fijo de "ruinas industriales"), armado solo con primitivas y texturas procedurales: contenedores,
    /// cajas, muros rotos, barreras de concreto, tuberías, barriles con fuego y farolas. Las piezas bajas con
    /// CoverObject sirven de cobertura. Las cajas usan UV por cara a escala real (no se estiran) y cada objeto
    /// varía un poco de tono y de posición de la textura para que no se vean idénticos.
    /// </summary>
    public static class IndustrialRuins
    {
        private static System.Random _rng;
        private static LookSettings _look;

        /// <summary>Posiciones de coberturas y cajas: la sangre y la maleza se colocan cerca de ellas.</summary>
        public static readonly System.Collections.Generic.List<Vector3> Anchors = new System.Collections.Generic.List<Vector3>();

        /// <summary>
        /// Construye las piezas del kit de un mapa EN EL ORDEN de la lista (MapData.props): los tonos y desplazamientos de textura salen de una sola semilla
        /// (MapData.seed) que se consume pieza a pieza, así el mismo JSON da siempre el mismo mapa.
        /// </summary>
        public static void Build(Transform root, LookSettings look, MapData map)
        {
            _rng = new System.Random(map.seed);
            _look = look;
            Anchors.Clear();
            foreach (var p in map.props) BuildProp(root, p);
        }

        private static Vector3 Pos(MapProp p) => new Vector3(p.pos[0], p.pos[1], p.pos[2]);
        private static Vector3 Size(MapProp p) => new Vector3(p.size[0], p.size[1], p.size[2]);
        private static Color Col(MapProp p) => new Color(p.color[0], p.color[1], p.color[2]);

        private static void BuildProp(Transform root, MapProp p)
        {
            switch (p.kind)
            {
                case "ground": BuildGround(root, p); break;
                case "road": Prim.CreateBox(p.name, Pos(p), Size(p), Tex(Col(p), p.surface, 0.25f), root, 3f, default, false); break;   // las marcas viales son calcomanías: RoadMarkings
                case "wall": Prim.CreateBox(p.name, Pos(p), Size(p), Tex(Col(p), p.surface, 0.12f, 0f, true), root, 3f); break;
                case "crumbles": Crumbles(root, p); break;
                case "barrier": Barrier(root, p.name, Pos(p), Size(p), p.rot[1], Col(p), p.surface); break;
                case "crate": Crate(root, p.name, Pos(p), p.size[0], p.rot[1], p.surface, p.cover, Col(p)); break;
                case "container": ContainerAt(root, p.name, Pos(p), p.rot[1], Col(p)); break;
                case "pillar": case "brickwall": BrickBox(root, p.name, Pos(p), Size(p), true, Col(p), p.surface); break;
                case "beam": case "roof":
                {
                    var go = Prim.CreateBox(p.name, Pos(p), Size(p), Tex(Col(p), p.surface, 0.3f, 0.3f, true), root, p.kind == "beam" ? 2f : 3f, UvJitter(), true);
                    if (p.rot[0] != 0f || p.rot[1] != 0f || p.rot[2] != 0f) go.transform.rotation = Quaternion.Euler(p.rot[0], p.rot[1], p.rot[2]);
                    break;
                }
                case "pipe":
                {
                    var go = Prim.Create(PrimitiveType.Cylinder, p.name, Pos(p), Size(p), Tex(Col(p), p.surface, 0.35f, 0.4f, true), root, true); // tubería: sólida (CapsuleCollider)
                    go.transform.rotation = Quaternion.Euler(p.rot[0], p.rot[1], p.rot[2]);
                    break;
                }
                case "barrel": Barrel(root, p); break;
                case "fire_barrel":
                {
                    Prim.Create(PrimitiveType.Cylinder, p.name, Pos(p) + Vector3.up * 0.5f, Size(p), Tex(Col(p), p.surface, 0.3f, 0.35f, true), root);
                    // llama, humo, chispas, luz sin sombras y mancha de quemadura: todo desde el pool de fuegos
                    FireManager.Instance?.Spawn(FireKind.Barrel, Pos(p) + Vector3.up * 1.0f);
                    break;
                }
                case "rubble_field": RubbleField(root, p); break;
                case "lamp": Lamp(root, p); break;
                case "soil": Prim.CreateBox(p.name, Pos(p), Size(p), Prim.GetMaterial(Col(p)), root, 3f, default, true, 0f); break;
                case "boundary":
                {
                    // límite invisible del mapa (el bosque no tiene muros): solo collider
                    var go = Prim.CreateBox(p.name, Pos(p), Size(p), Prim.GetMaterial(Color.black), root, 3f, default, true, 0f);
                    go.GetComponent<MeshRenderer>().enabled = false;
                    break;
                }
                case "trail":
                {
                    var go = Prim.CreateBox(p.name, Pos(p), Size(p), Prim.GetMaterial(Col(p)), root, 2f, default, false, 0f);
                    go.transform.rotation = Quaternion.Euler(0f, p.rot[1], 0f);
                    go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    break;
                }
                case "fence": Fence(root, p); break;
                case "boulder": Boulder(root, p); break;
                case "trunk": FallenTrunk(root, p); break;
                case "caravan": Caravan(root, p); break;
                case "lantern": Lantern(root, p); break;
                default: Debug.LogWarning("[7Days] Pieza de mapa desconocida: " + p.kind); break;
            }
        }

        /// <summary>
        /// Zonas ocupadas por el mapa de ruinas (contenedores, coberturas, barriles, farolas y nave). La vegetación y los
        /// props no se colocan encima. Son datos fijos para que también los use el exportador de JSON del editor.
        /// </summary>
        public static System.Collections.Generic.List<SevenDays.Procedural.Zone> Exclusions()
        {
            var z = MapKit.StreetRuinsExclusions();
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
        private static void BuildGround(Transform root, MapProp p)
        {
            // Piso: mapa de mezcla (UV 0..1 sobre la cara superior) si está listo; si no, el concreto genérico
            if (GroundLibrary.Ready)
                Prim.CreateBox(p.name, Pos(p), Size(p), GroundLibrary.Material, root, p.size[0], default, true, 0f);
            else
                Prim.CreateBox(p.name, Pos(p), Size(p), Tex(Col(p), p.surface), root, 3f);
        }

        // ---------- Bloques que sobresalen de los muros perimetrales (silueta rota) ----------
        // pos.y = altura del muro, size.x = distancia del muro al centro, size.z = mitad del tramo en el que caen, count = cuántos
        private static void Crumbles(Transform root, MapProp p)
        {
            var wall = Tex(Col(p), p.surface, 0.12f, 0f, true);
            float top = p.pos[1], d = p.size[0], span = p.size[2];
            for (int i = 0; i < p.count; i++)
            {
                float t = Rand() * span * 2f - span;
                float h = 0.6f + Rand() * 1.6f;
                float w = 1.2f + Rand() * 2.5f;
                int side = i % 4;
                Vector3 pos = side switch
                {
                    0 => new Vector3(t, top + h / 2f, d),
                    1 => new Vector3(t, top + h / 2f, -d),
                    2 => new Vector3(d, top + h / 2f, t),
                    _ => new Vector3(-d, top + h / 2f, t),
                };
                Vector3 size = side < 2 ? new Vector3(w, h, 1f) : new Vector3(1f, h, w);
                Prim.CreateBox(p.name + " " + i, pos, size, wall, root, 3f, UvJitter(), true);
            }
        }

        // ---------- Coberturas bajas: barreras de concreto y cajas ----------
        private static void Barrier(Transform root, string name, Vector3 pos, Vector3 size, float yaw, Color color, string surface)
        {
            Anchors.Add(new Vector3(pos.x, 0f, pos.z));
            var go = Prim.CreateBox(name, pos, size, Tex(color, surface, 0.12f, 0f, false, Tone()), root, 2f, UvJitter(), true, 0.05f);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<CoverObject>();
        }

        private static void Crate(Transform root, string name, Vector3 pos, float side, float yaw, string kind, bool cover, Color color)
        {
            // tile = lado de la caja: cada cara recibe exactamente una copia de la textura, sin estirarla
            var mat = Tex(color, kind, 0.1f, 0f, false, Tone(0.1f));
            if (cover) Anchors.Add(new Vector3(pos.x, 0f, pos.z));
            var go = Prim.CreateBox(name, pos, Vector3.one * side, mat, root, side, default, true, 0.025f);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cover) go.AddComponent<CoverObject>();
        }

        // ---------- Contenedores ----------
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

        // ---------- Nave semiderruida al fondo (pilares, vigas, techo y muro de ladrillo) ----------
        /// <summary>Ladrillo y revoque gastado; un ladrillo mide ~0.55 m (la textura se repite cada 2.2 m).</summary>
        private static void BrickBox(Transform root, string name, Vector3 pos, Vector3 size, bool collider, Color color, string surface)
            => Prim.CreateBox(name, pos, size, Tex(color, surface, 0.1f, 0f, false, Tone(0.08f)), root, 2.2f, UvJitter(), collider);

        // ---------- Props: barriles y escombros ----------
        private static void Barrel(Transform root, MapProp p)
        {
            var c = Col(p);
            var mat = Tex(c, p.surface, 0.3f, 0.35f, true, Tone(0.1f));
            var b = Prim.Create(PrimitiveType.Cylinder, p.name, Pos(p), Size(p), mat, root);
            // Aros del barril
            var ring = Tex(c * 0.6f, p.surface, 0.3f, 0.5f, true);
            for (int r = -1; r <= 1; r += 2)
                Prim.Create(PrimitiveType.Cylinder, "Ring", new Vector3(0f, r * 0.55f, 0f), new Vector3(1.04f, 0.04f, 1.04f), ring, b.transform, false);
        }

        /// <summary>Escombros pequeños sin colisión (no estorban al CharacterController): count piezas en un cuadrado de size.x por size.z; la franja |x| &lt; size.y (la calle) queda limpia.</summary>
        private static void RubbleField(Transform root, MapProp p)
        {
            var rubble = Tex(Col(p), p.surface, 0.1f, 0f, true);
            float hx = p.size[0] * 0.5f, hz = p.size[2] * 0.5f, clear = p.size[1];
            for (int i = 0; i < p.count; i++)
            {
                float x = p.pos[0] + Rand() * hx * 2f - hx;
                float z = p.pos[2] + Rand() * hz * 2f - hz;
                if (Mathf.Abs(x) < clear) continue; // la calle queda limpia
                float s = 0.15f + Rand() * 0.5f;
                var r = Prim.CreateBox(p.name + " " + i, new Vector3(x, s * 0.3f, z), new Vector3(s * 1.5f, s * 0.6f, s), rubble, root, 1f, UvJitter(), false);
                r.transform.rotation = Quaternion.Euler(Rand() * 25f, Rand() * 360f, Rand() * 25f);
            }
        }

        // ---------- Farolas ----------
        private static Material _bulb;

        /// <summary>Farola con brazo hacia la calle y luz sin sombras. Las de rank &lt; RedFlickerLamps parpadean rojizas con apagones.</summary>
        private static void Lamp(Transform root, MapProp mp)
        {
            int i = mp.name.Length > 5 && int.TryParse(mp.name.Substring(5), out int n) ? n : 0;
            Vector3 p = Pos(mp);
            var metal = Tex(Col(mp), mp.surface, 0.3f, 0.4f, true);
            _bulb = Prim.GetEmissive(new Color(1f, 0.82f, 0.5f), 5f);
            Prim.Create(PrimitiveType.Cylinder, "Lamp Pole " + i, p + Vector3.up * 2.5f, new Vector3(0.14f, 2.5f, 0.14f), metal, root, true);
            float dir = p.x < 0f ? 1f : -1f;
            Prim.Create(PrimitiveType.Cube, "Lamp Arm " + i, p + new Vector3(dir * 0.6f, 5f, 0f), new Vector3(1.3f, 0.1f, 0.1f), metal, root, false);
            var bulbGo = Prim.Create(PrimitiveType.Sphere, "Lamp Bulb " + i, p + new Vector3(dir * 1.2f, 4.92f, 0f), Vector3.one * 0.3f, _bulb, root, false);

            var lightGo = new GameObject("LampLight " + i);
            lightGo.transform.SetParent(root, false);
            lightGo.transform.position = p + new Vector3(dir * 1.2f, 4.7f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.78f, 0.48f);
            l.range = 16f;
            l.intensity = 2.6f;
            l.shadows = LightShadows.None;

            // Farolas defectuosas: parpadeo rojizo con apagones breves
            if (mp.rank >= 0 && mp.rank < _look.Atmosphere.RedFlickerLamps)
            {
                var f = lightGo.AddComponent<FlickerLight>();
                f.RedMix = 0.85f;
                f.Blackouts = true;
                f.Amount = 0.25f;
                f.Bulb = bulbGo.GetComponent<Renderer>();
            }
        }
    
        // ---------- Piezas de los mapas 2 y 3: valla, roca, tronco caído, caravana y farol ----------
        private static readonly System.Collections.Generic.Dictionary<string, Mesh> KitMeshes = new System.Collections.Generic.Dictionary<string, Mesh>();

        private static Mesh KitMesh(string key, System.Func<MeshData> make)
        {
            if (KitMeshes.TryGetValue(key, out var m) && m != null) return m;
            m = BoxMesh.ToMesh(make()); m.name = key; KitMeshes[key] = m;
            return m;
        }

        /// <summary>Valla de tablas: postes, dos travesaños y tablas con huecos. Un solo collider de caja (cobertura a media altura). size.x o size.z = largo.</summary>
        private static void Fence(Transform root, MapProp p)
        {
            var go = new GameObject(p.name);
            go.transform.SetParent(root, false);
            go.transform.position = Pos(p);
            go.transform.rotation = Quaternion.Euler(0f, p.rot[1], 0f);
            go.AddComponent<BoxCollider>().size = Size(p);
            go.AddComponent<CoverObject>();
            bool alongX = p.size[0] >= p.size[2];
            float len = alongX ? p.size[0] : p.size[2], h = p.size[1];
            var wood = Tex(Col(p), SurfaceLibrary.CrateWood, 0.08f, 0f, true, Tone(0.1f));
            Vector3 Along(float t, float y) => alongX ? new Vector3(t, y, 0f) : new Vector3(0f, y, t);
            for (float t = -len / 2f; t <= len / 2f + 0.01f; t += 2f)
                Prim.CreateBox("Fence Post", Along(t, 0.05f), new Vector3(0.1f, h + 0.1f, 0.1f), wood, go.transform, 1f, UvJitter(), false, 0.01f);
            foreach (float y in new[] { -h * 0.25f, h * 0.3f })
                Prim.CreateBox("Fence Rail", Along(0f, y), alongX ? new Vector3(len, 0.08f, 0.05f) : new Vector3(0.05f, 0.08f, len), wood, go.transform, 1.5f, UvJitter(), false, 0.01f);
            int boards = Mathf.FloorToInt(len / 0.22f);
            for (int i = 0; i < boards; i++)
            {
                if (Rand() < 0.12f) continue;   // tabla que falta: se ve a través
                float t = -len / 2f + (i + 0.5f) * len / boards, bh = h * (0.85f + 0.15f * Rand());
                var b = Prim.CreateBox("Fence Board", Along(t, (bh - h) * 0.5f), alongX ? new Vector3(0.16f, bh, 0.025f) : new Vector3(0.025f, bh, 0.16f), wood, go.transform, 1f, UvJitter(), false, 0.004f);
                b.transform.localRotation = Quaternion.Euler(0f, 0f, (Rand() - 0.5f) * 6f);
            }
        }

        /// <summary>Roca con musgo (malla redondeada compartida, 3 formas) y collider de esfera: cobertura natural.</summary>
        private static void Boulder(Transform root, MapProp p)
        {
            int v = Mathf.Max(0, p.rank) % 3;
            var go = new GameObject(p.name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(Pos(p), Quaternion.Euler(0f, p.rot[1], 0f));
            go.transform.localScale = Size(p);
            go.AddComponent<MeshFilter>().sharedMesh = KitMesh("roca " + v, () => ForestGeometry.Boulder(v));
            go.AddComponent<MeshRenderer>().sharedMaterial = Tex(Col(p), p.surface, 0.15f, 0f, true, Tone(0.08f));
            var c = go.AddComponent<SphereCollider>(); c.radius = 0.85f; c.center = new Vector3(0f, 0.1f, 0f);
            go.AddComponent<CoverObject>();
        }

        /// <summary>Tronco caído a lo largo de su X (size = largo, radio, radio) con collider de cápsula: cobertura baja.</summary>
        private static void FallenTrunk(Transform root, MapProp p)
        {
            var go = new GameObject(p.name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(Pos(p), Quaternion.Euler(0f, p.rot[1], (Rand() - 0.5f) * 6f));
            go.transform.localScale = Size(p);
            go.AddComponent<MeshFilter>().sharedMesh = KitMesh("tronco", ForestGeometry.Trunk);
            go.AddComponent<MeshRenderer>().sharedMaterial = Prim.GetTextured(Col(p), SurfaceLibrary.Bark, Vector2.one, 0.05f, 0f, true, 1f);
            var c = go.AddComponent<CapsuleCollider>(); c.direction = 0; c.radius = 0.95f; c.height = 1f + 1.9f * p.size[1] / Mathf.Max(0.1f, p.size[0]);
            go.AddComponent<CoverObject>();
        }

        /// <summary>Caravana oxidada: caja con techo redondeado, ventanas oscuras, puerta, ruedas pinchadas y lanza de enganche. Un collider de caja.</summary>
        private static void Caravan(Transform root, MapProp p)
        {
            var go = new GameObject(p.name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(Pos(p), Quaternion.Euler(0f, p.rot[1], 0f));
            float w = p.size[0], h = p.size[1], l = p.size[2];
            var c = go.AddComponent<BoxCollider>(); c.size = new Vector3(w, h, l); c.center = new Vector3(0f, h * 0.5f + 0.2f, 0f);
            go.AddComponent<CoverObject>();
            var rust = Tex(Col(p), SurfaceLibrary.Container, 0.25f, 0.3f, true, Tone(0.08f));
            var dark = Prim.GetMaterial(new Color(0.05f, 0.06f, 0.07f));
            Prim.CreateBox("Caravan Body", new Vector3(0f, 0.35f + h * 0.42f, 0f), new Vector3(w, h * 0.84f, l), rust, go.transform, 2.4f, UvJitterU(), false, 0.08f);
            var roof = Prim.Create(PrimitiveType.Capsule, "Caravan Roof", new Vector3(0f, 0.35f + h * 0.84f, 0f), new Vector3(w * 0.98f, l * 0.5f, h * 0.25f), rust, go.transform, false);
            roof.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            foreach (float z in new[] { -l * 0.28f, l * 0.18f }) Prim.CreateBox("Caravan Window", new Vector3(w * 0.5f + 0.01f, 0.35f + h * 0.55f, z), new Vector3(0.02f, h * 0.25f, l * 0.18f), dark, go.transform, 1f, default, false, 0f);
            Prim.CreateBox("Caravan Door", new Vector3(-w * 0.5f - 0.01f, 0.35f + h * 0.38f, l * 0.25f), new Vector3(0.02f, h * 0.7f, 0.7f), dark, go.transform, 1f, default, false, 0f);
            foreach (float sx in new[] { -1f, 1f })
            {
                var wheel = Prim.Create(PrimitiveType.Cylinder, "Caravan Wheel", new Vector3(sx * w * 0.48f, 0.3f, 0f), new Vector3(0.6f, 0.08f, 0.6f), dark, go.transform, false);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f + sx * 8f);   // rueda pinchada, algo caída
            }
            Prim.CreateBox("Caravan Hitch", new Vector3(0f, 0.3f, l * 0.5f + 0.6f), new Vector3(0.12f, 0.1f, 1.3f), rust, go.transform, 1f, default, false, 0.01f);
        }

        /// <summary>Farol a pilas sobre un palo: luz cálida pequeña sin sombras que tiembla un poco (las únicas luces del bosque con las fogatas).</summary>
        private static void Lantern(Transform root, MapProp p)
        {
            var go = new GameObject(p.name);
            go.transform.SetParent(root, false);
            go.transform.position = Pos(p);
            Prim.Create(PrimitiveType.Cylinder, "Lantern Post", new Vector3(0f, 0.6f, 0f), new Vector3(0.05f, 0.6f, 0.05f), Prim.GetMaterial(Col(p)), go.transform, false);
            var glass = Prim.Create(PrimitiveType.Sphere, "Lantern Glass", new Vector3(0f, 1.32f, 0f), new Vector3(0.16f, 0.2f, 0.16f), Prim.GetEmissive(new Color(1f, 0.72f, 0.38f), 4f), go.transform, false);
            var lg = new GameObject("LanternLight"); lg.transform.SetParent(go.transform, false); lg.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.7f, 0.4f); l.range = 8f; l.intensity = 1.8f; l.shadows = LightShadows.None;
            var f = lg.AddComponent<FlickerLight>(); f.Amount = 0.12f; f.Speed = 6f; f.Bulb = glass.GetComponent<Renderer>();
        }
    }
}
