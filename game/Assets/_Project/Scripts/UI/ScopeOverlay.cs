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
            float zoom = 60f / Mathf.Max(1f, _camera.CurrentFov);
            GUI.Label(new Rect(cx - 150, y + d * 0.88f, 300, 30), "x" + zoom.ToString("0.0") + "   (rueda: zoom)", _label);
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
                    alpha = Mathf.Max(alpha, Mathf.InverseLerp(0.75f, 0.92f, d) * 0.35f);   // viñeta interior de la lente
                    px[j * size + i] = new Color32(0, 0, 0, (byte)(alpha * 255));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }
    }
}
