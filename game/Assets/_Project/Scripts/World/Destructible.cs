using System.Collections.Generic;
using UnityEngine;

namespace SevenDays.World
{
    /// <summary>
    /// Cobertura destruible por el ataque de un jefe: se rompe (se oculta y deja de bloquear) y reaparece entre oleadas con Restore().
    /// strength 0 = ligera (cajas, muebles: la rompe la carga del bruto y el lanzamiento), 1 = pesada (barreras: solo la rompe un lanzamiento).
    /// </summary>
    public class Destructible : MonoBehaviour
    {
        public static readonly List<Destructible> All = new List<Destructible>();
        public int Strength;
        public bool Broken { get; private set; }

        private Collider[] _colliders;
        private Renderer[] _renderers;

        private void Cache() { _colliders ??= GetComponentsInChildren<Collider>(true); _renderers ??= GetComponentsInChildren<Renderer>(true); }
        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        /// <param name="power">0 = carga/golpe, 1 = lanzamiento grande</param>
        public bool TrySmash(int power, Vector3 point, Vector3 direction)
        {
            if (Broken || power < Strength) return false;
            Smash(point, direction);
            return true;
        }

        public void Smash(Vector3 point, Vector3 direction)
        {
            if (Broken) return;
            Cache();
            Broken = true;
            foreach (var c in _colliders) c.enabled = false;
            foreach (var r in _renderers) r.enabled = false;
            if (SevenDays.Combat.BloodFx.Instance != null)
                SevenDays.Combat.BloodFx.Instance.Spray(point + Vector3.up * 0.4f, Vector3.up + direction * 0.4f, 16, new Color(0.45f, 0.38f, 0.28f), 1.2f);
            SevenDays.Combat.ZombieManager.Instance?.RequestNavMeshRefresh();
        }

        /// <summary>Entre oleadas la cobertura vuelve a estar en su sitio.</summary>
        public void Restore()
        {
            if (!Broken) return;
            Cache();
            Broken = false;
            foreach (var c in _colliders) c.enabled = true;
            foreach (var r in _renderers) r.enabled = true;
        }

        public static void RestoreAll() { foreach (var d in All.ToArray()) d.Restore(); }
    }
}
