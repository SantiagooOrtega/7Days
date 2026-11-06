using System.Collections.Generic;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.World.Loot
{
    /// <summary>Lee y escribe el botín en JSON (mismo formato que podrán definir el backend y Gemini offline). Resources/Loot/loot.json tiene prioridad.</summary>
    public static class LootIO
    {
        public const string ResourcePath = "Loot/loot";

        public static string ToJson(LootLayoutData d) => JsonUtility.ToJson(d, true);

        public static LootLayoutData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonUtility.FromJson<LootLayoutData>(json); }
            catch (System.Exception e) { Debug.LogWarning("[7Days] JSON de botín inválido: " + e.Message); return null; }
        }

        /// <summary>El botín de Resources/Loot/loot.json si existe y es válido; si no, el de por defecto (con coches y arbustos de este mapa).</summary>
        public static LootLayoutData Load(IList<PropEntry> props, HouseLayoutData house)
        {
            var ta = Resources.Load<TextAsset>(ResourcePath);
            if (ta != null)
            {
                var l = FromJson(ta.text);
                var v = LootValidator.Validate(l);
                if (v.Ok) { Debug.Log("[7Days] Usando el botín de Resources/" + ResourcePath + ".json"); return l; }
                Debug.LogWarning("[7Days] loot.json inválido, se usa el de por defecto: " + string.Join("; ", v.Errors));
            }
            return LootDefaults.Create(props, house);
        }
    }
}
