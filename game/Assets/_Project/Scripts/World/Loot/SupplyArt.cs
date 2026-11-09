using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.World.Loot
{
    /// <summary>
    /// Recursos compartidos de los suministros (se crean una vez): una malla por tipo con 3 submallas (cuerpo, detalle, acento) y su contorno, materiales compartidos por
    /// color (SRP Batcher / instancing), un material de contorno aditivo por tipo cuyo latido se actualiza UNA vez por frame para todos, los iconos de los marcadores
    /// y la columna de luz de los drops.
    /// </summary>
    public static class SupplyArt
    {
        private static readonly Dictionary<SupplyKind, Mesh> Meshes = new Dictionary<SupplyKind, Mesh>();
        private static readonly Dictionary<SupplyKind, Mesh> Outlines = new Dictionary<SupplyKind, Mesh>();
        private static readonly Dictionary<SupplyKind, Material[]> Materials = new Dictionary<SupplyKind, Material[]>();
        private static readonly Dictionary<SupplyKind, Material> OutlineMats = new Dictionary<SupplyKind, Material>();
        private static readonly Dictionary<SupplyKind, Material> IconMats = new Dictionary<SupplyKind, Material>();
        private static readonly Dictionary<int, Material> WeaponIconMats = new Dictionary<int, Material>();
        private static Mesh _quad;
        private static Material _beam;
        private static int _tickFrame = -1;

        public static Color ToColor(float[] c, float k = 1f) => new Color(c[0] * k, c[1] * k, c[2] * k);

        public static Mesh MeshFor(SupplyKind k)
        {
            if (Meshes.TryGetValue(k, out var m) && m != null) return m;
            var d = SupplyModel.Build(k);
            m = SubMeshes(d.Slots, "Suministro " + k);
            Meshes[k] = m;
            Outlines[k] = SubMeshes(new[] { SupplyModel.Outline(Merge(d.Slots), 0.006f) }, "Contorno " + k);
            Materials[k] = new[] { Prim.GetMaterial(ToColor(d.Colors[0])), Prim.GetMaterial(ToColor(d.Colors[1])), Prim.GetEmissive(ToColor(d.Colors[2]), 1.6f) };
            return m;
        }

        public static Mesh OutlineFor(SupplyKind k) { MeshFor(k); return Outlines[k]; }
        public static Material[] MaterialsFor(SupplyKind k) { MeshFor(k); return Materials[k]; }

        /// <summary>Contorno aditivo (solo caras traseras de la malla hinchada) del color del tipo.</summary>
        public static Material OutlineMaterial(SupplyKind k)
        {
            if (OutlineMats.TryGetValue(k, out var m) && m != null) return m;
            m = Transparent(Texture2D.whiteTexture, true, "Contorno " + k);
            m.SetFloat("_Cull", (float)CullMode.Front);
            m.SetColor("_BaseColor", ToColor(SupplyModel.TypeColor(k), 1.5f));
            OutlineMats[k] = m;
            return m;
        }

        /// <summary>Icono del marcador (billboard). Las armas usan el de pistola teñido con su rareza.</summary>
        public static Material IconMaterial(SupplyKind k, Rarity rarity)
        {
            if (k == SupplyKind.Weapon)
            {
                if (WeaponIconMats.TryGetValue((int)rarity, out var wm) && wm != null) return wm;
                wm = Transparent(Icon(SupplyKind.Weapon), false, "Icono arma " + rarity);
                var rc = WeaponCatalog.RarityColor(rarity);
                wm.SetColor("_BaseColor", new Color(rc[0] / Mathf.Max(0.01f, SupplyModel.TypeColor(k)[0]), rc[1] / SupplyModel.TypeColor(k)[1], rc[2] / SupplyModel.TypeColor(k)[2], 1f));
                WeaponIconMats[(int)rarity] = wm;
                return wm;
            }
            if (IconMats.TryGetValue(k, out var m) && m != null) return m;
            m = Transparent(Icon(k), false, "Icono " + k);
            IconMats[k] = m;
            return m;
        }

        public static Mesh Quad
        {
            get
            {
                if (_quad != null) return _quad;
                _quad = new Mesh { name = "Marcador" };
                _quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
                _quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                _quad.triangles = new[] { 0, 2, 1, 1, 2, 3 };
                _quad.RecalculateNormals(); _quad.RecalculateBounds();
                return _quad;
            }
        }

        /// <summary>Columna de luz de los drops: rayo aditivo vertical (sin sombras, sin luz real).</summary>
        public static Material Beam
        {
            get
            {
                if (_beam != null) return _beam;
                _beam = Transparent(SevenDays.Look.ParticleMaterials.Streak.GetTexture("_BaseMap") as Texture2D ?? Texture2D.whiteTexture, true, "Columna de luz");
                _beam.SetFloat("_Cull", (float)CullMode.Off);
                _beam.SetColor("_BaseColor", new Color(1f, 0.85f, 0.45f, 0.55f));
                return _beam;
            }
        }

        /// <summary>Latido del contorno: un cambio de color por tipo y por frame (todos los objetos del mismo tipo laten a la vez).</summary>
        public static void Tick()
        {
            if (_tickFrame == Time.frameCount) return;
            _tickFrame = Time.frameCount;
            foreach (var kv in OutlineMats)
            {
                if (kv.Value == null) continue;
                float p = SupplyVisibility.Pulse(Time.time, (int)kv.Key * 0.13f);
                kv.Value.SetColor("_BaseColor", ToColor(SupplyModel.TypeColor(kv.Key), 0.35f + 1.4f * p));
            }
        }

        private static Texture2D Icon(SupplyKind k)
        {
            var t = new Texture2D(SupplyModel.IconSize, SupplyModel.IconSize, TextureFormat.RGBA32, true) { name = "Icono " + k, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixelData(SupplyModel.Icon(k), 0); t.Apply(true, true);
            return t;
        }

        private static Material Transparent(Texture2D tex, bool additive, string name)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = new Material(shader) { name = name, enableInstancing = true };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", (float)CullMode.Back);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent + 10;
            m.SetOverrideTag("RenderType", "Transparent");
            return m;
        }

        private static MeshData Merge(MeshData[] parts)
        {
            var o = new MeshData();
            foreach (var p in parts)
            {
                int b = o.Positions.Count;
                for (int i = 0; i < p.Positions.Count; i++) { o.Positions.Add(p.Positions[i]); o.Normals.Add(p.Normals[i]); o.Tangents.Add(p.Tangents[i]); o.TangentW.Add(p.WOf(i)); o.Uvs.Add(p.Uvs[i]); }
                foreach (int t in p.Triangles) o.Triangles.Add(b + t);
            }
            return o;
        }

        private static Mesh SubMeshes(MeshData[] parts, string name)
        {
            var merged = Merge(parts);
            var mesh = BoxMesh.ToMesh(merged); mesh.name = name;
            mesh.subMeshCount = parts.Length;
            int b = 0;
            for (int s = 0; s < parts.Length; s++)
            {
                var tris = new int[parts[s].Triangles.Count];
                for (int i = 0; i < tris.Length; i++) tris[i] = parts[s].Triangles[i] + b;
                mesh.SetTriangles(tris, s);
                b += parts[s].Positions.Count;
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Meshes.Clear(); Outlines.Clear(); Materials.Clear(); OutlineMats.Clear(); IconMats.Clear(); WeaponIconMats.Clear(); _quad = null; _beam = null; _tickFrame = -1; }
    }
}
