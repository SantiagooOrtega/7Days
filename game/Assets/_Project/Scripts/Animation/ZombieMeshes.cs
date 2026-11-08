using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.Animation
{
    /// <summary>Todo lo que hace falta para vestir un cuerpo de zombi: mallas de 3 niveles de detalle, pesos y poses de reposo, decals de heridas y poses finales de la muerte.</summary>
    public sealed class ZombieMeshBundle
    {
        public string TypeId, VariantId; public int Bucket;
        public ZombieAnatomy Anatomy;
        public ZombieModelResult Model;                    // LOD 0 (esqueleto y huesos extra)
        public Mesh[] Lod = new Mesh[3];
        public int[] Triangles = new int[3];
        public Matrix4x4[] BindPoses;
        public Bounds Bounds;
        public FootShape Foot;
        public readonly List<(WoundPatch patch, Mesh mesh)> Wounds = new List<(WoundPatch, Mesh)>();
        public ZombiePose[] Lying = new ZombiePose[3];     // por ZombieDeathForm, con la altura de la cadera ya ajustada al suelo
    }

    /// <summary>
    /// Mallas de los zombis con caché por (tipo, variante, cubo de altura). La parte pesada (modelo, pesos, proyección de heridas y ajuste al suelo de la muerte) es pura y puede
    /// hacerse en un hilo de fondo (Prewarm); la creación de las Mesh de Unity se hace en el hilo principal al pedirlas.
    /// </summary>
    public static class ZombieMeshes
    {
        private sealed class Pure
        {
            public ZombieAnatomy Anatomy; public ZombieModelResult[] Models = new ZombieModelResult[3]; public ZombieSkinned[] Skinned = new ZombieSkinned[3];
            public List<WoundPatch> Wounds; public ZombiePose[] Lying = new ZombiePose[3]; public ZombieVariantData Variant;
        }

        private static readonly Dictionary<string, Task<Pure>> PureCache = new Dictionary<string, Task<Pure>>();
        private static readonly Dictionary<string, ZombieMeshBundle> Bundles = new Dictionary<string, ZombieMeshBundle>();
        private static List<ZombieVariantData> _variants;
        public static long MeshBytes { get; private set; }

        public static List<ZombieVariantData> Variants
        {
            get
            {
                if (_variants != null) return _variants;
                _variants = ZombieVariants.Defaults();
                var asset = Resources.Load<TextAsset>("Zombies/zombie_variants");   // opcional: JSON propio (docs/ZOMBIES.md)
                if (asset != null)
                {
                    var file = JsonUtility.FromJson<ZombieVariantFile>(asset.text);
                    var errors = file != null ? ZombieLookValidator.Validate(file.variants) : new List<string> { "JSON ilegible" };
                    if (errors.Count == 0) _variants = file.variants;
                    else Debug.LogWarning("[7Days] zombie_variants.json inválido, se usan las variantes por defecto: " + string.Join("; ", errors));
                }
                return _variants;
            }
        }

        public static List<ZombieVariantData> ForType(string typeId) => ZombieVariants.ForType(Variants, typeId);

        private static string Key(string typeId, string variantId, int bucket) => typeId + "|" + variantId + "|" + bucket;

        private static Pure BuildPure(ZombieVariantData v, int bucket)
        {
            var a = ZombieAnatomies.For(v.typeId, bucket);
            var p = new Pure { Anatomy = a, Variant = v };
            for (int lod = 0; lod < 3; lod++) { p.Models[lod] = ZombieModel.Build(a, v, lod); p.Skinned[lod] = ZombieSkinning.Flatten(p.Models[lod], a.Size); }
            p.Wounds = ZombieWounds.Build(p.Models[0], p.Skinned[0], a);
            foreach (ZombieDeathForm form in System.Enum.GetValues(typeof(ZombieDeathForm)))
            {
                var pose = ZombiePoses.Lying(form, p.Models[2].Spec);
                pose.HipsPos = new V3(0f, ZombiePoses.FitToGround(p.Skinned[2], p.Models[2].Spec, pose), 0f);
                p.Lying[(int)form] = pose;
            }
            return p;
        }

        /// <summary>Empieza en hilos de fondo todas las variantes de un tipo en un cubo de altura (para que aparecer un zombi no cueste un tirón).</summary>
        public static void Prewarm(string typeId, int bucket)
        {
            foreach (var v in ForType(typeId))
            {
                string k = Key(typeId, v.id, bucket);
                if (PureCache.ContainsKey(k)) continue;
                var variant = v.Clone();
                PureCache[k] = Task.Run(() => BuildPure(variant, bucket));
            }
        }

        public static ZombieMeshBundle Get(string typeId, string variantId, int bucket)
        {
            string key = Key(typeId, variantId, bucket);
            if (Bundles.TryGetValue(key, out var b) && b.Lod[0] != null) return b;
            var variant = ForType(typeId).Find(x => x.id == variantId) ?? ForType(typeId)[0];
            if (!PureCache.TryGetValue(key, out var task)) { var vc = variant.Clone(); task = Task.FromResult(BuildPure(vc, bucket)); PureCache[key] = task; }
            var pure = task.Result;   // espera si el hilo de fondo aún no terminó
            b = new ZombieMeshBundle { TypeId = typeId, VariantId = variant.id, Bucket = bucket, Anatomy = pure.Anatomy, Model = pure.Models[0], Lying = pure.Lying };
            var skel = pure.Models[0];
            int bones = HumanoidRigSpec.BoneCount + skel.Extras.Count;
            b.BindPoses = new Matrix4x4[bones];
            var rest = pure.Skinned[0].RestBonePositions;
            for (int i = 0; i < bones; i++) b.BindPoses[i] = Matrix4x4.Translate(new Vector3(-rest[i].X, -rest[i].Y, -rest[i].Z));
            for (int lod = 0; lod < 3; lod++)
            {
                b.Lod[lod] = MakeMesh(pure.Skinned[lod], b.BindPoses, key + "|LOD" + lod);
                b.Triangles[lod] = pure.Skinned[lod].Triangles;
                MeshBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(b.Lod[lod]);
            }
            float h = pure.Anatomy.Proportions.height;
            b.Bounds = new Bounds(new Vector3(0f, h * 0.5f, 0.1f), new Vector3(h * 0.95f, h * 1.15f, h * 0.95f));
            b.Foot = MeasureFoot(skel);
            foreach (var w in pure.Wounds)
            {
                var m = BoxMesh.ToMesh(w.Mesh); m.name = "Herida " + w.Spot.Id;
                b.Wounds.Add((w, m));
            }
            Bundles[key] = b;
            return b;
        }

        private static Mesh MakeMesh(ZombieSkinned z, Matrix4x4[] bind, string name)
        {
            int ns = z.Skin.Mesh.Positions.Count, nc = z.Cloth.Mesh.Positions.Count, n = ns + nc;
            var verts = new Vector3[n]; var norms = new Vector3[n]; var tans = new Vector4[n]; var uvs = new Vector2[n]; var bw = new BoneWeight[n];
            void Copy(SkinnedPart part, int off)
            {
                var m = part.Mesh;
                for (int i = 0; i < m.Positions.Count; i++)
                {
                    var p = m.Positions[i]; var nr = m.Normals[i]; var t = m.Tangents[i]; var uv = m.Uvs[i]; var w = part.Weights[i];
                    verts[off + i] = new Vector3(p.X, p.Y, p.Z); norms[off + i] = new Vector3(nr.X, nr.Y, nr.Z); tans[off + i] = new Vector4(t.X, t.Y, t.Z, m.WOf(i)); uvs[off + i] = new Vector2(uv.X, uv.Y);
                    bw[off + i] = new BoneWeight { boneIndex0 = w.B0, weight0 = w.W0, boneIndex1 = w.B1, weight1 = w.W1, boneIndex2 = w.B2, weight2 = w.W2, boneIndex3 = w.B3, weight3 = w.W3 };
                }
            }
            Copy(z.Skin, 0); Copy(z.Cloth, ns);
            var mesh = new Mesh { name = name };
            mesh.vertices = verts; mesh.normals = norms; mesh.tangents = tans; mesh.uv = uvs; mesh.boneWeights = bw; mesh.bindposes = bind;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(z.Skin.Mesh.Triangles, 0);
            var ct = new int[z.Cloth.Mesh.Triangles.Count];
            for (int i = 0; i < ct.Length; i++) ct[i] = z.Cloth.Mesh.Triangles[i] + ns;
            mesh.SetTriangles(ct, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Medida real del pie en el espacio del pie (pose de reposo), como hace RigVisuals con el soldado.</summary>
        private static FootShape MeasureFoot(ZombieModelResult r)
        {
            float minY = 0f, minZ = 0f, maxToeZ = 0f;
            V3 ankle = HumanoidRigSpec.RestPosition(r.Spec, BoneId.LeftFoot);
            V3 toes = HumanoidRigSpec.RestPosition(r.Spec, BoneId.LeftToes);
            foreach (var p in r.Pieces)
            {
                if (p.Extra >= 0 || (p.Bone != BoneId.LeftFoot && p.Bone != BoneId.LeftToes)) continue;
                V3 origin = HumanoidRigSpec.RestPosition(r.Spec, p.Bone);
                foreach (var q in p.Mesh.Positions)
                {
                    V3 local = origin + p.Pivot + q - ankle;
                    minY = Mathf.Min(minY, local.Y); minZ = Mathf.Min(minZ, local.Z);
                    if (p.Bone == BoneId.LeftToes) maxToeZ = Mathf.Max(maxToeZ, local.Z);
                }
            }
            float ball = toes.Z - ankle.Z;
            return new FootShape { Drop = -minY, Heel = -minZ, Ball = ball, Toe = Mathf.Max(0.03f, maxToeZ - ball) };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { PureCache.Clear(); Bundles.Clear(); _variants = null; MeshBytes = 0; }
    }
}
