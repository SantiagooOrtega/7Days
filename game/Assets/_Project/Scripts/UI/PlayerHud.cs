using UnityEngine;
using UnityEngine.SceneManagement;
using SevenDays.Core;
using SevenDays.Net;
using SevenDays.Player;
using SevenDays.Procedural;

namespace SevenDays.UI
{
    /// <summary>HUD provisional con IMGUI (vida, munición, mira, cobertura). En la Fase 6 se reemplaza por uGUI.</summary>
    public class PlayerHud : MonoBehaviour
    {
        private Health _health;
        private PlayerShooting _shooting;
        private PlayerCover _cover;
        private PlayerCamera _camera;
        private PlayerInventory _inventory;
        private PlayerInteract _interact;
        private PlayerHealing _healing;
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
            _inventory = player.GetComponent<PlayerInventory>();
            _interact = player.GetComponent<PlayerInteract>();
            _healing = player.GetComponent<PlayerHealing>();
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

            // Arma, rareza y munición de su tipo (la reserva es compartida por todas las armas del mismo tipo)
            var wpn = _shooting.Current;
            var d = wpn.Data;
            var rc = WeaponCatalog.RarityColor(d.rarity);
            Color prevC = GUI.color;
            GUI.color = new Color(rc[0], rc[1], rc[2]);
            GUI.Label(new Rect(w - 660, h - 170, 600, 40), d.displayName + "  (" + WeaponCatalog.RarityName(d.rarity) + ")", new GUIStyle(_label) { alignment = TextAnchor.MiddleRight });
            GUI.color = prevC;
            int reserve = _shooting.CurrentReserve;
            if (d.IsMelee) GUI.Label(new Rect(w - 560, h - 130, 500, 70), "—", _big);
            else GUI.Label(new Rect(w - 560, h - 130, 500, 70), wpn.Magazine + " / " + reserve, _big);
            if (!d.IsMelee)
                GUI.Label(new Rect(w - 560, h - 60, 500, 36), "reserva de " + PlayerInventory.AmmoName(d.ammoType) + " (máx. " + _inventory.Ammo.Max(d.ammoType) + ")", new GUIStyle(_label) { alignment = TextAnchor.MiddleRight, fontSize = 20 });
            DrawSlots(w, h);
            DrawInventoryLine(w, h);

            if (_shooting.Reloading)
            {
                GUI.Label(new Rect(w / 2 - 200, h / 2 + 80, 400, 50), "RECARGANDO", _center);
                Fill(new Rect(w / 2 - 120, h / 2 + 135, 240 * _shooting.ReloadProgress, 8), Color.white);
            }
            else if (!d.IsMelee && wpn.Magazine == 0 && reserve == 0)
            {
                GUI.Label(new Rect(w / 2 - 200, h / 2 + 80, 400, 50), "SIN MUNICIÓN", _center);
            }

            DrawInteraction(w, h);
            DrawMessages(w);

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
                    "Clic der. apuntar · Clic izq. disparar/golpear · R recargar · 1-5/rueda cambiar arma\n" +
                    "E recoger arma · mantén E para buscar en cajas, coches, muebles y escombros · H botiquín · B venda\n" +
                    "C cobertura (cerca de cajas y muros bajos) · A/D deslizarse · Esc soltar ratón\n" +
                    "Espacio: saltar · pasa cajas, muros bajos y coches (corriendo o con Espacio; desde la cobertura, al otro lado)\n" +
                    "T/Y: probar daño/curación · U: subir 6 m · L: drop de prueba · N: siguiente día (respawn) · F3 ambiente · F1 ayuda", _label);
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

        private void DrawSlots(float w, float h)
        {
            var list = _inventory.Weapons;
            for (int i = 0; i < list.Count; i++)
            {
                var rc = WeaponCatalog.RarityColor(list[i].Data.rarity);
                bool cur = list[i] == _shooting.Current;
                Color prev = GUI.color;
                GUI.color = cur ? new Color(rc[0], rc[1], rc[2]) : new Color(rc[0], rc[1], rc[2], 0.55f);
                GUI.Label(new Rect(w - 660, h - 215 - (list.Count - 1 - i) * 30, 600, 30), (i + 1) + "  " + list[i].Data.displayName, new GUIStyle(_label) { alignment = TextAnchor.MiddleRight, fontSize = cur ? 24 : 20, fontStyle = cur ? FontStyle.Bold : FontStyle.Normal });
                GUI.color = prev;
            }
        }

        private void DrawInventoryLine(float w, float h)
        {
            var a = _inventory.Ammo; var m = _inventory.Medkits;
            string text = "Munición  pistola " + a.Count(AmmoType.Pistol) + "/" + a.Max(AmmoType.Pistol) + "  escopeta " + a.Count(AmmoType.Shotgun) + "/" + a.Max(AmmoType.Shotgun) + "  rifle " + a.Count(AmmoType.Rifle) + "/" + a.Max(AmmoType.Rifle)
                + "\nVendas " + m.Count(MedkitKind.Bandage) + " [B]   Botiquines  peq. " + m.Count(MedkitKind.Small) + "  med. " + m.Count(MedkitKind.Medium) + "  gran. " + m.Count(MedkitKind.Large) + " [H]";
            GUI.Label(new Rect(60, h - 190, 900, 90), text, new GUIStyle(_label) { fontSize = 20 });
        }

        /// <summary>Aviso de interacción (E recoger / mantén E buscar) y barras de búsqueda y de curación.</summary>
        private void DrawInteraction(float w, float h)
        {
            var t = _interact.Target;
            if (t != null)
            {
                GUI.Label(new Rect(0, h / 2 + 150, w, 40), t.Prompt, new GUIStyle(_center) { fontSize = 28 });
                if (_interact.Holding) Bar(new Rect(w / 2 - 150, h / 2 + 200, 300, 14), _interact.HoldProgress, new Color(1f, 0.9f, 0.4f));
            }
            if (_healing.Healing)
            {
                GUI.Label(new Rect(0, h / 2 + 235, w, 40), "Curando... (recibir daño la cancela)", new GUIStyle(_center) { fontSize = 26 });
                Bar(new Rect(w / 2 - 150, h / 2 + 280, 300, 14), _healing.Session.Progress, new Color(0.4f, 1f, 0.5f));
            }
        }

        private void DrawMessages(float w)
        {
            var list = HudMessages.Current;
            for (int i = 0; i < list.Count; i++)
            {
                Color prev = GUI.color;
                GUI.color = list[i].Color;
                GUI.Label(new Rect(0, 110 + i * 38, w, 40), list[i].Text, new GUIStyle(_center) { fontSize = 28 });
                GUI.color = prev;
            }
        }

        private static void Bar(Rect r, float t, Color c)
        {
            Fill(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), new Color(0, 0, 0, 0.65f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height), c);
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
            DrawHitmarker(cx, cy);
        }

        /// <summary>Hitmarker en forma de X: blanca al impactar, amarilla en headshot, roja y más grande al matar.</summary>
        private static void DrawHitmarker(float cx, float cy)
        {
            var fb = SevenDays.Combat.CombatFeedback.Instance;
            if (fb == null) return;
            float t = Time.time;
            bool kill = t < fb.KillUntil, hit = t < fb.HitUntil;
            if (!kill && !hit) return;
            Color c = kill ? new Color(1f, 0.15f, 0.15f) : (t < fb.HeadUntil ? new Color(1f, 0.85f, 0.15f) : Color.white);
            float size = kill ? 11f : 7f, thick = kill ? 3.5f : 2.5f;
            var prev = GUI.matrix;
            foreach (float angle in new[] { 45f, -45f })
            {
                GUIUtility.RotateAroundPivot(angle, new Vector2(cx, cy));
                Fill(new Rect(cx - size - 6f, cy - thick * 0.5f, size, thick), c);
                Fill(new Rect(cx + 6f, cy - thick * 0.5f, size, thick), c);
                GUI.matrix = prev;
            }
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
