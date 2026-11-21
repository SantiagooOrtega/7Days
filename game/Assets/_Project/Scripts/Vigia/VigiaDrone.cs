using UnityEngine;
using UnityEngine.Rendering;
using SevenDays.Core;
using SevenDays.Combat;

namespace SevenDays.Vigia
{
    /// <summary>
    /// Cuerpo de VIGÍA: una cámara de seguridad flotante hecha con primitivas (carcasa metálica redondeada, visera, anillo luminoso y lente con un iris de 6 láminas
    /// que se abre al hablar o vigilar y se cierra en calma). Flota junto al hombro del jugador, arriba y al lado contrario de la mira, con balanceo suave, y mira
    /// hacia donde apunta la cámara. Anillo azul (calma), ámbar (combate intenso o vida baja) o rojo (crítico o jefe en furia); late mientras habla.
    /// Sin colliders ni sombras (capa Ignore Raycast): no tapa disparos ni cobertura.
    /// </summary>
    public class VigiaDrone : MonoBehaviour
    {
        public static readonly Color Calm = new Color(0.25f, 0.7f, 1f), Alert = new Color(1f, 0.62f, 0.15f), Danger = new Color(1f, 0.18f, 0.12f);
        public Color StateColor { get; private set; } = Calm;

        private Transform _player, _cam, _head;
        private Health _health;
        private Renderer _ring;
        private Transform[] _blades;
        private MaterialPropertyBlock _mpb;
        private Vector3 _vel;
        private float _iris = 0.4f;
        private Light _light;

        public static VigiaDrone Create(Transform parent, GameObject player)
        {
            var go = new GameObject("VIGIA");
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<VigiaDrone>();
            d._player = player.transform; d._health = player.GetComponent<Health>();
            d.Build();
            go.transform.position = player.transform.position + Vector3.up * 2.4f;
            return d;
        }

        private void Build()
        {
            _mpb = new MaterialPropertyBlock();
            _head = new GameObject("Cabeza").transform; _head.SetParent(transform, false);
            var metal = Prim.GetMaterial(new Color(0.36f, 0.38f, 0.41f));
            var dark = Prim.GetMaterial(new Color(0.08f, 0.09f, 0.1f));
            Part(PrimitiveType.Sphere, "Carcasa", new Vector3(0f, 0f, -0.03f), new Vector3(0.24f, 0.2f, 0.3f), metal);
            Part(PrimitiveType.Cube, "Visera", new Vector3(0f, 0.095f, 0.06f), new Vector3(0.2f, 0.025f, 0.2f), dark);
            Part(PrimitiveType.Cylinder, "Antena", new Vector3(0.07f, 0.13f, -0.08f), new Vector3(0.012f, 0.06f, 0.012f), dark);
            var ring = Part(PrimitiveType.Cylinder, "Anillo", new Vector3(0f, 0f, 0.11f), new Vector3(0.15f, 0.012f, 0.15f), Prim.GetEmissive(Color.white, 1f));
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _ring = ring.GetComponent<Renderer>();
            var lens = Part(PrimitiveType.Cylinder, "Lente", new Vector3(0f, 0f, 0.118f), new Vector3(0.11f, 0.01f, 0.11f), Prim.GetMaterial(new Color(0.02f, 0.03f, 0.05f)));
            lens.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _blades = new Transform[6];
            var bladeMat = Prim.GetMaterial(new Color(0.15f, 0.15f, 0.17f));
            for (int i = 0; i < 6; i++)
            {
                var pivot = new GameObject("Lámina " + i).transform;
                pivot.SetParent(_head, false);
                pivot.localPosition = new Vector3(0f, 0f, 0.126f);
                pivot.localRotation = Quaternion.Euler(0f, 0f, i * 60f);
                var b = Part(PrimitiveType.Cube, "Hoja", Vector3.zero, new Vector3(0.05f, 0.03f, 0.004f), bladeMat);
                b.transform.SetParent(pivot, false);
                _blades[i] = b.transform;
            }
            var lg = new GameObject("Luz"); lg.transform.SetParent(_head, false); lg.transform.localPosition = new Vector3(0f, 0f, 0.16f);
            _light = lg.AddComponent<Light>(); _light.type = LightType.Point; _light.range = 1.2f; _light.intensity = 0.6f; _light.shadows = LightShadows.None;
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
        }

        private GameObject Part(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material m)
        {
            var go = Prim.Create(type, name, pos, scale, m, _head, false);
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return go;
        }

        private void LateUpdate() { SevenDays.Core.PerfProbe.Begin(SevenDays.Core.PerfSection.Vigia); LateUpdateMedido(); SevenDays.Core.PerfProbe.End(); }


        private void LateUpdateMedido()
        {
            if (_player == null) return;
            if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
            float dt = Time.deltaTime, t = Time.time;
            Vector3 fwd = _cam != null ? Vector3.ProjectOnPlane(_cam.forward, Vector3.up).normalized : _player.forward;
            if (fwd.sqrMagnitude < 0.01f) fwd = _player.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            // arriba y a la izquierda del jugador (la mira y el hombro de la cámara quedan a la derecha), un poco por detrás
            Vector3 target = _player.position + Vector3.up * 2.25f - right * 0.85f - fwd * 0.25f
                + new Vector3(Mathf.Sin(t * 1.3f) * 0.04f, Mathf.Sin(t * 2.1f) * 0.05f, 0f);
            transform.position = Vector3.SmoothDamp(transform.position, target, ref _vel, 0.25f);
            Vector3 look = _cam != null ? _cam.forward : fwd;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look) * Quaternion.Euler(Mathf.Sin(t * 0.9f) * 4f, Mathf.Sin(t * 0.6f) * 6f, Mathf.Sin(t * 1.1f) * 3f), 1f - Mathf.Exp(-6f * dt));

            var voice = VigiaVoice.Instance;
            bool speaking = voice != null && voice.Speaking;
            StateColor = PickColor(voice);
            // iris: abierto al hablar o en alerta, entornado en calma
            float want = speaking ? 1f : (StateColor == Calm ? 0.35f : 0.75f);
            _iris = Mathf.MoveTowards(_iris, want, dt * 3f);
            for (int i = 0; i < _blades.Length; i++) _blades[i].localPosition = new Vector3(0.015f + 0.03f * _iris, 0f, 0f);
            float pulse = speaking ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 9f)) : 0.75f;
            _mpb.SetColor("_EmissionColor", StateColor * (3.2f * pulse));
            _ring.SetPropertyBlock(_mpb);
            _light.color = StateColor; _light.intensity = 0.35f + 0.6f * pulse;
        }

        private Color PickColor(VigiaVoice voice)
        {
            float hp = _health != null ? _health.Current / Mathf.Max(1f, _health.Max) : 1f;
            var boss = ZombieManager.Instance != null ? ZombieManager.Instance.ActiveBoss : null;
            bool rage = boss != null && boss.InUse && !boss.Health.IsDead && boss.Boss != null && boss.Boss.Raging;
            if (hp < 0.2f || rage || (voice != null && voice.Current != null && voice.Speaking && voice.Current.Line.Priority == SevenDays.Procedural.VigiaPriority.Critica)) return Danger;
            if (hp < 0.4f || (voice != null && voice.Intense)) return Alert;
            return Calm;
        }
    }
}
