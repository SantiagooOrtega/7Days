using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.Animation
{
    /// <summary>
    /// Mano procedural con palma, pulgar y 4 dedos de 2 falanges cada uno, colgada del hueso de la mano. Ejes locales: los dedos van hacia -Y, la palma mira a -X
    /// (en las dos manos), el pulgar sale por +Z en la derecha y por -Z en la izquierda. Las falanges se doblan hacia la palma. La pose (HandPose) se suaviza.
    /// </summary>
    public class RigHand : MonoBehaviour
    {
        public bool IsRight;
        public HandPose Current;
        private readonly Transform[] _base = new Transform[5], _tip = new Transform[5];
        private Quaternion _thumbRest;

        private const float PalmLength = HandWrap.PalmLength, PalmWidth = HandWrap.PalmWidth, PalmThickness = HandWrap.PalmThickness;
        /// <summary>Escala de la mano (grosor del personaje) y radio del mango que sostiene ahora (lo fija el agarre).</summary>
        public float Scale { get; private set; } = 1f;
        public float GripRadius;
        /// <summary>Dónde queda el eje del mango que sostiene esta mano (mundo): delante de la palma, a GripRadius de su cara.</summary>
        public Vector3 GripCenter { get { var a = HandWrap.Anchor(GripRadius, Scale); return transform.TransformPoint(new Vector3(a.X, a.Y, a.Z)); } }

        public static RigHand Build(Transform handBone, bool right, Material glove, float bulk)
        {
            var root = new GameObject("Hand").transform;
            root.SetParent(handBone, false);
            var h = root.gameObject.AddComponent<RigHand>();
            h.IsRight = right;
            float k = Mathf.Clamp(bulk, 0.8f, 1.4f);
            h.Scale = k;
            Part(root, PrimitiveType.Cube, new Vector3(0f, -PalmLength * 0.5f, 0f), new Vector3(PalmThickness, PalmLength, PalmWidth) * k, glove);
            // nudillos reforzados en el dorso (+X: la palma mira a -X), lejos de lo que agarra
            Part(root, PrimitiveType.Cube, new Vector3((PalmThickness * 0.5f + 0.004f) * k, -PalmLength * 0.84f * k, 0f), new Vector3(0.008f, 0.02f, PalmWidth * 0.92f) * k, glove, CharacterTextures.GKnuckle);
            float thumbSide = right ? 1f : -1f;
            // dedos: índice junto al pulgar
            float[] z = HandWrap.Z, l1 = HandWrap.L1, l2 = HandWrap.L2;
            for (int i = 0; i < 4; i++)
            {
                var b = Pivot(root, new Vector3(0f, -PalmLength, z[i] * thumbSide) * k);
                Part(b, PrimitiveType.Cube, new Vector3(0f, -l1[i] * 0.5f, 0f), new Vector3(0.019f, l1[i], 0.018f) * k, glove);
                var t = Pivot(b, new Vector3(0f, -l1[i], 0f) * k);
                Part(t, PrimitiveType.Cube, new Vector3(0f, -l2[i] * 0.5f, 0f), new Vector3(0.017f, l2[i], 0.016f) * k, glove);
                h._base[i + 1] = b; h._tip[i + 1] = t;
            }
            // pulgar: sale del lado de la palma y apunta hacia los dedos
            var tb = Pivot(root, new Vector3(-0.005f, -0.025f, 0.042f * thumbSide) * k);
            tb.localRotation = Quaternion.Euler(0f, 0f, 0f) * Quaternion.AngleAxis(-35f * thumbSide, Vector3.right);
            Part(tb, PrimitiveType.Cube, new Vector3(0f, -0.022f, 0f), new Vector3(0.02f, 0.044f, 0.02f) * k, glove);
            var tt = Pivot(tb, new Vector3(0f, -0.044f, 0f) * k);
            Part(tt, PrimitiveType.Cube, new Vector3(0f, -0.016f, 0f), new Vector3(0.018f, 0.032f, 0.018f) * k, glove);
            h._base[0] = tb; h._tip[0] = tt; h._thumbRest = tb.localRotation;
            h.Current = HandPoses.For(HandGrip.Relaxed);
            h.Apply(h.Current);
            return h;
        }

        /// <summary>Lleva la mano hacia la pose pedida con suavidad (velocidad: cuánto se acerca por segundo).</summary>
        public void Blend(HandPose target, float dt, float speed = 18f)
        {
            Current = HandPose.Lerp(Current, target, 1f - Mathf.Exp(-speed * dt));
            Apply(Current);
        }

        public void Apply(HandPose p)
        {
            for (int i = 1; i < 5; i++)
            {
                var f = p[i];
                // doblar hacia la palma (-X): rotación negativa alrededor de Z local
                _base[i].localRotation = Quaternion.Euler(0f, 0f, -Mathf.Clamp(f.Base, 0f, HandPoses.MaxCurl));
                _tip[i].localRotation = Quaternion.Euler(0f, 0f, -Mathf.Clamp(f.Tip, 0f, HandPoses.MaxCurl));
            }
            float side = IsRight ? 1f : -1f;
            _base[0].localRotation = _thumbRest * Quaternion.AngleAxis(p.ThumbOpposition * side, Vector3.up) * Quaternion.Euler(0f, 0f, -p.Thumb.Base * 0.6f);
            _tip[0].localRotation = Quaternion.Euler(0f, 0f, -p.Thumb.Tip * 0.6f);
        }

        private static Transform Pivot(Transform parent, Vector3 pos)
        {
            var t = new GameObject("joint").transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Mesh> Meshes = new System.Collections.Generic.Dictionary<string, Mesh>();

        /// <summary>Pieza del guante: caja con bisel del tamaño pedido (escala 1) con sus UV en la celda de guante (o de nudillos) del atlas de equipo.</summary>
        private static void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 size, Material m, int cell = CharacterTextures.GGlove)
        {
            string key = size.x.ToString("0.####") + "|" + size.y.ToString("0.####") + "|" + size.z.ToString("0.####") + "|" + cell;
            if (!Meshes.TryGetValue(key, out var mesh) || mesh == null)
            {
                var mb = new MeshBuilder();
                mb.Box(new V3(0f, 0f, 0f), new V3(size.x, size.y, size.z), new V3(0f, 0f, 0f), Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.22f,
                    CharacterTextures.Cell(AtlasKind.Gear, cell), 0.1f);
                mesh = BoxMesh.ToMesh(mb.Data);
                mesh.name = "Guante " + key;
                Meshes[key] = mesh;
            }
            var go = new GameObject("finger");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
