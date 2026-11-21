using UnityEngine;
using SevenDays.Animation;
using SevenDays.Procedural;

namespace SevenDays.UI.Tactical
{
    /// <summary>
    /// Vista previa 3D del personaje elegido en la selección: el modelo REAL (ProceduralRig + RigVisuals, la misma ropa que en partida) sobre un pedestal,
    /// lejos de la escena, con luz principal cálida y luz de contorno detrás. Una cámara propia lo graba a una RenderTexture pequeña con fondo transparente
    /// (las llamas del menú se ven detrás). Gira despacio la cámara alrededor del modelo (el modelo queda quieto en reposo). Sin memoria por frame.
    /// </summary>
    public class CharacterPreview : MonoBehaviour
    {
        public const int Layer = 30;                          // capa libre: solo la ve esta cámara
        public const int Width = 384, Height = 448;
        public static readonly Vector3 Origin = new Vector3(0f, -400f, 0f);
        public const float DegreesPerSecond = 18f;

        private Camera _cam;
        private Transform _orbit, _model;
        private RenderTexture _rt;
        private string _variantId;
        private float _angle = 200f;

        public RenderTexture Texture => _rt;
        public ProceduralRig Rig { get; private set; }
        public string VariantId => _variantId;

        public static CharacterPreview Create(Transform parent)
        {
            var go = new GameObject("Vista previa 3D");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<CharacterPreview>();
            p.Build();
            return p;
        }

        private void Build()
        {
            var root = new GameObject("Escenario previa").transform;
            root.SetParent(transform, false);
            root.position = Origin;
            _rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "Previa personaje", antiAliasing = 2 };
            _rt.Create();

            var ped = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ped.name = "Pedestal";
            Kill(ped.GetComponent<Collider>());
            ped.transform.SetParent(root, false);
            ped.transform.localPosition = new Vector3(0f, -0.06f, 0f); ped.transform.localScale = new Vector3(1.1f, 0.06f, 1.1f);
            ped.GetComponent<MeshRenderer>().sharedMaterial = SevenDays.Core.Prim.GetMaterial(new Color(0.12f, 0.1f, 0.09f));
            var rimRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);   // borde naranja del pedestal
            rimRing.name = "Borde pedestal";
            Kill(rimRing.GetComponent<Collider>());
            rimRing.transform.SetParent(root, false);
            rimRing.transform.localPosition = new Vector3(0f, -0.075f, 0f); rimRing.transform.localScale = new Vector3(1.18f, 0.03f, 1.18f);
            rimRing.GetComponent<MeshRenderer>().sharedMaterial = SevenDays.Core.Prim.GetMaterial(new Color(0.55f, 0.2f, 0.06f));

            _orbit = new GameObject("Órbita").transform;
            _orbit.SetParent(root, false);
            var camGo = new GameObject("Cámara previa");
            camGo.transform.SetParent(_orbit, false);
            camGo.transform.localPosition = new Vector3(0f, 1.15f, 3.4f);
            camGo.transform.localRotation = Quaternion.Euler(6f, 180f, 0f);
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _cam.cullingMask = 1 << Layer; _cam.fieldOfView = 34f; _cam.nearClipPlane = 0.1f; _cam.farClipPlane = 20f;
            _cam.targetTexture = _rt; _cam.allowHDR = false; _cam.allowMSAA = true; _cam.depth = -50;

            // luz principal cálida (delante, arriba) y luz de contorno naranja detrás: siguen a la cámara
            Light(_orbit, "Luz principal", new Vector3(1.6f, 2.6f, 2.6f), new Color(1f, 0.9f, 0.78f), 2.2f, 7f);
            Light(_orbit, "Luz de contorno", new Vector3(-1.2f, 2.2f, -1.8f), new Color(1f, 0.45f, 0.12f), 6f, 6f);
            Light(_orbit, "Contorno 2", new Vector3(1.3f, 1.4f, -1.6f), new Color(1f, 0.6f, 0.3f), 3f, 5f);
            SetLayer(root);
        }

        private static void Light(Transform parent, string name, Vector3 pos, Color c, float intensity, float range)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            l.cullingMask = 1 << Layer;
        }

        private static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        private static void SetLayer(Transform t) { foreach (var x in t.GetComponentsInChildren<Transform>(true)) x.gameObject.layer = Layer; }

        /// <summary>Viste el modelo con la variante (reconstruye solo si cambia).</summary>
        public void Show(CharacterVariantData v)
        {
            if (v == null || v.id == _variantId) return;
            _variantId = v.id;
            if (_model != null) Kill(_model.gameObject);
            var go = new GameObject("Modelo " + v.id);
            go.transform.SetParent(_orbit.parent, false);
            var rig = go.AddComponent<ProceduralRig>();
            rig.Build(ProportionSettings.For(v));
            RigVisuals.BuildSoldier(rig, v);
            RigScenario.Drive(rig, RigScenarioMode.Idle);
            _model = go.transform; Rig = rig;
            SetLayer(_model);
            foreach (var r in _model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Update()
        {
            _angle += DegreesPerSecond * Time.unscaledDeltaTime;
            if (_angle > 360f) _angle -= 360f;
            _orbit.localRotation = Quaternion.Euler(0f, _angle, 0f);
        }

        private void OnDestroy()
        {
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Kill(_rt); }
        }
    }
}
