using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.Combat
{
    /// <summary>
    /// Capa "Hitbox": los colliders de impacto de los zombies (cabeza, torso, brazos, piernas) viven en esta capa y no chocan con nada del movimiento,
    /// solo los reciben los rayos de las armas. El nombre de la capa lo crea un script de editor (HitboxLayerSetup); mientras no exista se usa la 31.
    /// </summary>
    public static class HitboxLayer
    {
        public const string Name = "Hitbox";
        private static int _index = -1;
        private static bool _configured;

        public static int Index
        {
            get
            {
                if (_index < 0) { int i = LayerMask.NameToLayer(Name); _index = i >= 0 ? i : 31; }
                return _index;
            }
        }

        public static int Mask => 1 << Index;

        /// <summary>Hace que la capa Hitbox no colisione con ninguna otra (nada se queda atascado en un hitbox).</summary>
        public static void ConfigureCollisions()
        {
            if (_configured) return;
            _configured = true;
            for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(Index, i, true);
        }
    }

    /// <summary>Zona de impacto: reenvía el daño a la Health del zombie indicando la zona; el multiplicador de la zona lo aplica Health.</summary>
    public class Hitbox : MonoBehaviour, IDamageable
    {
        public HitZone Zone;
        public Health Owner;

        public bool IsDead => Owner == null || Owner.IsDead;

        public bool TakeDamage(DamageInfo info)
        {
            if (Owner == null) return false;
            info.UseZone = true;
            info.Zone = Zone;
            return Owner.TakeDamage(info);
        }
    }
}
