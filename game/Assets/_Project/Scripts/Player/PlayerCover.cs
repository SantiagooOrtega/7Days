using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;
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
        public void Exit() { Active = false; Exposure.Reset(); }
        /// <summary>Dirección en la que el jugador mira mientras está en cobertura (hacia el objeto).</summary>
        public Vector3 FacingDirection => -_normal;
        /// <summary>true si el jugador puede ser golpeado con normalidad (fuera de cobertura o asomado).</summary>
        public bool IsExposed => !Active || Exposure.Value > 0.5f;
        /// <summary>Lean (-1 izquierda .. +1 derecha) al asomarse por el lado; la cámara y el cuerpo lo acompañan.</summary>
        public LeanState Lean { get; } = new LeanState();
        /// <summary>Cobertura alta (tapa al jugador de pie): se asoma por el lado; si es baja, por encima.</summary>
        public bool TallCover => Active && _collider != null && CoverTop > _camera.Settings.lowCoverMax;

        /// <summary>Por dónde se asomaría ahora (encima, izquierda, derecha o ninguna: solo disparo a ciegas).</summary>
        public PopDirection PopDir { get; private set; }
        /// <summary>Exposición 0 (cubierto) .. 1 (asomado) con sus tiempos acumulados para el director.</summary>
        public ExposureState Exposure { get; } = new ExposureState();
        /// <summary>Exposición para el resto del juego: 1 fuera de cobertura.</summary>
        public float ExposureValue => Active ? Exposure.Value : 1f;
        public bool IsPoppedOut => Active && Exposure.State == CoverState.PoppedOut;
        public bool IsBlindFiring => Active && Exposure.State == CoverState.BlindFiring;
        /// <summary>Solo se recarga cubierto.</summary>
        public bool CanReload => !Active || Exposure.Value < 0.1f;
        /// <summary>Altura del borde superior de la cobertura sobre los pies del jugador.</summary>
        public float CoverTop => _collider != null ? _collider.bounds.max.y - transform.position.y : 0f;

        private PlayerCamera _camera;
        private CharacterController _cc;
        private Health _health;
        private Collider _collider;
        private Vector3 _normal;
        private float _nextEval;
        private int _lastShoulder = 1;
        private static readonly Collider[] Overlap = new Collider[8];

        public void Initialize(PlayerCamera cam)
        {
            _camera = cam;
            _cc = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
        }

        private void Update()
        {
            var cfg = _camera.Settings;
            if (_health.IsDead) { Active = false; Exposure.Reset(); }

            // por dónde asomarse: se reevalúa unas veces por segundo (y al cambiar de hombro), solo en cobertura
            if (Active && (Time.time >= _nextEval || _camera.Shoulder.Side != _lastShoulder))
            {
                _nextEval = Time.time + 0.1f;
                _lastShoulder = _camera.Shoulder.Side;
                PopDir = EvaluatePop();
            }
            if (!Active) PopDir = PopDirection.None;

            bool wants = Active && _camera.Aiming;
            Exposure.Tick(Time.deltaTime, Active, wants, PopDir != PopDirection.None, cfg.blindExposure, cfg.popSeconds);

            // asomarse por el lado opuesto al hombro: la cámara cambia de hombro para acompañar
            int sign = PopOutRules.Sign(PopDir);
            if (wants && sign != 0 && sign != _camera.Shoulder.Side) _camera.Shoulder.Toggle();

            Lean.Set(sign * Exposure.Value);
            _camera.LeanOffset = Lean.CameraOffset(cfg.popSideMeters);

            if (!Active || _health.IsDead) { if (!_health.IsDead && GameInput.CoverPressed) TryEnter(); return; }
            if (GameInput.CoverPressed) { Active = false; Exposure.Reset(); }
        }

        // ------------------------------------------------------------------ pop-out
        private Vector3 Tangent()
        {
            Vector3 t = Vector3.Cross(Vector3.up, _normal);
            if (Vector3.Dot(t, _camera.Cam.transform.right) < 0f) t = -t;   // derecha en pantalla
            return t;
        }

        private PopDirection EvaluatePop()
        {
            if (_collider == null) return PopDirection.None;
            Vector3 pos = transform.position, tangent = Tangent();
            var cfg = _camera.Settings;
            bool overClear = IsFree(pos + Vector3.up * 1.7f + FacingDirection * 0.15f);
            bool edgeL = IsEdge(pos, -tangent), edgeR = IsEdge(pos, tangent);
            bool leftClear = IsFree(pos + (-tangent) * (cfg.popSideMeters + 0.3f) + Vector3.up * 1.4f);
            bool rightClear = IsFree(pos + tangent * (cfg.popSideMeters + 0.3f) + Vector3.up * 1.4f);
            return PopOutRules.Choose(CoverTop, cfg.lowCoverMax, _camera.Shoulder.Side, overClear, edgeL, edgeR, leftClear, rightClear);
        }

        /// <summary>Hay un borde de la cobertura cerca por ese lado: 0.75 m más allá ya no hay objeto.</summary>
        private bool IsEdge(Vector3 pos, Vector3 dir)
        {
            Vector3 probe = pos + dir * 0.75f;
            probe.y = _collider.bounds.center.y;
            Vector3 cp = _collider.ClosestPoint(probe);
            return Flat(probe - cp).magnitude > 0.45f;
        }

        /// <summary>Espacio libre (sin sólidos, salvo la propia cobertura, el jugador y los hitboxes) en una esfera pequeña.</summary>
        private bool IsFree(Vector3 point)
        {
            int n = Physics.OverlapSphereNonAlloc(point, 0.25f, Overlap, ~((1 << 2) | SevenDays.Combat.HitboxLayer.Mask), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (Overlap[i] != _collider) return false;
            return true;
        }

        /// <summary>Punto desde el que sale el disparo: la cabeza/arma asomada (por el lado o por encima) o, a ciegas, la mano sobre el borde de la cobertura.</summary>
        public Vector3 PopBase(bool blind)
        {
            Vector3 pos = transform.position;
            var cfg = _camera.Settings;
            if (blind)
            {
                if (PopDir == PopDirection.Left || PopDir == PopDirection.Right)   // a ciegas por el lado
                    return pos + Vector3.up * 1.2f + Tangent() * (PopOutRules.Sign(PopDir) * (cfg.popSideMeters + 0.2f)) + FacingDirection * 0.2f;
                return new Vector3(pos.x, _collider.bounds.max.y + 0.12f, pos.z) + FacingDirection * 0.5f;   // por encima: brazo extendido sobre el borde
            }
            Vector3 lateral = Tangent() * (PopOutRules.Sign(PopDir) * cfg.popSideMeters * Exposure.Value);
            return pos + Vector3.up * _camera.PivotHeight + lateral;
        }

        /// <summary>Si el origen queda dentro del collider de la cobertura (su caja) lo saca por el lado libre más cercano. Devuelve true si lo movió.</summary>
        public bool ResolveOrigin(ref Vector3 origin, bool blind)
        {
            if (!Active || _collider == null) return false;
            var b = _collider.bounds;
            PopDirection prefer = blind && (PopDir == PopDirection.None || PopDir == PopDirection.Over) ? PopDirection.Over : PopDir;
            bool moved = ShotOrigin.PushOut(origin.x, origin.y, origin.z, b.min.x, b.min.y, b.min.z, b.max.x, b.max.y, b.max.z, 0.08f, prefer, out float ox, out float oy, out float oz);
            if (moved) origin = new Vector3(ox, oy, oz);
            return moved;
        }

        /// <summary>Un disparo a ciegas mantiene una exposición parcial unos instantes.</summary>
        public void OnBlindShot() => Exposure.OnBlindShot(_camera.Settings.blindHold);

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
            Exposure.Reset();
            _nextEval = 0f;
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
