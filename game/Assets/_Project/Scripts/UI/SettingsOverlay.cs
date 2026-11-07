using UnityEngine;
using SevenDays.Player;

namespace SevenDays.UI
{
    /// <summary>
    /// Ajustes rápidos que aparecen al soltar el ratón (Esc): "Movimiento de cámara" (accesibilidad, 0-100 %: balanceo al correr, jadeo y sacudidas) y sensibilidad.
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
            var r = new Rect(Screen.width - 430, Screen.height * 0.5f - 110, 400, 240);
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
        }
    }
}
