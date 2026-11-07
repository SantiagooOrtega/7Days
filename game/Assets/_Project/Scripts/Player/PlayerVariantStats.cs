using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.Player
{
    /// <summary>
    /// Aplica las estadísticas de la variante jugable (ver docs/BALANCE.md): vida máxima, velocidad, curación y balanceo del arma.
    /// La Calavera es la referencia (todo 1, 100 de vida).
    /// </summary>
    public static class PlayerVariantStats
    {
        public static void Apply(GameObject player, CharacterVariantData v)
        {
            if (player == null || v == null) return;
            var health = player.GetComponent<Health>();
            if (health != null) health.SetMax(v.maxHealth, true);   // al cambiar de variante (F6) la vida se rellena al nuevo máximo
            var motor = player.GetComponent<PlayerMotor>(); if (motor != null) motor.SpeedMultiplier = v.moveSpeed;
            var healing = player.GetComponent<PlayerHealing>(); if (healing != null) healing.Multiplier = v.healing;
            var handling = player.GetComponent<WeaponHandling>(); if (handling != null) handling.SwayMultiplier = v.weaponSway;
        }
    }
}
