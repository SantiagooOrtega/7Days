using System.Collections.Generic;
using UnityEngine;
using SevenDays.Core;

namespace SevenDays.Player
{
    /// <summary>Algo con lo que el jugador puede interactuar con la tecla E: recoger un arma o buscar en un contenedor (manteniendo la tecla).</summary>
    public interface IInteractable
    {
        Vector3 Position { get; }
        float Radius { get; }
        string Prompt { get; }
        /// <summary>0 = al pulsar E; mayor que 0 = hay que mantener E ese tiempo (búsqueda).</summary>
        float HoldSeconds { get; }
        bool CanInteract { get; }
        void Interact(PlayerInventory inventory);
    }

    /// <summary>Busca la interacción más cercana y gestiona el pulsar o mantener E, con su barra de progreso. Se cancela al recibir daño o alejarse.</summary>
    public class PlayerInteract : MonoBehaviour
    {
        public static readonly List<IInteractable> Registry = new List<IInteractable>();

        public IInteractable Target { get; private set; }
        /// <summary>0..1 mientras se mantiene E sobre algo que requiere tiempo.</summary>
        public float HoldProgress { get; private set; }
        public bool Holding { get; private set; }

        private PlayerInventory _inventory;
        private Health _health;
        private IInteractable _holdTarget;
        private float _held;

        public void Initialize(PlayerInventory inventory)
        {
            _inventory = inventory;
            _health = GetComponent<Health>();
            _health.Damaged += OnDamaged;
        }

        private void OnDestroy() { if (_health != null) _health.Damaged -= OnDamaged; }

        private void OnDamaged(DamageInfo info) { if (Holding) { CancelHold(); HudMessages.Add("Búsqueda interrumpida", new Color(1f, 0.55f, 0.4f)); } }

        private void Update()
        {
            if (_health.IsDead) { CancelHold(); Target = null; return; }
            Target = FindNearest();

            if (Target == null) { CancelHold(); return; }

            if (Target.HoldSeconds <= 0f)
            {
                CancelHold();
                if (GameInput.InteractPressed) Target.Interact(_inventory);
                return;
            }

            // interacción con tiempo: hay que MANTENER E sobre el mismo objetivo
            if (GameInput.InteractHeld)
            {
                if (_holdTarget != Target) { _holdTarget = Target; _held = 0f; }
                Holding = true;
                _held += Time.deltaTime;
                HoldProgress = Mathf.Clamp01(_held / Target.HoldSeconds);
                if (_held >= Target.HoldSeconds)
                {
                    var t = Target;
                    CancelHold();
                    t.Interact(_inventory);
                }
            }
            else CancelHold();
        }

        private void CancelHold()
        {
            Holding = false; HoldProgress = 0f; _held = 0f; _holdTarget = null;
        }

        private IInteractable FindNearest()
        {
            IInteractable best = null;
            float bestD = float.MaxValue;
            Vector3 p = transform.position;
            for (int i = Registry.Count - 1; i >= 0; i--)
            {
                var it = Registry[i];
                if (it == null || !it.CanInteract) continue;
                float d = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(it.Position.x, 0f, it.Position.z));
                if (d <= it.Radius && d < bestD) { bestD = d; best = it; }
            }
            return best;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Registry.Clear();
    }
}
