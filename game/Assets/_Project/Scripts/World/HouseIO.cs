using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.World
{
    /// <summary>Lee y escribe la casa en JSON (mismo formato que traerá un mapa de la base de datos). Resources/Layouts/house.json tiene prioridad.</summary>
    public static class HouseIO
    {
        public const string ResourcePath = "Layouts/house";

        public static string ToJson(HouseLayoutData d) => JsonUtility.ToJson(d, true);

        public static HouseLayoutData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonUtility.FromJson<HouseLayoutData>(json); }
            catch (System.Exception e) { Debug.LogWarning("[7Days] JSON de la casa inválido: " + e.Message); return null; }
        }

        /// <summary>La casa de Resources/Layouts/house.json si existe y es válida; si no, la de por defecto.</summary>
        public static HouseLayoutData Load()
        {
            var ta = Resources.Load<TextAsset>(ResourcePath);
            if (ta != null)
            {
                var l = FromJson(ta.text);
                var v = HouseValidator.Validate(l);
                if (v.Ok) { Debug.Log("[7Days] Usando la casa de Resources/" + ResourcePath + ".json"); return l; }
                Debug.LogWarning("[7Days] house.json inválido, se usa la casa por defecto: " + string.Join("; ", v.Errors));
            }
            return HouseLayoutData.Default();
        }

        /// <summary>Zona rectangular que ocupa la casa (más un margen), para que la vegetación y los props no se coloquen encima.</summary>
        public static Zone FootprintZone(HouseLayoutData l, float margin = 1.5f)
        {
            bool swap = Mathf.Abs(Mathf.Round(l.yaw / 90f)) % 2 == 1;
            float hx = (swap ? l.depth : l.width) / 2f + margin, hz = (swap ? l.width : l.depth) / 2f + margin;
            return Zone.Rect(l.x - hx, l.z - hz, l.x + hx, l.z + hz);
        }
    }
}
