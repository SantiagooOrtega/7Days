using UnityEngine;
using SevenDays.Procedural;
using SevenDays.World;

namespace SevenDays.Player
{
    /// <summary>
    /// Vault y mantle automáticos sobre obstáculos marcados con Vaultable. Se busca el obstáculo con un SphereCast hacia delante,
    /// se mide la altura de su superficie superior con un raycast hacia abajo y se comprueba que haya espacio libre arriba y
    /// donde se aterriza (CheckCapsule). La decisión (saltar, trepar o no se puede) es VaultMath, que tiene pruebas EditMode.
    /// Durante el vault el CharacterController se apaga y la posición sigue el recorrido; no se puede disparar.
    /// </summary>
    public class PlayerVault : MonoBehaviour
    {
        public bool IsVaulting { get; private set; }
        public VaultVerdict LastVerdict { get; private set; }

        private struct Plan
        {
            public VaultVerdict Verdict;
            public Vector3 Start, Top, End, Dir;
            public float Duration;
        }

        private const float CrouchedHeight = 1.1f;
        private MovementSettings _s;
        private CharacterController _cc;
        private PlayerCover _cover;
        private PlayerMotor _motor;
        private PlayerImpulse _impulse;
        private Plan _plan;
        private float _t;

        public void Initialize(MovementSettings settings, PlayerCover cover, PlayerMotor motor, PlayerImpulse impulse)
        {
            _s = settings;
            _cover = cover;
            _motor = motor;
            _impulse = impulse;
            _cc = GetComponent<CharacterController>();
        }

        // ------------------------------------------------------------------ entradas
        /// <summary>Espacio pulsado: salta un obstáculo vaultable de delante (o trepa uno de 1.3 a 1.6 m).</summary>
        public bool TryStartFromJump(Vector3 dir) => TryPlan(dir, null, true, out var p) && Begin(p);

        /// <summary>Corriendo hacia un obstáculo bajo: lo salta solo. El mantle (1.3 a 1.6 m) requiere salto.</summary>
        public bool TryAutoVault(Vector3 dir) => TryPlan(dir, null, false, out var p) && Begin(p);

        /// <summary>Desde la cobertura (C), Espacio salta el obstáculo hacia el lado contrario.</summary>
        public bool TryStartFromCover()
        {
            if (!_cover.Active || _cover.CoverCollider == null) return false;
            return TryPlan(_cover.FacingDirection, _cover.CoverCollider, true, out var p) && Begin(p);
        }

        // ------------------------------------------------------------------ detección
        private bool TryPlan(Vector3 dir, Collider forced, bool allowMantle, out Plan plan)
        {
            plan = default;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return false;
            dir.Normalize();

            Vector3 feet = transform.position;
            float radius = _cc.radius;
            int mask = Physics.DefaultRaycastLayers; // excluye la capa del jugador (Ignore Raycast)
            Vector3 hitPoint;
            Collider col = forced;

            if (forced == null)
            {
                Vector3 origin = feet + Vector3.up * 0.45f;
                if (!Physics.SphereCast(origin, 0.25f, dir, out RaycastHit hit, _s.VaultReach, mask, QueryTriggerInteraction.Ignore)) return false;
                if (hit.collider.GetComponentInParent<Vaultable>() == null) return false;
                if (Vector3.Dot(hit.normal, -dir) < 0.4f) return false; // de frente, no rozando
                col = hit.collider;
                hitPoint = hit.point;
            }
            else
            {
                hitPoint = forced.ClosestPoint(feet + Vector3.up * 0.45f);
            }

            // Superficie superior: raycast hacia abajo sobre el borde, solo contra ese collider
            float startY = feet.y + _s.MantleMaxHeight + 0.7f;
            Vector3 probe = new Vector3(hitPoint.x, startY, hitPoint.z) + dir * 0.2f;
            if (!col.Raycast(new Ray(probe, Vector3.down), out RaycastHit top, _s.MantleMaxHeight + 0.7f + 0.3f)) { LastVerdict = VaultVerdict.TooHigh; return false; }

            float height = top.point.y - feet.y;
            bool clear = !Physics.CheckCapsule(top.point + Vector3.up * (radius + 0.04f), top.point + Vector3.up * (CrouchedHeight - radius),
                radius * 0.8f, mask, QueryTriggerInteraction.Ignore);
            var verdict = VaultMath.Classify(height, clear, _s);
            LastVerdict = verdict;
            if (verdict != VaultVerdict.Vault && verdict != VaultVerdict.Mantle) return false;
            if (verdict == VaultVerdict.Mantle && !allowMantle) return false;

            // Grosor del obstáculo en la dirección del salto
            float maxDepth = _s.VaultOverMaxDepth + 0.4f;
            Vector3 farStart = new Vector3(hitPoint.x, top.point.y - 0.05f, hitPoint.z) + dir * maxDepth;
            float depth = _s.VaultOverMaxDepth + 1f;
            if (col.Raycast(new Ray(farStart, -dir), out RaycastHit back, maxDepth + 0.2f)) depth = Mathf.Max(0f, Vector3.Dot(back.point - hitPoint, dir));

            Vector3 end;
            if (depth <= _s.VaultOverMaxDepth)
            {
                // pasar al otro lado: buscar suelo y espacio libre donde se aterriza
                Vector3 land = hitPoint + dir * (depth + 0.5f);
                if (Physics.Raycast(land + Vector3.up * (height + 0.6f), Vector3.down, out RaycastHit ground, height + 2.5f, mask, QueryTriggerInteraction.Ignore))
                {
                    Vector3 landFeet = new Vector3(land.x, ground.point.y, land.z);
                    bool landClear = !Physics.CheckCapsule(landFeet + Vector3.up * (radius + 0.05f), landFeet + Vector3.up * (CrouchedHeight - radius),
                        radius * 0.9f, mask, QueryTriggerInteraction.Ignore);
                    if (landClear) { end = landFeet + Vector3.up * 0.02f; goto planned; }
                }
            }
            if (!clear) return false;
            // subirse encima (obstáculo grueso o sin hueco al otro lado)
            end = new Vector3(top.point.x, top.point.y + 0.02f, top.point.z) + dir * Mathf.Min(0.35f, Mathf.Max(0.1f, depth * 0.5f));

        planned:
            plan = new Plan { Verdict = verdict, Start = feet, Top = top.point, End = end, Dir = dir, Duration = VaultMath.Duration(verdict, _s) };
            return true;
        }

        // ------------------------------------------------------------------ ejecución
        private bool Begin(Plan p)
        {
            _plan = p;
            _t = 0f;
            IsVaulting = true;
            _cover.Exit();
            _cc.enabled = false; // la posición la gobierna el recorrido
            _impulse?.Vault();
            return true;
        }

        private void Update()
        {
            if (!IsVaulting) return;
            _t += Time.deltaTime / _plan.Duration;
            V3 p = VaultMath.Position(_plan.Verdict, ToV3(_plan.Start), ToV3(_plan.Top), ToV3(_plan.End), _t);
            transform.position = new Vector3(p.X, p.Y, p.Z);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(_plan.Dir, Vector3.up), 1f - Mathf.Exp(-16f * Time.deltaTime));
            if (_t >= 1f) Finish();
        }

        private void Finish()
        {
            transform.position = _plan.End;
            _cc.enabled = true;
            IsVaulting = false;
            _motor.OnVaultFinished();
        }

        private static V3 ToV3(Vector3 v) => new V3(v.x, v.y, v.z);
    }
}
