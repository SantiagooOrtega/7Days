using Unity.Cinemachine;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Player
{
    /// <summary>
    /// Sacudidas de cámara con Cinemachine Impulse: un pequeño golpe al saltar y otro, proporcional a la altura de la caída,
    /// al aterrizar. El CinemachineImpulseListener está en la cámara virtual (ver PlayerCamera).
    /// </summary>
    public class PlayerImpulse : MonoBehaviour
    {
        private CinemachineImpulseSource _source;
        private MovementSettings _s;

        public void Initialize(MovementSettings settings)
        {
            _s = settings;
            _source = gameObject.AddComponent<CinemachineImpulseSource>();
            var def = _source.ImpulseDefinition;
            def.ImpulseChannel = 1;
            def.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform; // se siente igual en todas partes
            def.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            def.ImpulseDuration = 0.3f;
            _source.ImpulseDefinition = def;
        }

        public void Jump() => Shake(_s.JumpImpulse);
        public void Land(float fallDistance) => Shake(FallDamage.LandImpulse(fallDistance, _s));
        public void Vault() => Shake(_s.JumpImpulse * 0.7f);

        public void Shake(float strength)
        {
            if (_source == null || strength <= 0f) return;
            _source.GenerateImpulseWithVelocity(Vector3.down * strength);
        }
    }
}
