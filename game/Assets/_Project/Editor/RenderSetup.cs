using UnityEditor;
using UnityEngine;

namespace SevenDays.EditorTools
{
    /// <summary>
    /// Activa "Frame Timing Stats" (Player Settings) para que el FrameTimingManager dé el tiempo de GPU también en una build: lo usan la resolución dinámica y
    /// el panel Render de F6. Se hace por la API del editor (no editando ProjectSettings a mano, que el editor abierto podría pisar).
    /// </summary>
    [InitializeOnLoad]
    public static class RenderSetup
    {
        static RenderSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying || PlayerSettings.enableFrameTimingStats) return;
                PlayerSettings.enableFrameTimingStats = true;
                Debug.Log("[7Days] Activado Player Settings > Frame Timing Stats (tiempo de GPU para la resolución dinámica).");
            };
        }
    }
}
