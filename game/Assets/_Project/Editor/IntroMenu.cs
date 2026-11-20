using UnityEditor;
using UnityEngine;

namespace SevenDays.EditorTools
{
    /// <summary>Menú 7Days > Intro: volver a ver la intro al dar Play sin borrar el resto de PlayerPrefs (ajustes, volumen...).</summary>
    public static class IntroMenu
    {
        [MenuItem("7Days/Intro/Volver a mostrar la intro al dar Play")]
        private static void ResetSeen()
        {
            PlayerPrefs.DeleteKey(SevenDays.UI.IntroPlayer.SeenPrefKey);
            PlayerPrefs.Save();
            Debug.Log("[7Days] La intro se mostrará la próxima vez que des Play en la escena Game.");
        }
    }
}
