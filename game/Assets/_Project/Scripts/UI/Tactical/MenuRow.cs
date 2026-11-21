using UnityEngine;
using UnityEngine.EventSystems;

namespace SevenDays.UI.Tactical
{
    /// <summary>Una fila de MenuList: el ratón encima la selecciona y el clic la confirma.</summary>
    public class MenuRow : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public MenuList List; public int Index;
        public void OnPointerEnter(PointerEventData e) { if (List != null) List.Select(Index, true); }
        public void OnPointerClick(PointerEventData e) { if (List != null) { List.Select(Index, false); List.Confirm(); } }
    }
}
