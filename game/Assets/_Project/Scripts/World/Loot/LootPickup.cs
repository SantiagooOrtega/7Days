using UnityEngine;
using UnityEngine.Rendering;
using SevenDays.Core;
using SevenDays.Player;
using SevenDays.Procedural;
using SevenDays.Weapons;

namespace SevenDays.World.Loot
{
    /// <summary>Algo que emite una luz suave para que se encuentre de lejos (el gestor mantiene encendidas solo las más cercanas).</summary>
    public interface ILootGlint
    {
        Light GlintLight { get; }
        Vector3 GlintPosition { get; }
    }

    /// <summary>
    /// Objeto de botín en el suelo (arma, munición o botiquín), con brillo del color de su rareza y una luz suave sin sombras. Munición y botiquines se
    /// recogen al acercarse (si no caben, aparece el aviso y NO se consumen); las armas con la tecla E. Vive en un pool.
    /// </summary>
    public class LootPickup : MonoBehaviour, IInteractable, ILootGlint
    {
        public LootItem Item { get; private set; }
        public Rarity Rarity { get; private set; }
        public string SourcePointId { get; private set; }
        public float NextFullToast;

        private LootManager _manager;
        private Transform _visual;
        private float _baseY;
        private Light _light;

        public Light GlintLight => _light;
        public Vector3 GlintPosition => transform.position;
        public Vector3 Position => transform.position;
        public float Radius => 1.8f;
        public bool CanInteract => gameObject.activeInHierarchy && Item != null && Item.kind == "weapon";
        public float HoldSeconds => 0f;
        public string Prompt => Item == null ? "" : "E: recoger " + ItemName(Item) + " (" + WeaponCatalog.RarityName(Rarity) + ")";

        public static LootPickup Create(Transform parent)
        {
            var go = new GameObject("Loot Pickup");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<LootPickup>();
            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = Vector3.up * 0.5f;
            p._light = lightGo.AddComponent<Light>();
            p._light.type = LightType.Point;
            p._light.range = 3.2f;
            p._light.intensity = 0.9f;
            p._light.shadows = LightShadows.None; // brillo suave sin sombras
            go.SetActive(false);
            return p;
        }

        public void Setup(LootItem item, Vector3 position, string pointId, LootManager manager)
        {
            Item = item;
            Rarity = LootRules.ItemRarity(item);
            SourcePointId = pointId;
            _manager = manager;
            NextFullToast = 0f;
            transform.position = position;
            _baseY = position.y;
            BuildVisual();
            var rc = WeaponCatalog.RarityColor(Rarity);
            _light.color = new Color(rc[0], rc[1], rc[2]);
            gameObject.SetActive(true);
            PlayerInteract.Registry.Add(this);
        }

        public void Release()
        {
            PlayerInteract.Registry.Remove(this);
            gameObject.SetActive(false);
        }

        public void Interact(PlayerInventory inventory)
        {
            var result = inventory.TryPickup(Item, out string message);
            var rc = WeaponCatalog.RarityColor(Rarity);
            HudMessages.Add(result == PickupResult.Picked ? "Recogiste: " + message : message, result == PickupResult.Picked ? new Color(rc[0], rc[1], rc[2]) : new Color(1f, 0.7f, 0.4f));
            if (result == PickupResult.Picked) _manager.Collected(this);
        }

        private void Update()
        {
            if (_visual == null) return;
            // reposo suave: sube y baja un poco y gira despacio para llamar la atención
            var p = transform.position;
            p.y = _baseY + Mathf.Sin(Time.time * 2f + _baseY * 3f) * 0.04f;
            transform.position = p;
            _visual.Rotate(0f, 50f * Time.deltaTime, 0f, Space.Self);
        }

        // ---------------------------------------------------------------- aspecto
        private void BuildVisual()
        {
            if (_visual != null) Destroy(_visual.gameObject);
            var root = new GameObject("Loot Visual").transform;
            root.SetParent(transform, false);
            root.localPosition = Vector3.up * 0.28f;
            _visual = root;
            var rc = WeaponCatalog.RarityColor(Rarity);
            var glow = Prim.GetEmissive(new Color(rc[0], rc[1], rc[2]), 1.5f);

            switch (Item.kind)
            {
                case "weapon":
                {
                    var d = WeaponLibrary.Get(Item.id);
                    var c = d != null ? d.color : new[] { 0.2f, 0.2f, 0.2f };
                    float len = d != null ? Mathf.Min(d.length, 0.9f) : 0.6f;
                    Part(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.1f, 0.12f, len), Prim.GetMaterial(new Color(c[0] + 0.1f, c[1] + 0.1f, c[2] + 0.1f)));
                    Part(PrimitiveType.Cube, root, new Vector3(0f, -0.09f, -len * 0.3f), new Vector3(0.06f, 0.12f, 0.1f), glow);
                    break;
                }
                case "ammo":
                {
                    Color box = Item.AmmoTypeValue == AmmoType.Pistol ? new Color(0.78f, 0.62f, 0.2f) : (Item.AmmoTypeValue == AmmoType.Shotgun ? new Color(0.75f, 0.25f, 0.2f) : new Color(0.3f, 0.55f, 0.3f));
                    Part(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.22f, 0.12f, 0.15f), Prim.GetMaterial(box));
                    Part(PrimitiveType.Cube, root, new Vector3(0f, 0.065f, 0f), new Vector3(0.24f, 0.02f, 0.05f), glow);
                    break;
                }
                default:
                {
                    float k = Item.MedkitKindValue == MedkitKind.Large ? 1.35f : (Item.MedkitKindValue == MedkitKind.Medium ? 1.15f : (Item.MedkitKindValue == MedkitKind.Small ? 1f : 0.7f));
                    var white = Prim.GetMaterial(new Color(0.85f, 0.85f, 0.82f));
                    var red = Prim.GetEmissive(new Color(0.9f, 0.1f, 0.1f), 1.2f);
                    if (Item.MedkitKindValue == MedkitKind.Bandage)
                    {
                        Part(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.16f, 0.05f, 0.16f), white);
                        Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.03f, 0f), new Vector3(0.17f, 0.012f, 0.17f), red);
                    }
                    else
                    {
                        Part(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.3f * k, 0.18f * k, 0.2f * k), white);
                        Part(PrimitiveType.Cube, root, new Vector3(0f, 0.095f * k, 0f), new Vector3(0.14f * k, 0.01f, 0.04f * k), red);
                        Part(PrimitiveType.Cube, root, new Vector3(0f, 0.095f * k, 0f), new Vector3(0.04f * k, 0.01f, 0.14f * k), red);
                    }
                    break;
                }
            }
            // aro brillante en el suelo (no gira): ayuda a verlo de lejos
            var ring = Prim.Create(PrimitiveType.Cylinder, "Loot Ring", Vector3.up * 0.02f, new Vector3(0.7f, 0.004f, 0.7f), glow, transform, false);
            ring.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m)
        {
            var go = Prim.Create(type, "Loot Part", pos, scale, m, parent, false);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static string ItemName(LootItem i)
        {
            if (i.kind == "weapon") return WeaponLibrary.Get(i.id)?.displayName ?? i.id;
            if (i.kind == "ammo") return "munición de " + PlayerInventory.AmmoName(i.AmmoTypeValue);
            return MedkitData.For(i.MedkitKindValue).Name.ToLowerInvariant();
        }
    }
}
