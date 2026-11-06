using Unity.Cinemachine;
using UnityEngine;

namespace SevenDays.Player
{
    /// <summary>
    /// Extensión de Cinemachine 3 que aplica la cámara de carrera (balanceo, roll, rebote, jadeo y FOV extra) DESPUÉS del cuerpo (que ya resolvió la colisión con
    /// paredes) y junto al ruido, sin tocar el Impulse (que se suma aparte). Solo lee la salida ya calculada por RunCamModel en PlayerCamera: sin alocaciones.
    /// </summary>
    [AddComponentMenu("")]
    public class RunCamExtension : CinemachineExtension
    {
        public PlayerCamera Source;

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Noise || Source == null) return;
            var o = Source.RunOutput;
            Quaternion rot = state.GetFinalOrientation();
            state.PositionCorrection += rot * new Vector3(o.OffsetX, o.OffsetY, 0f);
            state.OrientationCorrection = state.OrientationCorrection * Quaternion.Euler(o.Pitch, 0f, o.Roll);
            state.Lens.FieldOfView += o.FovDelta;
        }
    }
}
