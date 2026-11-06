using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
using SevenDays.Core;
using SevenDays.Look;
using SevenDays.Procedural;

namespace SevenDays.World
{
    /// <summary>
    /// Coloca la vegetación y los props de abandono a partir de una distribución (PropLayoutData): por defecto la generada
    /// por VegetationLayout, o la de Resources/Layouts/props.json si existe (mapa de la BD). Árboles, arbustos, hierba,
    /// neumáticos y bolsas se dibujan con GPU instancing, sin sombras salvo los árboles si se activa. La vegetación (árboles, arbustos,
    /// hierba) usa material de doble cara (Cull Off); neumáticos y bolsas son sólidos cerrados (Cull Back). Árboles y arbustos
    /// grandes llevan collider y se marcan "no caminable" para el NavMesh; la hierba no tiene collider ni afecta al NavMesh.
    /// </summary>
    public static class Vegetation
    {
        private const int NotWalkableArea = 1;
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();

        public static PropLayoutData BuildLayout(LookSettings look)
        {
            var v = look.Vegetation;
            var fromFile = PropLayoutIO.LoadOverride(v.LayoutResource);
            if (fromFile != null)
            {
                Debug.Log("[7Days] Usando distribución de props de Resources/" + v.LayoutResource + ".json (" + fromFile.items.Length + " piezas)");
                return fromFile;
            }
            var zones = IndustrialRuins.Exclusions();
            var veg = VegetationLayout.Generate(new VegetationParams
            {
                Seed = v.Seed, Density = v.Density, MaxTrees = v.MaxTrees, MaxBushes = v.MaxBushes, MaxGrass = v.MaxGrass, MaxProps = v.MaxProps,
            }, zones);
            var signs = SignLayout.Generate(new SignLayoutParams { Seed = look.Signs.Seed, Density = look.Signs.Density, MaxSigns = look.Signs.MaxSigns }, zones);

            // los carteles genéricos rotos de la vegetación se reemplazan por las señales nuevas; sus extras (hierba, bolsas) van primero
            var merged = new System.Collections.Generic.List<PropEntry>();
            foreach (var e in signs.items) if (!PropTypes.IsSign(e.type)) merged.Add(e);
            foreach (var e in veg.items) if (e.type != PropTypes.SignBroken) merged.Add(e);
            foreach (var e in signs.items) if (PropTypes.IsSign(e.type)) merged.Add(e);
            return new PropLayoutData { items = merged.ToArray() };
        }

        public static void Build(Transform root, LookSettings look, PropLayoutData layout)
        {
            var v = look.Vegetation;
            var caps = new Dictionary<string, int>
            {
                { PropTypes.TreeDead, v.MaxTrees }, { PropTypes.Bush, v.MaxBushes }, { PropTypes.Grass, v.MaxGrass },
                { PropTypes.CarWreck, 12 }, { PropTypes.PoleFallen, 12 }, { PropTypes.Tire, 40 }, { PropTypes.Bag, 40 }, { PropTypes.SignBroken, 16 },
            };
            foreach (var st in PropTypes.SignTypes) caps[st] = look.Signs.MaxSigns;
            var result = PropLayoutValidator.Validate(layout, caps);
            foreach (var err in result.Errors) Debug.LogWarning("[7Days] Prop descartado: " + err);

            var parent = new GameObject("Vegetation").transform;
            parent.SetParent(root, false);

            // Agrupar por tipo y variante: una malla instanciada por grupo
            var groups = new Dictionary<string, List<Matrix4x4>>();
            var obstacles = new GameObject("Obstacles").transform;
            obstacles.SetParent(parent, false);

            var signEntries = new System.Collections.Generic.List<PropEntry>();
            foreach (var e in result.Valid)
            {
                if (PropTypes.IsSign(e.type)) { signEntries.Add(e); continue; }
                var pos = new Vector3(e.x, 0f, e.z);
                var rot = Quaternion.Euler(0f, e.yaw, 0f);
                switch (e.type)
                {
                    case PropTypes.CarWreck: AbandonedProps.Car(parent, pos, e.yaw, e.variant); continue;
                    case PropTypes.PoleFallen: AbandonedProps.FallenPole(parent, pos, e.yaw, e.scale); continue;
                    case PropTypes.SignBroken: AbandonedProps.BrokenSign(parent, pos, e.yaw, e.variant); continue;
                }

                string key = e.type + ":" + e.variant;
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<Matrix4x4>();
                list.Add(Matrix4x4.TRS(pos, rot, Vector3.one * e.scale));

                if (e.type == PropTypes.TreeDead) AddTreeCollider(obstacles, pos, e);
                else if (e.type == PropTypes.Bush && e.variant == 0 && e.scale >= 1.1f) AddBushCollider(obstacles, pos, e);
            }

            TrafficSigns.Build(root, look, signEntries);

            foreach (var kv in groups)
            {
                string[] parts = kv.Key.Split(':');
                string type = parts[0];
                int variant = int.Parse(parts[1]);
                var go = new GameObject("Instanced " + kv.Key);
                go.transform.SetParent(parent, false);
                var batch = go.AddComponent<InstancedBatch>();
                batch.Init(MeshFor(type, variant), MaterialFor(type, variant), kv.Value.ToArray(), type == PropTypes.TreeDead && v.TreeShadows);

                // El relleno pequeño se puede ocultar si el FPS baja; los árboles nunca.
                if (type != PropTypes.TreeDead) AtmosphereBudget.RegisterGroup(go, type == PropTypes.Grass ? 3 : 4);
            }
        }

        // ---------------------------------------------------------------- mallas y materiales
        private static Mesh MeshFor(string type, int variant)
        {
            string key = type + ":" + variant;
            if (Meshes.TryGetValue(key, out var m) && m != null) return m;
            MeshData data;
            switch (type)
            {
                case PropTypes.TreeDead: data = FoliageGeometry.Tree(variant, 1 + variant); break;
                case PropTypes.Bush: data = FoliageGeometry.Bush(variant, 2 + variant); break;
                case PropTypes.Grass: data = FoliageGeometry.GrassPatch(3 + variant); break;
                case PropTypes.Tire: data = FoliageGeometry.Tire(1); break;
                default: data = FoliageGeometry.Bag(variant + 1); break;
            }
            m = BoxMesh.ToMesh(data);
            m.name = key;
            Meshes[key] = m;
            return m;
        }

        private static Material MaterialFor(string type, int variant)
        {
            switch (type)
            {
                case PropTypes.TreeDead: return Prim.GetTextured(new Color(0.5f, 0.45f, 0.4f), SurfaceLibrary.Bark, Vector2.one, 0.05f, 0f, false, 1f, true);
                case PropTypes.Bush: return Prim.GetMaterial(variant == 0 ? new Color(0.30f, 0.33f, 0.15f) : new Color(0.24f, 0.19f, 0.12f), true);
                case PropTypes.Grass: return Prim.GetMaterial(variant == 0 ? new Color(0.32f, 0.38f, 0.17f) : new Color(0.52f, 0.44f, 0.24f), true);
                case PropTypes.Tire: return Prim.GetMaterial(new Color(0.07f, 0.07f, 0.075f));
                default:
                    Color[] bags = { new Color(0.12f, 0.12f, 0.13f), new Color(0.55f, 0.55f, 0.5f), new Color(0.2f, 0.26f, 0.36f) };
                    return Prim.GetMaterial(bags[variant % bags.Length]);
            }
        }

        // ---------------------------------------------------------------- colisión y NavMesh
        private static void AddTreeCollider(Transform parent, Vector3 pos, PropEntry e)
        {
            var go = new GameObject("Tree Collider");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = (e.variant == 2 ? 0.32f : 0.22f) * e.scale;
            col.height = (e.variant == 2 ? 3.2f : 6.5f) * e.scale;
            col.center = new Vector3(0f, col.height * 0.5f, 0f);
            MarkObstacle(go);
        }

        private static void AddBushCollider(Transform parent, Vector3 pos, PropEntry e)
        {
            var go = new GameObject("Bush Collider");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.55f * e.scale;
            col.center = new Vector3(0f, 0.4f * e.scale, 0f);
            MarkObstacle(go);
        }

        /// <summary>Marca el objeto como obstáculo del NavMesh (área "Not Walkable"), para cuando se hornee en la Fase 3.</summary>
        public static void MarkObstacle(GameObject go)
        {
            var mod = go.AddComponent<NavMeshModifier>();
            mod.overrideArea = true;
            mod.area = NotWalkableArea;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Meshes.Clear();
    }
}
