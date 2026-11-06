using UnityEngine;
using SevenDays.Core;
using SevenDays.World;

namespace SevenDays.Player
{
    /// <summary>
    /// Sistema de cobertura simple. C junto a un objeto con CoverObject: el jugador se pega a él,
    /// se agacha y se desliza lateralmente con A/D. Al apuntar (clic derecho) se asoma para disparar.
    /// C otra vez para salir.
    /// </summary>
    public class PlayerCover : MonoBehaviour
    {
        private const float SearchRadius = 2.2f;
        private const float StandOff = 0.6f;

        public bool Active { get; private set; }
        /// <summary>Collider del objeto de cobertura actual (null si no hay).</summary>
        public Collider CoverCollider => Active ? _collider : null;
        /// <summary>Sale de la cobertura (por ejemplo al saltar el obstáculo).</summary>
        public void Exit() => Active = false;
        /// <summary>Dirección en la que el jugador mira mientras está en cobertura (hacia el objeto).</summary>
        public Vector3 FacingDirection => -_normal;
        /// <summary>true si el jugador puede ser golpeado con normalidad (fuera de cobertura o asomado).</summary>
        public bool IsExposed => !Active || _camera.Aiming;

        private PlayerCamera _camera;
        private CharacterController _cc;
        private Health _health;
        private Collider _collider;
        private Vector3 _normal;

        public void Initialize(PlayerCamera cam)
        {
            _camera = cam;
            _cc = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
        }

        private void Update()
        {
            if (_health.IsDead) { Active = false; return; }
            if (!GameInput.CoverPressed) return;
            if (Active) Active = false;
            else TryEnter();
        }

        private void TryEnter()
        {
            Vector3 pos = transform.position;
            CoverObject best = null;
            Collider bestCollider = null;
            Vector3 bestPoint = Vector3.zero;
            float bestDist = float.MaxValue;

            foreach (var c in Physics.OverlapSphere(pos, SearchRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                var cover = c.GetComponentInParent<CoverObject>();
                if (cover == null) continue;
                Vector3 p = c.ClosestPoint(pos);
                float d = Flat(p - pos).magnitude;
                if (d < bestDist) { bestDist = d; best = cover; bestCollider = c; bestPoint = p; }
            }
            if (best == null) return;

            Vector3 n = Flat(pos - bestPoint);
            _normal = n.sqrMagnitude > 1e-4f ? n.normalized : -transform.forward;
            _collider = bestCollider;

            // Colocar al jugador pegado a la cobertura (el CharacterController hay que apagarlo para teletransportar).
            _cc.enabled = false;
            Vector3 target = bestPoint + _normal * StandOff;
            transform.position = new Vector3(target.x, pos.y, target.z);
            _cc.enabled = true;
            Active = true;
        }

        /// <summary>Velocidad lateral a lo largo de la cobertura, sin salirse de sus extremos.</summary>
        public Vector3 CoverVelocity(Vector2 input, float speed)
        {
            Vector3 pos = transform.position;
            Vector3 tangent = Vector3.Cross(Vector3.up, _normal);
            if (Vector3.Dot(tangent, _camera.Cam.transform.right) < 0f) tangent = -tangent; // D = derecha en pantalla

            Vector3 slide = tangent * (input.x * speed);
            if (Mathf.Abs(input.x) > 0.01f)
            {
                // Si un poco más adelante ya no hay "cara" de la cobertura, es el borde: no avanzar.
                Vector3 probe = pos + tangent * (Mathf.Sign(input.x) * 0.45f);
                Vector3 pn = Flat(probe - _collider.ClosestPoint(probe));
                if (pn.sqrMagnitude < 1e-4f || Vector3.Dot(pn.normalized, _normal) < 0.85f) slide = Vector3.zero;
            }

            Vector3 desired = _collider.ClosestPoint(pos) + _normal * StandOff;
            Vector3 correction = Flat(desired - pos) * 8f; // mantiene la distancia a la pared
            return slide + correction;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
