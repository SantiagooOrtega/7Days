using System.Collections.Generic;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Weapons
{
    /// <summary>
    /// ScriptableObject de UN arma: daño, cadencia, cargador, retroceso, dispersión, alcance, rareza y tipo de munición. Hay uno por arma en
    /// Assets/_Project/Resources/Weapons (se crean solos la primera vez; menú 7Days > Crear armas por defecto). Se editan en el Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "7Days/Arma", fileName = "Arma")]
    public class WeaponAsset : ScriptableObject
    {
        public WeaponData Data = new WeaponData();
    }

    /// <summary>Catálogo de armas en tiempo de juego: los ScriptableObjects de Resources/Weapons mandan; lo que falte sale de los valores por defecto.</summary>
    public static class WeaponLibrary
    {
        private static Dictionary<string, WeaponData> _all;

        public static IReadOnlyCollection<WeaponData> All
        {
            get { Build(); return _all.Values; }
        }

        public static WeaponData Get(string id)
        {
            Build();
            return _all.TryGetValue(id, out var w) ? w : null;
        }

        private static void Build()
        {
            if (_all != null) return;
            _all = new Dictionary<string, WeaponData>();
            foreach (var d in WeaponCatalog.Defaults()) _all[d.id] = d;
            foreach (var asset in Resources.LoadAll<WeaponAsset>("Weapons"))
            {
                if (asset == null || asset.Data == null || string.IsNullOrEmpty(asset.Data.id)) continue;
                var copy = JsonUtility.FromJson<WeaponData>(JsonUtility.ToJson(asset.Data)); // copia: el juego no modifica el asset
                if (copy.dataVersion < WeaponData.CurrentDataVersion && !WeaponCatalog.Migrate(copy))
                {
                    Debug.LogWarning("[7Days] El arma '" + asset.Data.id + "' en Resources/Weapons es demasiado antigua: se usan los valores por defecto (abre el editor para actualizarla).");
                    continue;
                }
                copy.Validate();
                _all[copy.id] = copy;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _all = null;
    }
}
