using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SevenDays.UI.Tactical
{
    /// <summary>
    /// Lista de opciones: etiquetas en mayúsculas crema, fila activa con degradado #B3261A→#FF7A1F, texto blanco, triángulo ► que late y una barra de luz que
    /// recorre la fila al cambiar (0.15 s); las filas entran escalonadas (0.08 s). Sonido seco al mover. Teclado: flechas arriba/abajo (W/S), Enter confirma, izquierda/derecha ajustan (opciones),
    /// Esc vuelve. Ratón: encima selecciona, clic confirma. Sin memoria por frame (solo cambia colores al mover).
    /// </summary>
    public class MenuList : MonoBehaviour
    {
        public const float RowHeight = 54f;
        public event Action<int> Selected, Confirmed;
        public event Action<int, int> Adjusted;
        public event Action Back;
        public bool Focused = true;
        public int Index { get; private set; }
        public int Count => _labels.Count;

        private readonly List<Text> _labels = new List<Text>();
        private readonly List<Text> _values = new List<Text>();
        private RectTransform _highlight;
        private float _width, _rowH = RowHeight;
        private RectTransform _arrowL, _arrowR, _marker, _sweep;
        private Image _highlightImg, _sweepImg;
        private Text _markerTxt;
        private readonly List<CanvasGroup> _rows = new List<CanvasGroup>();
        private float _born = -1f, _sweepAt = -10f;
        public const float SweepTime = 0.15f, StaggerTime = 0.08f, EnterTime = 0.22f;
        private readonly List<bool> _adjustable = new List<bool>();
        private Vector2 _arrowLBase, _arrowRBase;

        public float RowH => _rowH;

        public static MenuList Create(Transform parent, Vector2 pos, float width, IList<string> items, IList<string> values = null,
            float rowHeight = RowHeight, int labelSize = 30, int valueSize = 26)
        {
            float RowHeight = rowHeight;
            var r = UiKit.Rect(parent, "Lista", new Vector2(0.5f, 0.5f), pos, new Vector2(width, items.Count * RowHeight));
            var l = r.gameObject.AddComponent<MenuList>(); l._width = width; l._rowH = rowHeight;
            l._highlightImg = UiKit.Image(r, "Resaltado", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(width, RowHeight - 6f), Color.white, UiKit.RowSprite);
            l._highlight = l._highlightImg.rectTransform;
            // barra de luz que recorre la fila activa al cambiar
            l._sweepImg = UiKit.Image(l._highlight, "Barra de luz", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(60f, RowHeight - 6f), new Color(1f, 0.85f, 0.6f, 0f));
            l._sweep = l._sweepImg.rectTransform;
            // triángulo ► de la fila activa (late hacia la derecha)
            l._markerTxt = UiKit.Text(r, "►", Mathf.Max(UiKit.MinFont, labelSize * 2 / 3), Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(36f, RowHeight - 6f), true, false);
            l._marker = l._markerTxt.rectTransform; l._marker.anchorMin = l._marker.anchorMax = new Vector2(0.5f, 1f); l._marker.name = "Marcador";
            for (int i = 0; i < items.Count; i++)
            {
                var row = UiKit.Image(r, "Fila " + items[i], new Vector2(0.5f, 1f), new Vector2(0f, -RowHeight * (i + 0.5f)), new Vector2(width, RowHeight - 6f), new Color(0, 0, 0, 0.001f));
                row.raycastTarget = true;
                l._rows.Add(row.gameObject.AddComponent<CanvasGroup>());
                var mr = row.gameObject.AddComponent<MenuRow>(); mr.List = l; mr.Index = i;
                // etiqueta en la mitad izquierda y valor en la derecha, sin solaparse (el texto condensado se escala desde su centro)
                float c = UiKit.Condensed;
                l._labels.Add(UiKit.Text(row.transform, SevenDays.Procedural.SelectContent.Tracked(items[i].ToUpperInvariant()), labelSize, UiKit.Cream, TextAnchor.MiddleLeft, new Vector2(-width / 2f + 56f + width * 0.18f, 0f), new Vector2(width * 0.36f / c, Mathf.Max(44f, labelSize * 1.4f)), true));
                var lb = l._labels[l._labels.Count - 1];   // etiquetas espaciadas: se encogen si no caben en su caja
                lb.horizontalOverflow = HorizontalWrapMode.Wrap; lb.verticalOverflow = VerticalWrapMode.Truncate;
                lb.resizeTextForBestFit = true; lb.resizeTextMinSize = UiKit.MinFont; lb.resizeTextMaxSize = labelSize;
                if (values == null) lb.rectTransform.sizeDelta = new Vector2((width - 80f) / c, lb.rectTransform.sizeDelta.y);
                if (values == null) lb.rectTransform.anchoredPosition = new Vector2(-width / 2f + 56f + (width - 80f) / 2f, 0f);   // hueco para el ►
                Text v = null;
                if (values != null && i < values.Count)
                {
                    v = UiKit.Text(row.transform, values[i], valueSize, UiKit.Cream, TextAnchor.MiddleRight, new Vector2(width / 2f - 20f - width * 0.24f, 0f), new Vector2(width * 0.48f / c, Mathf.Max(44f, valueSize * 1.4f)));
                    v.horizontalOverflow = HorizontalWrapMode.Wrap; v.verticalOverflow = VerticalWrapMode.Truncate;
                    v.resizeTextForBestFit = true; v.resizeTextMinSize = UiKit.MinFont; v.resizeTextMaxSize = valueSize;   // listas largas (armas usadas) se encogen o pasan a dos líneas
                    var lab = l._labels[l._labels.Count - 1];   // etiquetas largas: se encogen en su mitad en vez de pisar el valor
                    lab.horizontalOverflow = HorizontalWrapMode.Wrap; lab.verticalOverflow = VerticalWrapMode.Truncate;
                    lab.resizeTextForBestFit = true; lab.resizeTextMinSize = UiKit.MinFont; lab.resizeTextMaxSize = labelSize;
                }
                l._values.Add(v);
            }
            l.Select(0, false);
            return l;
        }

        /// <summary>Flechas ◄ ► animadas a los lados del valor de la fila activa cuando se puede cambiar con ←→ (solo se mueven, sin memoria por frame).</summary>
        public void EnableArrows(params int[] rows)
        {
            for (int i = 0; i < _labels.Count; i++) _adjustable.Add(System.Array.IndexOf(rows, i) >= 0);
            var r = (RectTransform)transform;
            float valueLeft = _width / 2f - 20f - _width * 0.48f, valueRight = _width / 2f - 20f;
            _arrowLBase = new Vector2(valueLeft - 22f, 0f); _arrowRBase = new Vector2(valueRight + 18f, 0f);
            _arrowL = UiKit.Text(r, "◄", 28, UiKit.White, TextAnchor.MiddleCenter, _arrowLBase, new Vector2(30f, 40f), true, false).rectTransform;
            _arrowR = UiKit.Text(r, "►", 28, UiKit.White, TextAnchor.MiddleCenter, _arrowRBase, new Vector2(30f, 40f), true, false).rectTransform;
            _arrowL.anchorMin = _arrowL.anchorMax = _arrowR.anchorMin = _arrowR.anchorMax = new Vector2(0.5f, 1f);
            PlaceArrows();
        }

        private void PlaceArrows()
        {
            if (_arrowL == null) return;
            bool on = Index < _adjustable.Count && _adjustable[Index];
            _arrowL.gameObject.SetActive(on); _arrowR.gameObject.SetActive(on);
            float y = -_rowH * (Index + 0.5f);
            _arrowLBase.y = _arrowRBase.y = y;
            var v = Index < _values.Count ? _values[Index] : null;
            if (on && v != null)
            {
                // la flecha izquierda va junto al texto real (alineado a la derecha), no en el borde de su caja
                float pw = Mathf.Min(v.preferredWidth * UiKit.Condensed, _width * 0.48f);
                _arrowLBase.x = _width / 2f - 20f - pw - 22f;
            }
            _arrowL.anchoredPosition = _arrowLBase; _arrowR.anchoredPosition = _arrowRBase;
        }

        public void SetValue(int i, string v) { if (i >= 0 && i < _values.Count && _values[i] != null) { _values[i].text = v; if (i == Index) PlaceArrows(); } }
        public void SetLabel(int i, string v) { if (i >= 0 && i < _labels.Count) _labels[i].text = SevenDays.Procedural.SelectContent.Tracked(v.ToUpperInvariant()); }

        /// <summary>Línea fina gris bajo la fila "after" (separa bloques de opciones).</summary>
        public void AddSeparator(int after)
        {
            var r = (RectTransform)transform;
            UiKit.Image(r, "Separador", new Vector2(0.5f, 1f), new Vector2(-_width * 0.15f, -_rowH * (after + 1)), new Vector2(_width * 0.7f, 2f), UiKit.Grey);
        }

        public void Select(int i, bool sound)
        {
            if (_labels.Count == 0) return;
            i = (i % _labels.Count + _labels.Count) % _labels.Count;
            if (i != Index && sound) UiSounds.Move();
            if (i != Index) _sweepAt = Time.unscaledTime;
            _labels[Index].color = UiKit.Cream; if (_values[Index] != null) _values[Index].color = UiKit.Cream;
            Index = i;
            _labels[i].color = UiKit.White; if (_values[i] != null) _values[i].color = UiKit.White;   // la fila activa, en blanco sobre el degradado
            _highlight.anchoredPosition = new Vector2(0f, -_rowH * (i + 0.5f));
            _marker.anchoredPosition = new Vector2(-_width / 2f + 26f, -_rowH * (i + 0.5f));
            PlaceArrows();
            Selected?.Invoke(i);
        }

        public void Confirm() { UiSounds.Accept(); Confirmed?.Invoke(Index); }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (_born < 0f) _born = now;
            // entrada escalonada de las filas
            for (int i = 0; i < _rows.Count; i++) { float a = Mathf.Clamp01((now - _born - i * StaggerTime) / EnterTime); if (_rows[i].alpha != a) _rows[i].alpha = a; }
            // la fila activa late suavemente; el ► se mece; la barra de luz cruza la fila tras cambiar
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 4f);
            _highlightImg.color = new Color(1f, 1f, 1f, 0.85f + 0.15f * pulse);
            _marker.anchoredPosition = new Vector2(-_width / 2f + 26f + 4f * pulse, -_rowH * (Index + 0.5f));
            float sw = (now - _sweepAt) / SweepTime;
            _sweepImg.color = new Color(1f, 0.85f, 0.6f, sw < 1f ? 0.55f * (1f - sw) : 0f);
            _sweep.anchoredPosition = new Vector2(Mathf.Clamp01(sw) * (_width - 60f), 0f);
            if (_arrowL != null && _arrowL.gameObject.activeSelf)
            {
                float o = Mathf.Sin(Time.unscaledTime * 5f) * 4f;
                _arrowL.anchoredPosition = new Vector2(_arrowLBase.x - o, _arrowLBase.y);
                _arrowR.anchoredPosition = new Vector2(_arrowRBase.x + o, _arrowRBase.y);
            }
            if (!Focused) return;
            var k = Keyboard.current; if (k == null) return;
            if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) Select(Index + 1, true);
            else if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) Select(Index - 1, true);
            else if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) Confirm();
            else if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) { UiSounds.Move(); Adjusted?.Invoke(Index, -1); }
            else if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame) { UiSounds.Move(); Adjusted?.Invoke(Index, 1); }
            else if (k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame) { UiSounds.Back(); Back?.Invoke(); }
        }
    }
}
