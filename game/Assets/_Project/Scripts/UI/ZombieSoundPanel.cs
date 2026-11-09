using UnityEngine;
using UnityEngine.InputSystem;
using SevenDays.Combat;
using SevenDays.Procedural;

namespace SevenDays.UI
{
    /// <summary>
    /// Panel "Sonidos de zombi" de F6 (abajo a la derecha): elige tipo y garganta, y reproduce cualquier evento 3 m delante de la cámara por el mismo pool de
    /// voces del juego. Muestra voces en uso, sonidos descartados (lejos o sin voz libre), clips generados y su memoria. Con el ratón suelto (Esc) se pulsa.
    /// </summary>
    public class ZombieSoundPanel : MonoBehaviour
    {
        private bool _open;
        private int _type, _timbre;
        private readonly ZombieSounds _preview = new ZombieSounds();
        private Transform _anchor;
        private GUIStyle _label, _head;
        private Vector2 _scroll;

        private void Update()
        {
            var k = Keyboard.current;
            if (k != null && k.f6Key.wasPressedThisFrame)
            {
                _open = !_open;
                if (_open) foreach (string t in ZombieAnatomies.Types) ZombieSoundBank.Prewarm(t);
            }
            if (_anchor != null && Camera.main != null) _anchor.position = Camera.main.transform.position + Camera.main.transform.forward * 3f - Vector3.up * 1.5f;
        }

        private void OnGUI()
        {
            if (!_open) return;
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
            _head ??= new GUIStyle(_label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.5f) } };
            var r = new Rect(Screen.width - 430, Screen.height - 330, 420, 320);
            if (r.Contains(Event.current.mousePosition)) SevenDays.Player.PlayerCamera.UiHoverUntil = Time.unscaledTime + 0.15f;
            var prev = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.7f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = prev;
            GUILayout.BeginArea(new Rect(r.x + 8, r.y + 4, r.width - 16, r.height - 8));
            GUILayout.Label("SONIDOS DE ZOMBI (F6)", _head);
            GUILayout.Label("Voces " + ZombieAudio.ActiveVoices + "/" + ZombieAudio.MaxVoices + "   descartados " + ZombieAudio.Dropped + "   clips " + ZombieSoundBank.ClipCount
                + (ZombieSoundBank.OverrideCount > 0 ? " (+" + ZombieSoundBank.OverrideCount + " propios)" : "") + "   " + (ZombieSoundBank.Bytes / 1048576f).ToString("0.0") + " MB", _label);
            _type = GUILayout.SelectionGrid(_type, ZombieAnatomies.Types, 6);
            _timbre = GUILayout.SelectionGrid(_timbre, new[] { "Garganta A", "Garganta B" }, 2);
            string typeId = ZombieAnatomies.Types[_type];
            _scroll = GUILayout.BeginScrollView(_scroll);
            var events = ZombieSoundMap.EventsFor(typeId);
            for (int i = 0; i < events.Count; i += 4)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + 4, events.Count); j++)
                    if (GUILayout.Button(events[j].ToString())) Preview(typeId, events[j]);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void Preview(string typeId, ZombieSound ev)
        {
            if (_anchor == null) _anchor = new GameObject("Prueba de sonido de zombi").transform;
            // semilla fija por garganta: la primera semilla que caiga en el timbre elegido
            int seed = 1;
            for (int s = 1; s < 200; s++) { _preview.Begin(_anchor, typeId, s, false); if (_preview.Timbre == _timbre) { seed = s; break; } }
            _preview.Begin(_anchor, typeId, seed, false);
            if (ev == ZombieSound.Fuse || ev == ZombieSound.Blast) { var c = ev == ZombieSound.Fuse ? _preview.FuseClip() : null; if (c != null) ZombieAudio.Play(_preview.Owner, _anchor, _anchor.position, c, 1f, 1.2f, 22000f, true); else _preview.Blast(_anchor.position); return; }
            bool sfx = ev == ZombieSound.Step || ev == ZombieSound.Metal || ev == ZombieSound.Headshot;
            _preview.Play(ev, 1f, true, sfx);
        }
    }
}
