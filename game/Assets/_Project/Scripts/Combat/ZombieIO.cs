using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Combat
{
    /// <summary>Lee y escribe los tipos de zombie en JSON (mismo formato que el esquema zod de docs/ZOMBIES.md). Resources/Zombies/zombies.json tiene prioridad.</summary>
    public static class ZombieIO
    {
        public const string ResourcePath = "Zombies/zombies";

        public static string ToJson(System.Collections.Generic.List<ZombieTypeData> types) => JsonUtility.ToJson(new ZombieFile { types = types }, true);

        public static System.Collections.Generic.List<ZombieTypeData> Load()
        {
            var ta = Resources.Load<TextAsset>(ResourcePath);
            if (ta != null)
            {
                try
                {
                    var file = JsonUtility.FromJson<ZombieFile>(ta.text);
                    var v = ZombieValidator.Validate(file?.types);
                    if (v.Ok) { Debug.Log("[7Days] Usando los zombis de Resources/" + ResourcePath + ".json"); return file.types; }
                    Debug.LogWarning("[7Days] zombies.json inválido, se usan los de por defecto: " + string.Join("; ", v.Errors));
                }
                catch (System.Exception e) { Debug.LogWarning("[7Days] zombies.json ilegible: " + e.Message); }
            }
            return ZombieCatalog.Defaults();
        }
    }
}
