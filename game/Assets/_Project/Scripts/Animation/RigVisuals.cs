using System.Collections.Generic;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.Animation
{
    /// <summary>Paleta de un personaje (uniforme, armadura, piel, acento). Diseño propio: tonos apagados y sucios con un acento naranja o rojo gastado.</summary>
    public struct RigPalette
    {
        public Color Cloth, Armor, Skin, Accent, Boots;

        public static RigPalette Soldier => new RigPalette
        {
            Cloth = new Color(0.33f, 0.35f, 0.3f), Armor = new Color(0.27f, 0.28f, 0.29f), Skin = new Color(0.62f, 0.48f, 0.38f),
            Accent = new Color(0.72f, 0.36f, 0.12f), Boots = new Color(0.16f, 0.14f, 0.12f),
        };
    }

    /// <summary>
    /// Viste el esqueleto con el modelo del soldado (SoldierModel: uniforme con camuflaje, chaleco, protecciones, botas, casco calavera u otra cabeza según la
    /// variante) y sus materiales (CharacterArt: atlas procedurales). Cada pieza es hija de su hueso con el pivote en su articulación, así que sigue la animación
    /// sin romperse. Las mallas se guardan en caché por variante y complexión. Las manos (RigHand) llevan guante con nudillos reforzados.
    /// </summary>
    public static class RigVisuals
    {
        /// <summary>Piezas creadas en total (para el informe de rendimiento).</summary>
        public static int PartCount;

        /// <summary>Radio de la rótula (m): todo lo que rodea la rodilla cabe en él. Arrodillado, la rodilla trasera queda a HalfKneel.RearKneeHeight del suelo.</summary>
        public const float KneeJointRadius = SoldierModel.KneeJointRadius;

        private static readonly Dictionary<string, List<(ModelPiece piece, Mesh mesh)>> Cache = new Dictionary<string, List<(ModelPiece, Mesh)>>();

        /// <summary>Con la variante elegida (CharacterVariantLibrary). La paleta antigua ya no se usa: los colores vienen de la variante.</summary>
        public static void BuildSoldier(ProceduralRig rig, RigPalette pal) => BuildSoldier(rig, CharacterVariantLibrary.Current);

        public static void BuildSoldier(ProceduralRig rig, CharacterVariantData v)
        {
            v = v ?? CharacterVariantLibrary.Current;
            var set = CharacterArt.For(v);
            foreach (var (piece, mesh) in Pieces(rig, v))
            {
                var go = new GameObject(piece.Name);
                go.transform.SetParent(rig.Bone(piece.Bone), false);
                go.transform.localPosition = new Vector3(piece.Pivot.X, piece.Pivot.Y, piece.Pivot.Z);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = set[piece.Slot];
                // las piezas pequeñas (hebillas, portacargadores, rodilleras...) no proyectan sombra: no se nota y ahorra llamadas de sombra
                if (!piece.Shadow) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                PartCount++;
            }
            float k = rig.Proportions.bulk;
            foreach (bool left in new[] { true, false })
            {
                // mano con guante: pulgar + 4 dedos de 2 falanges que agarran el arma con poses
                var rh = RigHand.Build(rig.Bone(left ? BoneId.LeftHand : BoneId.RightHand), !left, set.Gear, k);
                if (left) rig.LeftHandMesh = rh; else rig.RightHandMesh = rh;
            }
            // medida REAL de la bota (no la supuesta): el rig la usa para que ningún vértice baje del suelo al inclinar el pie
            rig.SetFootShape(MeasureFoot(rig.Bone(BoneId.LeftFoot), rig.Bone(BoneId.LeftToes)));
            rig.Variant = v;
        }

        private static List<(ModelPiece, Mesh)> Pieces(ProceduralRig rig, CharacterVariantData v)
        {
            string key = v.id + "|" + JsonUtility.ToJson(v).GetHashCode() + "|" + JsonUtility.ToJson(rig.Proportions);
            if (Cache.TryGetValue(key, out var list) && list.TrueForAll(x => x.Item2 != null)) return list;
            list = new List<(ModelPiece, Mesh)>();
            foreach (var p in SoldierModel.Build(rig.Spec, rig.Proportions, v))
            {
                var mesh = BoxMesh.ToMesh(p.Mesh);
                mesh.name = p.Name;
                list.Add((p, mesh));
            }
            Cache[key] = list;
            return list;
        }

        /// <summary>Triángulos del soldado vestido (modelo + manos), para el informe y las pruebas.</summary>
        public static int Triangles(ProceduralRig rig)
        {
            int n = 0;
            foreach (var mf in rig.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null) n += (int)mf.sharedMesh.GetIndexCount(0) / 3;
            return n;
        }

        /// <summary>Reconstruye esqueleto y mallas con otra complexión o variante (en Play, desde F6), conservando la capa del personaje.</summary>
        public static void Rebuild(ProceduralRig rig, RigProportions p, RigPalette pal) => Rebuild(rig, p, CharacterVariantLibrary.Current);

        public static void Rebuild(ProceduralRig rig, RigProportions p, CharacterVariantData v)
        {
            rig.Rebuild(p);
            BuildSoldier(rig, v);
            int layer = rig.gameObject.layer;
            foreach (var t in rig.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        /// <summary>Mide la bota en el espacio del pie (pose de reposo): caída del tobillo a la suela, talón, bola y largo de la punta.</summary>
        private static FootShape MeasureFoot(Transform foot, Transform toes)
        {
            float minY = 0f, minZ = 0f, maxToeZ = 0f;
            foreach (var mf in foot.GetComponentsInChildren<MeshFilter>())
                foreach (var vert in mf.sharedMesh.vertices)
                {
                    Vector3 p = foot.InverseTransformPoint(mf.transform.TransformPoint(vert));
                    minY = Mathf.Min(minY, p.y); minZ = Mathf.Min(minZ, p.z);
                    if (mf.transform.IsChildOf(toes)) maxToeZ = Mathf.Max(maxToeZ, p.z);
                }
            return new FootShape { Drop = -minY, Heel = -minZ, Ball = toes.localPosition.z, Toe = maxToeZ - toes.localPosition.z };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();
    }
}
