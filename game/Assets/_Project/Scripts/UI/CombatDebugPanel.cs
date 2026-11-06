using UnityEngine;
using SevenDays.Core;
using SevenDays.Player;
using SevenDays.Procedural;

namespace SevenDays.UI
{
    /// <summary>
    /// Panel de ajuste en vivo (F5): valores actuales de retroceso, dispersión, FOV, sensibilidad y cámara de carrera (balanceo, roll, fatiga), y deslizadores para
    /// cambiar los del arma actual y de la cámara sin recompilar. Los cambios valen hasta salir de Play; para guardarlos, copia los números al ScriptableObject.
    /// Al abrirlo se suelta el ratón (Esc no hace falta).
    /// </summary>
    public class CombatDebugPanel : MonoBehaviour
    {
        private PlayerCamera _camera;
        private PlayerShooting _shooting;
        private WeaponHandling _handling;
        private PlayerCover _cover;
        private bool _open;
        private Vector2 _scroll;
        private GUIStyle _label, _head;
        private Rect _rect;

        public void Initialize(GameObject player)
        {
            _camera = player.GetComponent<PlayerCamera>();
            _shooting = player.GetComponent<PlayerShooting>();
            _handling = player.GetComponent<WeaponHandling>();
            _cover = player.GetComponent<PlayerCover>();
        }

        private void Update()
        {
            if (!GameInput.DebugPanelPressed) return;
            _open = !_open;
            Cursor.lockState = _open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = _open;
        }

        private void OnGUI()
        {
            if (!_open || _camera == null || _shooting == null) return;
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
            _head ??= new GUIStyle(_label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.4f) } };
            _rect = new Rect(10, 10, 430, Screen.height - 20);
            if (_rect.Contains(Event.current.mousePosition))   // IMGUI: el proyecto usa solo el Input System nuevo (Input.mousePosition daría error)
                PlayerCamera.UiHoverUntil = Time.unscaledTime + 0.15f;
            GUI.Box(_rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(_rect.x + 8, _rect.y + 6, _rect.width - 16, _rect.height - 12));
            _scroll = GUILayout.BeginScrollView(_scroll);

            var w = _shooting.Current.Data;
            var run = _camera.RunCam;
            var o = _camera.RunOutput;
            GUILayout.Label("AJUSTE EN VIVO (F5 cierra)", _head);
            GUILayout.Label("Arma: " + w.displayName + "   modo " + FireModes.Label(_shooting.FireControl.Mode), _label);
            GUILayout.Label("Retroceso: pitch " + _camera.Recoil.Pitch.ToString("0.00") + "°  yaw " + _camera.Recoil.Yaw.ToString("0.00") + "°   kick " + (_handling != null ? _handling.Kick.ToString("0.00") : "-"), _label);
            GUILayout.Label("Dispersión: " + _shooting.CurrentSpread.ToString("0.00") + "°  (bloom " + _shooting.Spread.Bloom.ToString("0.00") + "°)", _label);
            GUILayout.Label("FOV " + _camera.CurrentFov.ToString("0.0") + "   apuntado " + (_camera.Aim.Eased * 100f).ToString("0") + "%   sensibilidad " + _camera.CurrentSensitivity.ToString("0.000"), _label);
            GUILayout.Label("Hombro " + (_camera.Shoulder.Side > 0 ? "derecho" : "izquierdo") + "  lean " + _camera.LeanOffset.ToString("0.00") + " m  primera persona " + (_camera.FirstPersonBlend * 100f).ToString("0") + "%", _label);
            var cv = _cover;
            if (cv != null) GUILayout.Label("Cobertura: " + (cv.Active ? cv.Exposure.State + "  asomada " + cv.PopDir + "  exposición " + (cv.Exposure.Value * 100f).ToString("0") + "%  borde sup. " + cv.CoverTop.ToString("0.00") + " m" : "fuera"), _label);
            if (run != null)
            {
                GUILayout.Label("Carrera: intensidad " + (o.Weight * 100f).ToString("0") + "%  x " + (o.OffsetX * 100f).ToString("0.0") + " cm  y " + (o.OffsetY * 100f).ToString("0.0") + " cm  roll " + o.Roll.ToString("0.00") + "°", _label);
                GUILayout.Label("Fatiga " + (run.Fatigue * 100f).ToString("0") + "%  corriendo " + run.RunTime.ToString("0.0") + " s  cabeceo " + o.Pitch.ToString("0.00") + "°  FOV +" + o.FovDelta.ToString("0.0") + "  pasos/s " + run.Steps.StepsPerSecond.ToString("0.0"), _label);
            }

            GUILayout.Space(6);
            GUILayout.Label("Arma actual", _head);
            w.recoil = Slider("recoil (vertical °/disparo)", w.recoil, 0f, 6f);
            w.recoilHorizontal = Slider("recoilHorizontal (°)", w.recoilHorizontal, 0f, 3f);
            w.recoilRecovery = Slider("recoilRecovery (°/s)", w.recoilRecovery, 0.5f, 30f);
            w.recoilMaxPitch = Slider("recoilMaxPitch (°)", w.recoilMaxPitch, 0.5f, 20f);
            w.spreadHip = Slider("spreadHip (°)", w.spreadHip, 0f, 10f);
            w.spreadAim = Slider("spreadAim (°)", w.spreadAim, 0f, w.spreadHip);
            w.spreadPerShot = Slider("spreadPerShot (°)", w.spreadPerShot, 0f, 3f);
            w.spreadRecovery = Slider("spreadRecovery (°/s)", w.spreadRecovery, 0.5f, 30f);
            w.spreadMove = Slider("spreadMove (°)", w.spreadMove, 0f, 5f);
            w.aimFov = Slider("aimFov", w.aimFov, 10f, 70f);
            w.aimSensitivity = Slider("aimSensitivity (x)", w.aimSensitivity, 0.1f, 1.2f);
            w.adsTime = Slider("adsTime (s)", w.adsTime, 0.05f, 0.6f);
            w.swayAmount = Slider("swayAmount", w.swayAmount, 0f, 3f);
            w.bobAmount = Slider("bobAmount", w.bobAmount, 0f, 3f);
            w.kickback = Slider("kickback (m)", w.kickback, 0f, 0.3f);
            w.fireShake = Slider("fireShake", w.fireShake, 0f, 0.5f);
            w.Validate();

            GUILayout.Space(6);
            GUILayout.Label("Cámara", _head);
            var c = _camera.Settings;
            c.sensitivity = Slider("sensibilidad", c.sensitivity, 0.02f, 0.4f);
            c.hipFov = Slider("FOV sin apuntar", c.hipFov, 45f, 85f);
            c.rotationSmoothing = Slider("suavizado (s)", c.rotationSmoothing, 0f, 0.15f);
            c.shakeScale = Slider("sacudidas (x)", c.shakeScale, 0f, 2f);
            c.popSeconds = Slider("asomarse (s)", c.popSeconds, 0.15f, 0.25f);
            c.blindDamageMultiplier = Slider("daño a ciegas (x)", c.blindDamageMultiplier, 0.1f, 1f);
            c.blindSpreadMultiplier = Slider("dispersión a ciegas (x)", c.blindSpreadMultiplier, 1f, 6f);
            c.coveredDamageFloor = Slider("daño cubierto (x)", c.coveredDamageFloor, 0f, 1f);
            c.motionScale = Slider("Movimiento de cámara (accesibilidad)", c.motionScale, 0f, 1f);
            c.Validate();

            if (run != null)
            {
                GUILayout.Space(6);
                GUILayout.Label("Cámara de carrera", _head);
                var r = run.Settings;
                r.fovBoost = Slider("fovBoost (°)", r.fovBoost, 0f, 8f);
                r.swayMeters = Slider("swayMeters (m)", r.swayMeters, 0f, 0.08f);
                r.rollDegrees = Slider("rollDegrees (°)", r.rollDegrees, 0f, 1.5f);
                r.bobMeters = Slider("bobMeters (m)", r.bobMeters, 0f, 0.08f);
                r.distanceDelta = Slider("distanceDelta (m)", r.distanceDelta, -1f, 0.5f);
                r.heightDelta = Slider("heightDelta (m)", r.heightDelta, -0.3f, 0.2f);
                r.fatigueDelay = Slider("fatigueDelay (s)", r.fatigueDelay, 0f, 15f);
                r.fatigueAmplitudeBoost = Slider("fatigueAmplitudeBoost", r.fatigueAmplitudeBoost, 0f, 0.4f);
                r.breathPitch = Slider("breathPitch (°)", r.breathPitch, 0f, 1.5f);
                r.recoverSeconds = Slider("recoverSeconds (s)", r.recoverSeconds, 0.5f, 6f);
                r.Validate();   // los límites duros se aplican siempre
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private float Slider(string name, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, _label, GUILayout.Width(210));
            float v = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(130));
            GUILayout.Label(v.ToString("0.###"), _label, GUILayout.Width(50));
            GUILayout.EndHorizontal();
            return v;
        }
    }
}
