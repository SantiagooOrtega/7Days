using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.Player
{
    /// <summary>
    /// Curación con barra: H usa el mejor botiquín para la vida que falta y B una venda. La vida se recupera al terminar la barra; si el jugador
    /// recibe daño (o vuelve a pulsar la tecla) se cancela y el botiquín NO se gasta. Con la vida completa avisa y no gasta nada.
    /// </summary>
    public class PlayerHealing : MonoBehaviour
    {
        public HealingSession Session { get; } = new HealingSession();
        public bool Healing => Session.Active;

        /// <summary>Multiplicador de curación de la variante jugable (PlayerVariantStats).</summary>
        public float Multiplier = 1f;
        private Health _health;
        private PlayerInventory _inventory;
        private MedkitKind _usingKind;

        public void Initialize(PlayerInventory inventory)
        {
            _inventory = inventory;
            _health = GetComponent<Health>();
            _health.Damaged += OnDamaged;
        }

        private void OnDestroy() { if (_health != null) _health.Damaged -= OnDamaged; }

        private void OnDamaged(DamageInfo info)
        {
            if (!Session.Active) return;
            Session.Cancel();
            HudMessages.Add("Curación cancelada: recibiste daño", new Color(1f, 0.55f, 0.4f));
        }

        private void Update()
        {
            if (_health.IsDead) { Session.Cancel(); return; }

            if (Session.Active)
            {
                if (GameInput.MedkitPressed || GameInput.BandagePressed) { Session.Cancel(); HudMessages.Add("Curación cancelada"); return; }
                float heal = Session.Tick(Time.deltaTime, _health.Current, _health.Max);
                if (!Session.Active)
                {
                    // terminó: se gasta el botiquín y se cura
                    if (_inventory.Medkits.TryTake(_usingKind))
                    {
                        heal *= Multiplier;   // variante jugable (el médico cura más)
                        _health.Heal(heal);
                        _inventory.NotifyChanged();
                        HudMessages.Add("+" + Mathf.RoundToInt(heal) + " de vida", new Color(0.5f, 1f, 0.5f));
                    }
                }
                return;
            }

            if (GameInput.BandagePressed) Begin(MedkitKind.Bandage);
            else if (GameInput.MedkitPressed)
            {
                var best = _inventory.Medkits.Best(_health.Max - _health.Current);
                if (best == null) HudMessages.Add("No tienes botiquines ni vendas", new Color(1f, 0.6f, 0.4f));
                else Begin(best.Value);
            }
        }

        private void Begin(MedkitKind kind)
        {
            if (_inventory.Medkits.Count(kind) <= 0) { HudMessages.Add("No tienes " + MedkitData.For(kind).Name.ToLowerInvariant(), new Color(1f, 0.6f, 0.4f)); return; }
            if (!HealingSession.CanStart(_health.Current, _health.Max)) { HudMessages.Add("Vida completa: no se gasta el botiquín"); return; }
            _usingKind = kind;
            Session.Start(MedkitData.For(kind), _health.Current, _health.Max);
            HudMessages.Add("Curando con " + MedkitData.For(kind).Name.ToLowerInvariant() + "...", new Color(0.7f, 1f, 0.7f));
        }
    }
}
