using UnityEngine;
using SevenDays.Player;

namespace SevenDays.UI
{
    /// <summary>Overlay de mira telescópica: todo negro salvo un círculo con borde suave, cruz fina y el nivel de zoom. Solo con el francotirador apuntando.</summary>
    public class ScopeOverlay : MonoBehaviour
    {
        private PlayerCamera _camera;
        private Texture2D _mask;
        private GUIStyle _label;

        public void Initialize(PlayerCamera cam) => _camera = cam;

        private void OnDestroy() { if (_mask != null) Destroy(_mask); }

        private void OnGUI()
        {
            if (_camera == null || Event.current.type != EventType.Repaint) return;
            float a = Mathf.InverseLerp(0.75f, 1f, _camera.FirstPersonBlend);
            if (!_camera.Weapon?.HasScope ?? true) return;
            if (a <= 0f) return;
            if (_mask == null) _mask = BuildMask(512);
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.8f, 0.85f, 0.8f) } };

            float h = Screen.height, w = Screen.width, d = h * 0.98f;
            float x = (w - d) * 0.5f, y = (h - d) * 0.5f;
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, a);
            GUI.DrawTexture(new Rect(0, 0, x + 1, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + d - 1, 0, w - x - d + 1, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, 0, w, y + 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, y + d - 1, w, h - y - d + 1), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.DrawTexture(new Rect(x, y, d, d), _mask);
            // cruz fina con hueco central
            GUI.color = new Color(0f, 0f, 0f, 0.9f * a);
            float cx = w * 0.5f, cy = h * 0.5f, gap = 6f;
            GUI.DrawTexture(new Rect(x + d * 0.08f, cy - 0.75f, cx - gap - x - d * 0.08f, 1.5f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + gap, cy - 0.75f, x + d * 0.92f - cx - gap, 1.5f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 0.75f, y + d * 0.08f, 1.5f, cy - gap - y - d * 0.08f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 0.75f, cy + gap, 1.5f, y + d * 0.92f - cy - gap), Texture2D.whiteTexture);
            GUI.color = new Color(0.9f, 0.1f, 0.1f, a);
            GUI.DrawTexture(new Rect(cx - 1.5f, cy - 1.5f, 3f, 3f), Texture2D.whiteTexture);
            GUI.color = prev;
            // zoom nominal del nivel actual (x3, x5, x8, x12) y estado de la respiración
            var wpn = _camera.Weapon;
            float zoom = wpn != null && wpn.HasScope ? 60f / wpn.scopeFovs[Mathf.Clamp(_camera.ScopeLevel, 0, wpn.scopeFovs.Length - 1)] : 60f / Mathf.Max(1f, _camera.CurrentFov);
            string breath = _camera.Breath.State == SevenDays.Procedural.BreathState.Holding ? "   CONTENIENDO " + Mathf.CeilToInt(_camera.Breath.Air * 100f) + "%"
                : (_camera.Breath.State == SevenDays.Procedural.BreathState.Exhausted ? "   JADEANDO" : "   Shift: contener la respiración");
            GUI.Label(new Rect(cx - 250, y + d * 0.88f, 500, 30), "x" + Mathf.RoundToInt(zoom) + "   (rueda: zoom)" + breath, _label);
        }

        /// <summary>Máscara circular: transparente dentro, negra fuera, con borde suave y un anillo oscuro.</summary>
        private static Texture2D BuildMask(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                {
                    float d = Mathf.Sqrt((i + 0.5f - r) * (i + 0.5f - r) + (j + 0.5f - r) * (j + 0.5f - r)) / r;
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, 0.97f, d));
                    alpha = Mathf.Max(alpha, Mathf.InverseLerp(0.86f, 0.95f, d) * 0.18f);   // viñeta interior suave: la lente se ve más clara
                    px[j * size + i] = new Color32(0, 0, 0, (byte)(alpha * 255));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }
    }
}
