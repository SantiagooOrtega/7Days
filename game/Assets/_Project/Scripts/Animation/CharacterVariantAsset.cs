using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Animation
{
    /// <summary>Variante jugable en un ScriptableObject (Resources/Config/Variants): paleta, pieza de cabeza y estadísticas. Se crea sola desde los valores por defecto.</summary>
    [CreateAssetMenu(menuName = "7Days/Variante de personaje", fileName = "Variante")]
    public class CharacterVariantAsset : ScriptableObject
    {
        public CharacterVariantData Data = new CharacterVariantData();
    }
}
