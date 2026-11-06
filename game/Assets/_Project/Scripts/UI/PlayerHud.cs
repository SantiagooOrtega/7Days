using UnityEngine;
using UnityEngine.SceneManagement;
using SevenDays.Core;
using SevenDays.Net;
using SevenDays.Player;

namespace SevenDays.UI
{
    /// <summary>HUD provisional con IMGUI (vida, munición, mira, cobertura). En la Fase 6 se reemplaza por uGUI.</summary>
    public class PlayerHud : MonoBehaviour
    {
        private Health _health;
        private PlayerShooting _shooting;
        private PlayerCover _cover;
        private PlayerCamera _camera;
        private float _hitUntil;
        private bool _showHelp = true;
        private float _fps = 60f;
        private GUIStyle _label, _big, _center;

        public void Initialize(GameObject player)
        {
            _health = player.GetComponent<Health>();
            _shooting = player.GetComponent<PlayerShooting>();
            _cover = player.GetComponent<PlayerCover>();
            _camera = player.GetComponent<PlayerCamera>();
            _shooting.HitConfirmed += () => _hitUntil = Time.time + 0.12f;
        }

        private void Update()
        {
            if (GameInput.HelpPressed) _showHelp = !_showHelp;
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.05f);
            if (_health != null && _health.IsDead && GameInput.ReloadPressed)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnGUI()
        {
            if (_health == null) return;
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 26, normal = { textColor = Color.white } };
                _big = new GUIStyle(_label) { fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
                _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontSize = 40, fontStyle = FontStyle.Bold };
            }

            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s;
            const float h = 1080f;

            DrawCrosshair(w, h);

            // Vida
            float pct = _health.Current / _health.Max;
            Rect bar = new Rect(60, h - 90, 420, 30);
            Fill(new Rect(bar.x - 4, bar.y - 4, bar.width + 8, bar.height + 8), new Color(0, 0, 0, 0.6f));
            Fill(new Rect(bar.x, bar.y, bar.width * pct, bar.height), Color.Lerp(new Color(0.8f, 0.1f, 0.1f), new Color(0.3f, 0.8f, 0.3f), pct));
            GUI.Label(new Rect(bar.x, bar.y - 40, 420, 36), "VIDA  " + Mathf.CeilToInt(_health.Current), _label);

            // Arma y munición
            var wpn = _shooting.Current;
            GUI.Label(new Rect(w - 560, h - 170, 500, 40), wpn.Def.displayName, new GUIStyle(_label) { alignment = TextAnchor.MiddleRight });
            GUI.Label(new Rect(w - 560, h - 130, 500, 70), wpn.Magazine + " / " + wpn.Reserve, _big);
            if (_shooting.Reloading)
            {
                GUI.Label(new Rect(w / 2 - 200, h / 2 + 80, 400, 50), "RECARGANDO", _center);
                Fill(new Rect(w / 2 - 120, h / 2 + 135, 240 * _shooting.ReloadProgress, 8), Color.white);
            }
            else if (wpn.Magazine == 0 && wpn.Reserve == 0)
            {
                GUI.Label(new Rect(w / 2 - 200, h / 2 + 80, 400, 50), "SIN MUNICIÓN", _center);
            }

            if (_cover.Active)
                GUI.Label(new Rect(w / 2 - 200, h - 150, 400, 50), _cover.IsExposed ? "ASOMADO" : "EN COBERTURA", _center);

            // FPS (objetivo: 60)
            Color prevColor = GUI.color;
            GUI.color = _fps >= 55f ? new Color(0.5f, 1f, 0.5f) : (_fps >= 40f ? Color.yellow : new Color(1f, 0.4f, 0.4f));
            GUI.Label(new Rect(w - 520, 20, 490, 40), "FPS " + Mathf.RoundToInt(_fps) + "  (" + (1000f / _fps).ToString("0.0") + " ms)  ambiente " + (3 - SevenDays.Look.AtmosphereBudget.Level) + "/3",
                new GUIStyle(_label) { alignment = TextAnchor.MiddleRight });
            GUI.color = prevColor;

            // Cuenta
            string who = Session.IsLoggedIn ? Session.User.username : "(sin sesión)";
            GUI.Label(new Rect(30, 20, 700, 40), "Jugador: " + who, _label);
            if (_showHelp)
            {
                GUI.Label(new Rect(30, 60, 900, 260),
                    "WASD mover · Shift correr · Ctrl agacharse · Ratón mirar\n" +
                    "Clic der. apuntar · Clic izq. disparar · R recargar · 1/2/rueda cambiar arma\n" +
                    "C cobertura (cerca de cajas y muros bajos) · A/D deslizarse · Esc soltar ratón\n" +
                    "Espacio: saltar · pasa cajas, muros bajos y coches (corriendo o con Espacio; desde la cobertura, al otro lado)\n" +
                    "T/Y: probar daño/curación · U: subir 6 m (probar caída) · F3 recortar ambiente · F1 ocultar ayuda", _label);
            }

            if (SevenDays.World.FireManager.PlayerBurning)
            {
                Fill(new Rect(0, 0, w, h), new Color(1f, 0.35f, 0.05f, 0.14f));
                GUI.Label(new Rect(0, h - 250, w, 50), "¡TE QUEMAS! Sal del fuego", _center);
            }

            if (_health.IsDead)
            {
                Fill(new Rect(0, 0, w, h), new Color(0.4f, 0, 0, 0.5f));
                GUI.Label(new Rect(0, h / 2 - 60, w, 120), "HAS MUERTO\nPulsa R para reiniciar", _center);
            }
        }

        private void DrawCrosshair(float w, float h)
        {
            float gap = _camera.Aiming ? 5f : 16f;
            float len = 12f;
            Color c = Time.time < _hitUntil ? new Color(1f, 0.2f, 0.2f) : new Color(1f, 1f, 1f, 0.9f);
            float cx = w / 2f, cy = h / 2f;
            Fill(new Rect(cx - 1.5f, cy - 1.5f, 3f, 3f), c);
            Fill(new Rect(cx - gap - len, cy - 1.5f, len, 3f), c);
            Fill(new Rect(cx + gap, cy - 1.5f, len, 3f), c);
            Fill(new Rect(cx - 1.5f, cy - gap - len, 3f, len), c);
            Fill(new Rect(cx - 1.5f, cy + gap, 3f, len), c);
        }

        private static void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
