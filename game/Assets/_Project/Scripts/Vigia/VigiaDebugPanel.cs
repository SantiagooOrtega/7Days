using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SevenDays.Procedural;

namespace SevenDays.Vigia
{
    /// <summary>
    /// F8: panel de pruebas de VIGÍA. Lista todas las frases por categoría (clic = decirla, saltándose enfriamientos), simula rachas (2, 3, 4 bajas seguidas,
    /// racha de 8 y de 12, triple cabeza) y muestra la cola, el modo y si el combate cuenta como intenso.
    /// </summary>
    public class VigiaDebugPanel : MonoBehaviour
    {
        private void Awake() { useGUILayout = false; }   // sin GUILayout: Unity se salta la pasada de Layout (la mitad del coste de IMGUI)

        private bool _show;
        private Vector2 _scroll;
        private int _cat;
        private GUIStyle _label;

        private void Update()
        {
            var k = Keyboard.current;
            if (k != null && k.f8Key.wasPressedThisFrame) _show = !_show;
        }

        /// <summary>Dice una frase sin pasar por los enfriamientos (vacía la cola antes).</summary>
        public static void Force(string id)
        {
            var v = VigiaVoice.Instance;
            if (v == null || !v.Catalog.TryGetValue(id, out var line)) return;
            v.Queue.Clear();
            v.Queue.Request(line, Time.unscaledTimeAsDouble + 1000.0 * Random.value, false);
        }

        /// <summary>Simula n bajas separadas "gap" segundos con el ComboTracker real y dice lo que saldría en la última.</summary>
        public static string SimulateStreak(int n, double gap, bool heads)
        {
            var c = new ComboTracker();
            string last = null;
            for (int i = 0; i < n; i++) { var s = c.OnKill(i * gap, heads); if (s != null) last = s; }
            if (last != null) Force(last);
            return last;
        }

        private void OnGUI()
        {
            if (SevenDays.Core.HudGate.Hidden) return;   // intro o epílogo en pantalla: solo la lámina
           
            if (!_show) return;
            var v = VigiaVoice.Instance;
            if (v == null) return;
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            var r = new Rect(10, 140, 520, 640);
            if (r.Contains(Event.current.mousePosition)) SevenDays.Player.PlayerCamera.UiHoverUntil = Time.unscaledTime + 0.15f;
            GUI.Box(r, GUIContent.none);
            GUI.Label(new Rect(r.x + 10, r.y + 6, 500, 22), "VIGÍA (F8) · modo " + VigiaSettings.ModeNames[(int)VigiaSettings.Mode] + " · intenso " + (v.Intense ? "sí" : "no") + " · en cola " + v.Queue.Pending.Count + (v.Current != null && v.Speaking ? " · diciendo " + v.Current.Line.Id : ""), _label);
            float y = r.y + 32;
            string[] sims = { "Doble", "Triple", "Cuádruple", "Combo x3", "Racha 8", "Masacre 12", "3 cabezas" };
            for (int i = 0; i < sims.Length; i++)
            {
                if (GUI.Button(new Rect(r.x + 10 + (i % 4) * 126, y + (i / 4) * 28, 120, 24), sims[i]))
                {
                    switch (i)
                    {
                        case 0: SimulateStreak(2, 0.5, false); break;
                        case 1: SimulateStreak(3, 0.5, false); break;
                        case 2: SimulateStreak(4, 0.5, false); break;
                        case 3: SimulateStreak(3, 2.5, false); break;
                        case 4: SimulateStreak(8, 2.5, false); break;
                        case 5: SimulateStreak(12, 2.5, false); break;
                        case 6: SimulateStreak(3, 5.0, true); break;
                    }
                }
            }
            y += 62;
            var cats = VigiaCatalog.Categories;
            for (int i = 0; i < cats.Length; i++) if (GUI.Toggle(new Rect(r.x + 10 + i * 84, y, 80, 24), _cat == i, cats[i], GUI.skin.button)) _cat = i;
            y += 30;
            var lines = new List<VigiaLine>();
            foreach (var l in v.Catalog.Values) if (l.Category == cats[_cat]) lines.Add(l);
            _scroll = GUI.BeginScrollView(new Rect(r.x + 6, y, r.width - 12, r.yMax - y - 6), _scroll, new Rect(0, 0, r.width - 30, lines.Count * 26));
            for (int i = 0; i < lines.Count; i++)
                if (GUI.Button(new Rect(0, i * 26, r.width - 30, 24), lines[i].Id + " (" + lines[i].Priority + "): " + lines[i].Variants[0])) Force(lines[i].Id);
            GUI.EndScrollView();
        }
    }
}
