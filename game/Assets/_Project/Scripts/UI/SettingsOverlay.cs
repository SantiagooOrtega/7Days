using UnityEngine;
using SevenDays.Player;

namespace SevenDays.UI
{
    /// <summary>
    /// Ajustes rápidos que aparecen al soltar el ratón (Esc): "Movimiento de cámara" (accesibilidad, 0-100 %: balanceo al correr, jadeo y sacudidas), sensibilidad,
    /// calidad y volumen (general y por grupo: zombis, armas, ambiente, interfaz).
    /// Se guardan por jugador en PlayerPrefs. El menú de opciones completo llega en la Fase 6.
    /// </summary>
    public class SettingsOverlay : MonoBehaviour
    {
        private PlayerCamera _camera;
        private GUIStyle _label, _title;

        public void Initialize(PlayerCamera cam) => _camera = cam;

        private void OnGUI()
        {
            if (_camera == null || Cursor.lockState == CursorLockMode.Locked) return;
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = Color.white } };
            _title ??= new GUIStyle(_label) { fontSize = 22, fontStyle = FontStyle.Bold };
            var s = _camera.Settings;
            var r = new Rect(Screen.width - 430, Screen.height * 0.5f - 250, 400, 520);
            if (r.Contains(Event.current.mousePosition)) PlayerCamera.UiHoverUntil = Time.unscaledTime + 0.15f;
            GUI.Box(r, GUIContent.none);
            GUI.Label(new Rect(r.x + 16, r.y + 10, 380, 30), "Ajustes (clic en el juego para volver)", _title);

            GUI.Label(new Rect(r.x + 16, r.y + 50, 380, 26), "Movimiento de cámara: " + Mathf.RoundToInt(s.motionScale * 100f) + " %", _label);
            float motion = GUI.HorizontalSlider(new Rect(r.x + 16, r.y + 82, 368, 20), s.motionScale, 0f, 1f);
            GUI.Label(new Rect(r.x + 16, r.y + 110, 380, 26), "Sensibilidad: " + s.sensitivity.ToString("0.000"), _label);
            float sens = GUI.HorizontalSlider(new Rect(r.x + 16, r.y + 142, 368, 20), s.sensitivity, 0.02f, 0.4f);

            bool toggle = GUI.Toggle(new Rect(r.x + 16, r.y + 180, 368, 26), s.aimToggle, s.aimToggle ? " Apuntar: alternar (un clic apunta, otro suelta)" : " Apuntar: mantener clic derecho");
            if (toggle != s.aimToggle) { s.aimToggle = toggle; PlayerPrefs.SetInt(CameraSettings.AimTogglePrefKey, toggle ? 1 : 0); }
            if (!Mathf.Approximately(motion, s.motionScale)) { s.motionScale = Mathf.Round(motion * 20f) / 20f; PlayerPrefs.SetFloat(CameraSettings.MotionPrefKey, s.motionScale); }
            if (!Mathf.Approximately(sens, s.sensitivity)) { s.sensitivity = sens; PlayerPrefs.SetFloat(CameraSettings.SensitivityPrefKey, s.sensitivity); }

            // calidad gráfica (QualityDirector): Auto ajusta sola sombras, post-procesado, atmósfera y resolución para sostener 60 FPS
            var q = SevenDays.Look.QualityDirector.Instance;
            if (q != null)
            {
                GUI.Label(new Rect(r.x + 16, r.y + 218, 380, 26), "Calidad: " + q.Mode + (q.Mode == SevenDays.Look.QualityMode.Auto ? " (ahora " + SevenDays.Procedural.QualityProfile.For(q.Level).Name + ")" : ""), _label);
                string[] names = { "Auto", "Alta", "Media", "Baja" };
                for (int i = 0; i < 4; i++)
                {
                    var b = new Rect(r.x + 16 + i * 94, r.y + 248, 88, 30);
                    bool sel = (int)q.Mode == i;
                    if (GUI.Toggle(b, sel, names[i], GUI.skin.button) && !sel) q.SetMode((SevenDays.Look.QualityMode)i);
                }
                bool dyn = GUI.Toggle(new Rect(r.x + 16, r.y + 290, 368, 26), q.DynamicResolution, " Resolución dinámica (" + q.RenderScale.ToString("0.00") + "x)");
                if (dyn != q.DynamicResolution) q.SetDynamicResolution(dyn);
            }

            // volumen: general y por grupo (zombis, armas, ambiente, interfaz); se guarda al soltar
            float y = r.y + 330;
            AudioRow(r.x, ref y, "General", SevenDays.Core.AudioMix.Master, v => SevenDays.Core.AudioMix.Master = v);
            for (int i = 0; i < SevenDays.Core.AudioMix.Names.Length; i++)
            {
                var g = (SevenDays.Core.AudioGroup)i;
                AudioRow(r.x, ref y, SevenDays.Core.AudioMix.Names[i], SevenDays.Core.AudioMix.GroupVolume(g), v => SevenDays.Core.AudioMix.SetGroupVolume(g, v));
            }
        }

        private void AudioRow(float x, ref float y, string name, float value, System.Action<float> set)
        {
            GUI.Label(new Rect(x + 16, y, 150, 24), name + ": " + Mathf.RoundToInt(value * 100f) + " %", _label);
            float v = GUI.HorizontalSlider(new Rect(x + 170, y + 8, 214, 20), value, 0f, 1f);
            if (!Mathf.Approximately(v, value)) set(Mathf.Round(v * 20f) / 20f);
            y += 34f;
        }
    }
}
