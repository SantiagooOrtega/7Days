using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
using SevenDays.Procedural;

namespace SevenDays.World
{
    /// <summary>
    /// Control de la casa en tiempo de juego. El director (Fase 4) bloquea o abre las entradas según el día con SetDay o SetBlocked: se activan
    /// los tablones (collider incluido) y se apaga el enlace de NavMesh de la ventana. También expone dónde están las antorchas.
    /// </summary>
    public class HouseController : MonoBehaviour
    {
        public readonly List<Vector3> TorchPoints = new List<Vector3>();
        private readonly Dictionary<string, GameObject> _barricades = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, NavMeshLink> _links = new Dictionary<string, NavMeshLink>();
        private readonly HashSet<string> _blocked = new HashSet<string>();
        private HouseLayoutData _layout;

        /// <summary>Plantas de las casas del mapa: los zombis que no pueden entrar a edificios las usan como barrera de seguridad.</summary>
        public static readonly List<Footprint> Footprints = new List<Footprint>();
        public Footprint Footprint { get; private set; }
        private bool _registered;

        public int Day { get; private set; } = 1;
        public IReadOnlyCollection<string> BlockedOpenings => _blocked;

        public void Initialize(HouseLayoutData layout, HousePlan plan)
        {
            _layout = layout;
            Footprint = Footprint.FromHouse(layout.x, layout.z, layout.width, layout.depth, layout.yaw, layout.wallThickness * 0.5f);
            Footprints.Add(Footprint);
            _registered = true;
        }

        private void OnDestroy() { if (_registered) Footprints.Remove(Footprint); }

        /// <summary>Si el punto está dentro de alguna casa (con margen), devuelve su planta.</summary>
        public static bool Inside(Vector3 p, float margin, out Footprint f)
        {
            foreach (var fp in Footprints) if (fp.Contains(p.x, p.z, margin)) { f = fp; return true; }
            f = default;
            return false;
        }
        public void RegisterBarricade(string openingId, GameObject go) { _barricades[openingId] = go; go.SetActive(false); }
        public void RegisterLink(string openingId, NavMeshLink link) => _links[openingId] = link;

        /// <summary>Aplica el bloqueo del día según blockedDays de cada abertura.</summary>
        public void SetDay(int day)
        {
            Day = day;
            if (_layout == null) return;
            foreach (var o in _layout.openings) SetBlocked(o.id, HouseTypes.IsBlocked(o, day));
        }

        /// <summary>Tapa (true) o abre (false) una abertura por su id. No hace nada si el id no existe.</summary>
        public void SetBlocked(string openingId, bool blocked)
        {
            if (!_barricades.TryGetValue(openingId, out var planks)) return;
            planks.SetActive(blocked);
            if (blocked) _blocked.Add(openingId); else _blocked.Remove(openingId);
            if (_links.TryGetValue(openingId, out var link) && link != null) link.enabled = !blocked;
        }

        public bool IsBlocked(string openingId) => _blocked.Contains(openingId);
    }
}
