using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.World
{
    /// <summary>Lee y escribe la distribución de props en JSON (el mismo formato que traerá un mapa de la base de datos).</summary>
    public static class PropLayoutIO
    {
        public static string ToJson(PropLayoutData data) => JsonUtility.ToJson(data, true);

        public static PropLayoutData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonUtility.FromJson<PropLayoutData>(json); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[7Days] JSON de props inválido: " + e.Message);
                return null;
            }
        }

        /// <summary>Distribución desde Resources/{path}.json si existe; si no, null.</summary>
        public static PropLayoutData LoadOverride(string resourcePath)
        {
            var ta = Resources.Load<TextAsset>(resourcePath);
            return ta != null ? FromJson(ta.text) : null;
        }
    }
}
