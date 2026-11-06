using Unity.Cinemachine;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.Player
{
    /// <summary>
    /// Sacudidas de cámara con Cinemachine Impulse (el CinemachineImpulseListener está en la cámara virtual, ver PlayerCamera). Tres fuentes con formas distintas:
    /// golpe (salto, aterrizaje, vault, pasos corriendo), retroceso (disparo) y explosión (daño recibido). Todas se escalan con los ajustes de cámara y con la
    /// opción de accesibilidad "Movimiento de cámara".
    /// </summary>
    public class PlayerImpulse : MonoBehaviour
    {
        private CinemachineImpulseSource _bump, _recoil, _hit;
        private MovementSettings _s;
        private CameraSettingsData _cam;

        public void Initialize(MovementSettings settings, CameraSettingsData camera = null)
        {
            _s = settings;
            _cam = camera ?? new CameraSettingsData();
            _bump = Make(CinemachineImpulseDefinition.ImpulseShapes.Bump, 0.3f);
            _recoil = Make(CinemachineImpulseDefinition.ImpulseShapes.Recoil, 0.14f);
            _hit = Make(CinemachineImpulseDefinition.ImpulseShapes.Explosion, 0.28f);
            var health = GetComponent<Health>();
            if (health != null) health.Damaged += OnDamaged;
        }

        private void OnDestroy() { var h = GetComponent<Health>(); if (h != null) h.Damaged -= OnDamaged; }

        private CinemachineImpulseSource Make(CinemachineImpulseDefinition.ImpulseShapes shape, float duration)
        {
            var src = gameObject.AddComponent<CinemachineImpulseSource>();
            var def = src.ImpulseDefinition;
            def.ImpulseChannel = 1;
            def.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform; // se siente igual en todas partes
            def.ImpulseShape = shape;
            def.ImpulseDuration = duration;
            src.ImpulseDefinition = def;
            return src;
        }

        private float Scale => _cam.shakeScale * _cam.motionScale;

        public void Jump() => Emit(_bump, Vector3.down * _s.JumpImpulse);
        public void Land(float fallDistance) => Emit(_bump, Vector3.down * FallDamage.LandImpulse(fallDistance, _s) * _cam.landShakeScale);
        public void Vault() => Emit(_bump, Vector3.down * _s.JumpImpulse * 0.7f);
        /// <summary>Un paso corriendo (lo llama la cámara de carrera en cada pisada).</summary>
        public void RunStep(float weight) => Emit(_bump, Vector3.down * _cam.runStepShake * weight);
        /// <summary>Disparo: hacia atrás y un poco hacia arriba.</summary>
        public void Fire(float strength) => Emit(_recoil, new Vector3(0f, 0.6f, -1f) * strength * _cam.fireShakeScale);

        /// <summary>Sacudida genérica hacia abajo (explosiones, golpes de jefes, aterrizajes...).</summary>
        public void Shake(float strength) => Emit(_bump, Vector3.down * strength);

        private void OnDamaged(DamageInfo info)
        {
            if (info.Amount <= 0f) return;
            Vector3 dir = info.Direction.sqrMagnitude > 0.01f ? info.Direction.normalized : Random.onUnitSphere;
            Emit(_hit, dir * _cam.damageShake * Mathf.Clamp(info.Amount / 20f, 0.3f, 2.5f));
        }

        private void Emit(CinemachineImpulseSource src, Vector3 velocity)
        {
            if (src == null) return;
            float k = Scale;
            if (k <= 0f || velocity.sqrMagnitude < 1e-8f) return;
            src.GenerateImpulseWithVelocity(velocity * k);
        }
    }
}
