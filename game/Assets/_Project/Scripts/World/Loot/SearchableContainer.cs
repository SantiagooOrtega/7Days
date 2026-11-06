using UnityEngine;
using SevenDays.Core;
using SevenDays.Player;
using SevenDays.Procedural;

namespace SevenDays.World.Loot
{
    /// <summary>
    /// Contenedor buscable (caja, mueble de la casa, maletero de un coche, montón de escombros, escondite entre arbustos): hay que MANTENER E entre 1 y 2 s.
    /// Al terminar tira en la tabla del punto y deja el botín en el suelo (con los topes del director). Una luz tenue lo delata hasta que se busca.
    /// </summary>
    public class SearchableContainer : MonoBehaviour, IInteractable, ILootGlint
    {
        public LootPoint Point { get; private set; }
        public bool Searched { get; private set; }

        private LootManager _manager;
        private Light _glint;
        private float _radius = 2.1f;

        public Light GlintLight => _glint;
        public Vector3 GlintPosition => transform.position + Vector3.up * 0.8f;
        public Vector3 Position => transform.position;
        public float Radius => _radius;
        public bool CanInteract => !Searched && isActiveAndEnabled;
        public float HoldSeconds => Point.searchSeconds;
        public string Prompt => "Mantén E para buscar (" + Point.searchSeconds.ToString("0.#") + " s)";

        public static SearchableContainer Attach(GameObject target, LootPoint point, LootManager manager, float radius = 2.1f)
        {
            var c = target.AddComponent<SearchableContainer>();
            c.Point = point;
            c._manager = manager;
            c._radius = radius;
            var g = new GameObject("Glint");
            g.transform.SetParent(target.transform, false);
            g.transform.localPosition = Vector3.up * 0.8f;
            c._glint = g.AddComponent<Light>();
            c._glint.type = LightType.Point;
            c._glint.color = new Color(1f, 0.92f, 0.65f);
            c._glint.range = 2.4f;
            c._glint.intensity = 0.55f;
            c._glint.shadows = LightShadows.None;
            PlayerInteract.Registry.Add(c);
            manager.RegisterContainer(c);
            return c;
        }

        private void OnDestroy() => PlayerInteract.Registry.Remove(this);

        public void Interact(PlayerInventory inventory)
        {
            if (Searched) return;
            if (!_manager.SearchResult(this))
            {
                HudMessages.Add("No encuentras nada útil por ahora", new Color(0.8f, 0.8f, 0.8f)); // el suelo está lleno: se puede reintentar
                return;
            }
            Searched = true;
            if (_glint != null) _glint.enabled = false;
        }

        /// <summary>El director lo rellena (respawn lento entre días).</summary>
        public void Refill()
        {
            Searched = false;
            if (_glint != null) _glint.enabled = true;
        }
    }
}
