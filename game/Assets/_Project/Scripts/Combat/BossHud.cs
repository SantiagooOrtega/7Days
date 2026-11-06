using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Combat
{
    /// <summary>Barra de vida grande y fija del jefe (arriba, centrada) con su nombre, las marcas de furia al 50 % y 25 %, el aviso del ataque en curso y el punto débil.</summary>
    public class BossHud : MonoBehaviour
    {
        private float _shown = 1f, _alpha;
        private GUIStyle _name, _label;
        private ZombieController _boss;

        public static BossHud Create(Transform parent)
        {
            var go = new GameObject("BossHud");
            go.transform.SetParent(parent, false);
            return go.AddComponent<BossHud>();
        }

        private void Update()
        {
            var mgr = ZombieManager.Instance;
            if (mgr != null && mgr.ActiveBoss != null && mgr.ActiveBoss.InUse && mgr.ActiveBoss.Boss != null) _boss = mgr.ActiveBoss;
            if (_boss != null && !_boss.InUse) _boss = null;
            float target = _boss != null && !_boss.Health.IsDead ? 1f : 0f;
            _alpha = Mathf.MoveTowards(_alpha, target, Time.deltaTime * 2f);
            if (_boss != null) _shown = FeedbackMath.Smooth(_shown, _boss.Health.Fraction, Time.deltaTime, 6f);
            if (_boss != null && _boss.Health.IsDead && _alpha <= 0.01f) _boss = null;
        }

        private void OnGUI()
        {
            if (_boss == null || _alpha <= 0.01f || Event.current.type != EventType.Repaint) return;
            if (_name == null)
            {
                _name = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };
                _label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
            }
            var b = _boss.Boss;
            float w = Screen.width * 0.5f, h = 22f, x = (Screen.width - w) * 0.5f, y = 46f;
            bool raging = b != null && b.Raging;
            float pulse = raging ? 0.75f + 0.25f * Mathf.Sin(Time.time * 8f) : 1f;

            Fill(new Rect(x - 4, y - 4, w + 8, h + 8), new Color(0f, 0f, 0f, 0.75f * _alpha));
            FeedbackMath.BarColor(Mathf.Clamp01(_shown), out float r, out float g, out float bl);
            Color fillCol = raging ? new Color(0.85f * pulse + 0.1f, 0.1f, 0.08f) : new Color(0.75f, 0.12f, 0.1f);
            Fill(new Rect(x, y, w * Mathf.Clamp01(_shown), h), new Color(fillCol.r, fillCol.g, fillCol.b, _alpha));
            // marcas de las fases de furia
            var d = _boss.Type.boss;
            foreach (float mark in new[] { d.phase2At, d.phase3At }) Fill(new Rect(x + w * mark - 1.5f, y - 4, 3f, h + 8), new Color(1f, 1f, 1f, 0.8f * _alpha));

            DrawShadowed(new Rect(x, y - 36f, w, 30f), _boss.Type.displayName.ToUpperInvariant(), _name, new Color(1f, 0.92f, 0.85f, _alpha));
            string label = b != null ? b.TelegraphLabel : "";
            if (!string.IsNullOrEmpty(label))
                DrawShadowed(new Rect(x, y + h + 6f, w, 28f), label, _label, new Color(1f, 0.3f + 0.4f * pulse, 0.2f, _alpha));
            else if (b != null && b.Brain != null && b.Brain.WeakPointExposed)
                DrawShadowed(new Rect(x, y + h + 6f, w, 28f), "¡PUNTO DÉBIL EXPUESTO!", _label, new Color(1f, 0.75f, 0.2f, _alpha));
        }

        private static void DrawShadowed(Rect r, string text, GUIStyle style, Color c)
        {
            style.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.8f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, style);
            style.normal.textColor = c;
            GUI.Label(r, text, style);
        }

        private static void Fill(Rect r, Color c)
        {
            var prev = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = prev;
        }
    }
}
