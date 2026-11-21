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
        private float _width;

        public static MenuList Create(Transform parent, Vector2 pos, float width, IList<string> items, IList<string> values = null)
        {
            var r = UiKit.Rect(parent, "Lista", new Vector2(0.5f, 0.5f), pos, new Vector2(width, items.Count * RowHeight));
            var l = r.gameObject.AddComponent<MenuList>(); l._width = width;
            l._highlight = UiKit.Image(r, "Resaltado", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(width, RowHeight - 6f), Color.white, UiKit.HighlightSprite).rectTransform;
            for (int i = 0; i < items.Count; i++)
            {
                var row = UiKit.Image(r, "Fila " + items[i], new Vector2(0.5f, 1f), new Vector2(0f, -RowHeight * (i + 0.5f)), new Vector2(width, RowHeight - 6f), new Color(0, 0, 0, 0.001f));
                row.raycastTarget = true;
                var mr = row.gameObject.AddComponent<MenuRow>(); mr.List = l; mr.Index = i;
                // etiqueta en la mitad izquierda y valor en la derecha, sin solaparse (el texto condensado se escala desde su centro)
                float c = UiKit.Condensed;
                l._labels.Add(UiKit.Text(row.transform, items[i].ToUpperInvariant(), 30, UiKit.CreamDim, TextAnchor.MiddleLeft, new Vector2(-width / 2f + 24f + width * 0.21f, 0f), new Vector2(width * 0.42f / c, 44f), true));
                Text v = null;
                if (values != null && i < values.Count)
                {
                    v = UiKit.Text(row.transform, values[i], 26, UiKit.Cream, TextAnchor.MiddleRight, new Vector2(width / 2f - 20f - width * 0.24f, 0f), new Vector2(width * 0.48f / c, 44f));
                    v.horizontalOverflow = HorizontalWrapMode.Wrap; v.verticalOverflow = VerticalWrapMode.Truncate;
                    v.resizeTextForBestFit = true; v.resizeTextMinSize = 14; v.resizeTextMaxSize = 26;   // listas largas (armas usadas) se encogen o pasan a dos líneas
                    var lab = l._labels[l._labels.Count - 1];   // etiquetas largas: se encogen en su mitad en vez de pisar el valor
                    lab.horizontalOverflow = HorizontalWrapMode.Wrap; lab.verticalOverflow = VerticalWrapMode.Truncate;
                    lab.resizeTextForBestFit = true; lab.resizeTextMinSize = 16; lab.resizeTextMaxSize = 30;
                }
                l._values.Add(v);
            }
            l.Select(0, false);
            return l;
        }

        public void SetValue(int i, string v) { if (i >= 0 && i < _values.Count && _values[i] != null) _values[i].text = v; }
        public void SetLabel(int i, string v) { if (i >= 0 && i < _labels.Count) _labels[i].text = v.ToUpperInvariant(); }

        public void Select(int i, bool sound)
        {
            if (_labels.Count == 0) return;
            i = (i % _labels.Count + _labels.Count) % _labels.Count;
            if (i != Index && sound) UiSounds.Move();
            _labels[Index].color = UiKit.CreamDim;
            Index = i;
            _labels[i].color = UiKit.Cream;
            _highlight.anchoredPosition = new Vector2(0f, -RowHeight * (i + 0.5f));
            Selected?.Invoke(i);
        }

        public void Confirm() { UiSounds.Accept(); Confirmed?.Invoke(Index); }

        private void Update()
        {
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
