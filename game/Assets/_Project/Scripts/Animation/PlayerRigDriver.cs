using UnityEngine;
using SevenDays.Core;
using SevenDays.Player;
using SevenDays.Procedural;

namespace SevenDays.Animation
{
    /// <summary>
    /// Conecta el jugador con su rig procedural. Lee la interfaz pública de PlayerAnimatorHooks (Speed, MoveX/Y, IsAiming, IsInCover, IsVaulting, Fatigue,
    /// IsBlindFiring, Exposure...) y lo poco que no está ahí (agachado continuo, lean, mira, objetivos de las manos). Si mañana se usan clips reales, el resto del juego
    /// no cambia: solo este driver y el rig.
    /// </summary>
    [DefaultExecutionOrder(90)]   // después de WeaponHandling (pose del arma) y antes de ProceduralRig (100)
    public class PlayerRigDriver : MonoBehaviour
    {
        public ProceduralRig Rig { get; private set; }
        private PlayerAnimatorHooks _hooks;
        private PlayerMotor _motor;
        private PlayerCamera _camera;
        private PlayerCover _cover;
        private PlayerShooting _shooting;
        private PlayerVault _vault;
        private Health _health;
        private Transform _weaponPose;

        public void Initialize(ProceduralRig rig, Transform weaponPose)
        {
            Rig = rig; _weaponPose = weaponPose;
            _hooks = GetComponent<PlayerAnimatorHooks>();
            _motor = GetComponent<PlayerMotor>();
            _camera = GetComponent<PlayerCamera>();
            _cover = GetComponent<PlayerCover>();
            _shooting = GetComponent<PlayerShooting>();
            _vault = GetComponent<PlayerVault>();
            _health = GetComponent<Health>();
            _health.Damaged += OnDamaged;
            _motor.Landed += OnLanded;
            if (_shooting != null) _shooting.ShotFired += OnShot;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
            if (_motor != null) _motor.Landed -= OnLanded;
            if (_shooting != null) _shooting.ShotFired -= OnShot;
        }

        /// <summary>Dónde está la mano izquierda en cada momento de la recarga (el rig la lleva con suavidad de un punto a otro).</summary>
        private Vector3 ReloadHand(ReloadState rl, Vector3 support, SevenDays.Weapons.GunParts parts)
        {
            Vector3 magWell = parts != null ? parts.MagWorldRest : support;
            Vector3 belt = Rig.Bone(BoneId.Hips).TransformPoint(-0.2f, 0.05f, 0.12f);   // bolsillo de cargadores en la cintura izquierda
            switch (rl.Style)
            {
                case ReloadStyle.Shells:
                {
                    // cada cartucho: a la cintura y a la ventana de carga
                    if (rl.Elapsed < ReloadState.ShellLead) return support;
                    float local = ((rl.Elapsed - ReloadState.ShellLead) % rl.ShellTime) / rl.ShellTime;
                    Vector3 port = parts != null && parts.Pump != null ? parts.Pump.position + parts.transform.up * -0.04f : support;
                    return local < 0.45f ? belt : port;
                }
                case ReloadStyle.Cylinder:
                    switch (rl.LastEvent)
                    {
                        case ReloadEvent.CylinderOpen: return parts != null && parts.Cylinder != null ? parts.Cylinder.position : support;
                        case ReloadEvent.HandToBelt: return belt;
                        case ReloadEvent.MagIn: return parts != null && parts.Cylinder != null ? parts.Cylinder.position : support;
                        default: return support;
                    }
                default:   // cargador (y cerrojo + cargador)
                    switch (rl.LastEvent)
                    {
                        case ReloadEvent.BoltOpen: return parts != null && parts.Bolt != null ? parts.Bolt.position : support;
                        case ReloadEvent.MagOut: return belt;
                        case ReloadEvent.HandToBelt: return Vector3.Lerp(belt, magWell + Vector3.down * 0.1f, Mathf.Clamp01(rl.SinceEvent / 0.3f));
                        case ReloadEvent.MagIn: return magWell;
                        case ReloadEvent.Slap: return support;
                        default: return rl.Progress < 0.05f ? support : magWell;   // empieza agarrando el cargador
                    }
            }
        }

        private void OnShot()
        {
            var d = _shooting.Current.Data;
            if (!d.IsMelee) Rig.Recoil(d.recoil * 0.35f);   // el torso y los brazos acusan el retroceso
        }

        private void OnDamaged(DamageInfo info) { if (info.Amount > 0f) Rig.Hit(info.Direction, info.Amount / 20f); }
        private void OnLanded(float fall) => Rig.Land(fall);

        private void LateUpdate()
        {
            if (Rig == null || _hooks == null) return;
            var i = new RigInputs
            {
                MoveX = _hooks.Move.x, MoveY = _hooks.Move.y,
                Grounded = _hooks.IsGrounded, Aiming = _hooks.IsAiming, Sprinting = _motor.IsSprinting,
                InCover = _hooks.IsInCover, Vaulting = _hooks.IsVaulting, BlindFiring = _hooks.IsBlindFiring, Dead = _health.IsDead,
                Crouch = _motor.CrouchAmount, Fatigue = _hooks.Fatigue, Exposure = _hooks.Exposure,
                Lean = _cover != null ? _cover.Lean.Value : 0f,
                LookDirection = _camera.AimForward, DesiredForward = _motor.DesiredFacing,
                ArmSwing = 1f,
                // cobertura baja, cubierto y quieto: una rodilla al suelo (la del lado del hombro activo; Q la cambia). Al asomarse se estira desde las rodillas.
                Kneel = _cover != null && _cover.Active && !_cover.WantsPop && !_cover.IsBlindFiring && _cover.CoverTop <= 1.25f && _motor.HorizontalSpeed < 0.3f ? 1f : 0f,
                KneelSide = _camera.Shoulder.Side,
                CoverCollider = _cover != null ? _cover.CoverCollider : null,
            };

            // manos al arma según el tipo: derecha en la empuñadura; izquierda en el guardamano si el arma se lleva a dos manos (rifles siempre, pistola al apuntar)
            var data = _shooting != null ? _shooting.Current.Data : null;
            if (data != null && _weaponPose != null)
            {
                var hold = WeaponHolds.For(data.kind);
                Vector3 fingers = (_weaponPose.forward * 0.35f - _weaponPose.up).normalized;
                i.RightHand = _weaponPose.TransformPoint(0f, -0.07f, 0.02f);
                i.RightHandRot = Quaternion.LookRotation(_weaponPose.forward, -fingers);
                i.RightWeight = 1f;
                bool twoHands = !hold.OneHandedMelee && (_hooks.IsAiming ? hold.TwoHandsAim : hold.TwoHandsHip);
                Vector3 support = _weaponPose.TransformPoint(0f, -0.05f, Mathf.Clamp(data.length * hold.LeftGripFraction, 0.06f, 0.5f));
                i.LeftHand = support;
                i.LeftHandRot = Quaternion.LookRotation(_weaponPose.forward, -(_weaponPose.right * -0.6f - _weaponPose.up).normalized);
                i.LeftWeight = twoHands ? 1f : 0f;

                // recarga: la mano izquierda va al cargador, a la cintura, vuelve con el nuevo y da el golpe (o cartucho a cartucho, o al tambor/cerrojo)
                var rl = _shooting.Reload;
                if (rl.Active)
                {
                    i.LeftWeight = 1f;
                    i.LeftHand = ReloadHand(rl, support, _shooting.Parts);
                }
            }

            // vault: las dos manos se apoyan en el obstáculo durante la primera mitad
            if (_hooks.IsVaulting && _vault != null)
            {
                float t = _vault.Progress;
                i.VaultProgress = t; i.VaultTop = _vault.ObstacleTop; i.VaultDir = _vault.VaultDirection;
                float hands = Mathf.Clamp01(Mathf.Sin(Mathf.Clamp01(t / 0.65f) * Mathf.PI) * 1.6f);
                Vector3 right = Vector3.Cross(Vector3.up, _vault.VaultDirection).normalized;
                Vector3 top = _vault.ObstacleTop + Vector3.up * 0.03f;
                i.LeftHand = Vector3.Lerp(i.LeftHand, top - right * 0.25f, hands); i.LeftWeight = Mathf.Max(i.LeftWeight, hands);
                i.RightHand = Vector3.Lerp(i.RightHand, top + right * 0.25f, hands);
                i.LeftHandRot = i.RightHandRot = Quaternion.LookRotation(_vault.VaultDirection, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            }
            Rig.Inputs = i;
        }
    }
}
