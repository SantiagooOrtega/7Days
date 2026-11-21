using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SevenDays.UI.Tactical
{
    /// <summary>
    /// Lista de opciones con la fila activa resaltada en rojo/naranja. Teclado: flechas arriba/abajo (W/S), Enter confirma, izquierda/derecha ajustan (opciones),
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
        private RectTransform _arrowL, _arrowR, _marker;
        private Image _highlightImg, _markerImg;
        private readonly List<bool> _adjustable = new List<bool>();
        private Vector2 _arrowLBase, _arrowRBase;

        public float RowH => _rowH;

        public static MenuList Create(Transform parent, Vector2 pos, float width, IList<string> items, IList<string> values = null,
            float rowHeight = RowHeight, int labelSize = 30, int valueSize = 26)
        {
            float RowHeight = rowHeight;
            var r = UiKit.Rect(parent, "Lista", new Vector2(0.5f, 0.5f), pos, new Vector2(width, items.Count * RowHeight));
            var l = r.gameObject.AddComponent<MenuList>(); l._width = width; l._rowH = rowHeight;
            l._highlightImg = UiKit.Image(r, "Resaltado", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(width, RowHeight - 6f), Color.white, UiKit.HighlightSprite);
            l._highlight = l._highlightImg.rectTransform;
            // marcador de la fila activa: cuadrado rojo a la izquierda que late
            l._markerImg = UiKit.Image(r, "Marcador", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(12f, 12f), UiKit.Red);
            l._marker = l._markerImg.rectTransform; l._marker.localRotation = Quaternion.Euler(0f, 0f, 45f);
            for (int i = 0; i < items.Count; i++)
            {
                var row = UiKit.Image(r, "Fila " + items[i], new Vector2(0.5f, 1f), new Vector2(0f, -RowHeight * (i + 0.5f)), new Vector2(width, RowHeight - 6f), new Color(0, 0, 0, 0.001f));
                row.raycastTarget = true;
                var mr = row.gameObject.AddComponent<MenuRow>(); mr.List = l; mr.Index = i;
                // etiqueta en la mitad izquierda y valor en la derecha, sin solaparse (el texto condensado se escala desde su centro)
                float c = UiKit.Condensed;
                l._labels.Add(UiKit.Text(row.transform, SevenDays.Procedural.SelectContent.Tracked(items[i].ToUpperInvariant()), labelSize, UiKit.CreamDim, TextAnchor.MiddleLeft, new Vector2(-width / 2f + 24f + width * 0.21f, 0f), new Vector2(width * 0.42f / c, Mathf.Max(44f, labelSize * 1.4f)), true));
                var lb = l._labels[l._labels.Count - 1];   // etiquetas espaciadas: se encogen si no caben en su caja
                lb.horizontalOverflow = HorizontalWrapMode.Wrap; lb.verticalOverflow = VerticalWrapMode.Truncate;
                lb.resizeTextForBestFit = true; lb.resizeTextMinSize = 16; lb.resizeTextMaxSize = labelSize;
                if (values == null) lb.rectTransform.sizeDelta = new Vector2((width - 60f) / c, lb.rectTransform.sizeDelta.y);
                if (values == null) lb.rectTransform.anchoredPosition = new Vector2(-width / 2f + 40f + (width - 60f) / 2f, 0f);
                Text v = null;
                if (values != null && i < values.Count)
                {
                    v = UiKit.Text(row.transform, values[i], valueSize, UiKit.Cream, TextAnchor.MiddleRight, new Vector2(width / 2f - 20f - width * 0.24f, 0f), new Vector2(width * 0.48f / c, Mathf.Max(44f, valueSize * 1.4f)));
                    v.horizontalOverflow = HorizontalWrapMode.Wrap; v.verticalOverflow = VerticalWrapMode.Truncate;
                    v.resizeTextForBestFit = true; v.resizeTextMinSize = 14; v.resizeTextMaxSize = valueSize;   // listas largas (armas usadas) se encogen o pasan a dos líneas
                    var lab = l._labels[l._labels.Count - 1];   // etiquetas largas: se encogen en su mitad en vez de pisar el valor
                    lab.horizontalOverflow = HorizontalWrapMode.Wrap; lab.verticalOverflow = VerticalWrapMode.Truncate;
                    lab.resizeTextForBestFit = true; lab.resizeTextMinSize = 16; lab.resizeTextMaxSize = labelSize;
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
            _arrowL = UiKit.Text(r, "◄", 28, UiKit.Orange, TextAnchor.MiddleCenter, _arrowLBase, new Vector2(30f, 40f), true, false).rectTransform;
            _arrowR = UiKit.Text(r, "►", 28, UiKit.Orange, TextAnchor.MiddleCenter, _arrowRBase, new Vector2(30f, 40f), true, false).rectTransform;
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
            _labels[Index].color = UiKit.CreamDim;
            Index = i;
            _labels[i].color = UiKit.White;
            _highlight.anchoredPosition = new Vector2(0f, -_rowH * (i + 0.5f));
            _marker.anchoredPosition = new Vector2(-_width / 2f + 16f, -_rowH * (i + 0.5f));
            PlaceArrows();
            Selected?.Invoke(i);
        }

        public void Confirm() { UiSounds.Accept(); Confirmed?.Invoke(Index); }

        private void Update()
        {
            // la fila activa late suavemente (brillo del resaltado y del marcador)
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            _highlightImg.color = new Color(1f, 1f, 1f, 0.8f + 0.2f * pulse);
            _markerImg.color = new Color(UiKit.Red.r + 0.3f * pulse, UiKit.Red.g + 0.1f * pulse, UiKit.Red.b, 1f);
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
