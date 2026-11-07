using System.Collections.Generic;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Animation
{
    /// <summary>
    /// Variantes jugables: las de Resources/Config/Variants (ScriptableObject, editables) sustituyen por id a las de código; cualquier otra se añade. La elegida se
    /// guarda en PlayerPrefs ("7d.variant"); por defecto, la Calavera. Cada una se valida al cargar (colores y estadísticas en rango).
    /// </summary>
    public static class CharacterVariantLibrary
    {
        public const string ResourceDir = "Config/Variants", PrefKey = "7d.variant";
        private static List<CharacterVariantData> _all;

        public static IReadOnlyList<CharacterVariantData> All
        {
            get
            {
                if (_all != null) return _all;
                _all = CharacterVariants.Defaults();
                foreach (var a in Resources.LoadAll<CharacterVariantAsset>(ResourceDir))
                {
                    if (a == null || a.Data == null) continue;
                    var d = JsonUtility.FromJson<CharacterVariantData>(JsonUtility.ToJson(a.Data));
                    d.Validate();
                    int i = _all.FindIndex(x => x.id == d.id);
                    if (i >= 0) _all[i] = d; else _all.Add(d);
                }
                foreach (var v in _all) v.Validate();
                return _all;
            }
        }

        public static CharacterVariantData Current => Get(PlayerPrefs.GetString(PrefKey, "calavera"));

        public static CharacterVariantData Get(string id)
        {
            foreach (var v in All) if (v.id == id) return v;
            return All[0];
        }

        public static void SetCurrent(string id) => PlayerPrefs.SetString(PrefKey, Get(id).id);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _all = null;
    }
}
