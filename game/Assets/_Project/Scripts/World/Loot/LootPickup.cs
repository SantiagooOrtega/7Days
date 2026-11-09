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
    /// Objeto de botín en el suelo (arma, munición o botiquín) con su modelo propio (SupplyModel; el arma, su modelo real con el acento de rareza), un 30 % más grande,
    /// flotando 20-30 cm sobre el suelo y girando despacio, con un contorno que late con el color del tipo, un destello de partículas, una luz suave sin sombras (el
    /// gestor enciende solo las más cercanas), un icono flotante a menos de 15 m (oculto si algo lo tapa de lejos) y, si lo soltó un zombi, una columna de luz breve.
    /// Munición y botiquines se recogen al pasar por encima (si no caben, aviso y NO se consumen) o con E; las armas con E. Vive en un pool.
    /// </summary>
    public class LootPickup : MonoBehaviour, IInteractable, ILootGlint
    {
        public const float VisualScale = 1.3f, Hover = 0.25f;

        public LootItem Item { get; private set; }
        public Rarity Rarity { get; private set; }
        public SupplyKind Kind { get; private set; }
        public string SourcePointId { get; private set; }
        public float NextFullToast;
        /// <summary>El jugador ya oyó el "ping" de este objeto (se avisa una vez al entrar en el radio).</summary>
        public bool Pinged;
        public bool MarkerVisible => _marker != null && _marker.gameObject.activeSelf;
        public float Phase { get; private set; }

        /// <summary>Marcadores visibles y sistemas de partículas emitiendo (panel F6).</summary>
        public static int VisibleMarkers, EmittingSparkles;

        private LootManager _manager;
        private Transform _visual, _marker, _beam;
        private float _groundY, _beamAge = -1f, _nextLos;
        private bool _los = true;
        private Light _light;
        private ParticleSystem _sparkle;
        private Renderer _markerRenderer;

        public Light GlintLight => _light;
        public Vector3 GlintPosition => transform.position + Vector3.up * (Hover + 0.15f);
        public Vector3 Position => transform.position;
        public float Radius => 1.8f;
        public bool CanInteract => gameObject.activeInHierarchy && Item != null;
        public float HoldSeconds => 0f;
        public string Prompt => Item == null ? "" : "E: recoger " + ItemName(Item) + (Item.kind == "weapon" ? " (" + WeaponCatalog.RarityName(Rarity) + ")" : "");

        public static LootPickup Create(Transform parent)
        {
            var go = new GameObject("Loot Pickup");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<LootPickup>();
            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = Vector3.up * 0.55f;
            p._light = lightGo.AddComponent<Light>();
            p._light.type = LightType.Point;
            p._light.range = 3.2f;
            p._light.intensity = 0.9f;
            p._light.shadows = LightShadows.None; // brillo suave sin sombras
            p._light.enabled = false;
            p._sparkle = Sparkles(go.transform);
            // marcador flotante (billboard) y columna de luz: se crean una vez y se reutilizan con el pool
            var mk = new GameObject("Marcador");
            mk.transform.SetParent(go.transform, false);
            mk.AddComponent<MeshFilter>().sharedMesh = SupplyArt.Quad;
            p._markerRenderer = mk.AddComponent<MeshRenderer>();
            p._markerRenderer.shadowCastingMode = ShadowCastingMode.Off; p._markerRenderer.receiveShadows = false;
            p._marker = mk.transform; mk.SetActive(false);
            var bm = new GameObject("Columna de luz");
            bm.transform.SetParent(go.transform, false);
            bm.AddComponent<MeshFilter>().sharedMesh = SupplyArt.Quad;
            var br = bm.AddComponent<MeshRenderer>(); br.sharedMaterial = SupplyArt.Beam; br.shadowCastingMode = ShadowCastingMode.Off; br.receiveShadows = false;
            p._beam = bm.transform; bm.SetActive(false);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            go.SetActive(false);
            return p;
        }

        public void Setup(LootItem item, Vector3 position, string pointId, LootManager manager)
        {
            Item = item;
            Rarity = LootRules.ItemRarity(item);
            Kind = SupplyModel.KindOf(item);
            SourcePointId = pointId;
            _manager = manager;
            NextFullToast = 0f; Pinged = false; Phase = Random.value;
            // al suelo real (las mesas, escombros o la acera cambian la altura); luego flota Hover metros por encima
            _groundY = position.y - 0.35f;
            int solid = ~((1 << 2) | SevenDays.Combat.HitboxLayer.Mask);
            if (Physics.Raycast(position + Vector3.up * 1.2f, Vector3.down, out RaycastHit hit, 3f, solid, QueryTriggerInteraction.Ignore)) _groundY = hit.point.y;
            transform.position = new Vector3(position.x, _groundY, position.z);
            BuildVisual();
            var c = Kind == SupplyKind.Weapon ? WeaponCatalog.RarityColor(Rarity) : SupplyModel.TypeColor(Kind);
            _light.color = new Color(c[0], c[1], c[2]);
            var main = _sparkle.main; main.startColor = new Color(c[0], c[1], c[2], 0.9f);
            _markerRenderer.sharedMaterial = SupplyArt.IconMaterial(Kind, Rarity);
            _marker.gameObject.SetActive(false); _los = true; _nextLos = 0f;
            _beamAge = pointId == null ? 0f : -1f;   // drop de zombi o del jefe: columna de luz breve para localizarlo
            _beam.gameObject.SetActive(_beamAge >= 0f);
            gameObject.SetActive(true);
            PlayerInteract.Registry.Add(this);
        }

        private void OnDestroy() => PlayerInteract.Registry.Remove(this);

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

        /// <summary>El gestor enciende o apaga el destello de partículas según la distancia (lejos no se ve y cuesta).</summary>
        public void SetSparkles(bool on)
        {
            var em = _sparkle.emission;
            em.enabled = on;
        }

        private void Update()
        {
            if (_visual == null) return;
            SupplyArt.Tick();
            // reposo: flota algo por encima del suelo, sube y baja un poco y gira despacio para llamar la atención
            _visual.localPosition = Vector3.up * (Hover + Mathf.Sin(Time.time * 2f + Phase * 6.28f) * 0.035f);
            _visual.Rotate(0f, 45f * Time.deltaTime, 0f, Space.Self);
            if (_beamAge >= 0f)
            {
                _beamAge += Time.deltaTime;
                float k = SupplyVisibility.Beam(_beamAge);
                if (k <= 0f) { _beamAge = -1f; _beam.gameObject.SetActive(false); }
                else _beam.localScale = new Vector3(0.26f * k, 5f, 1f);
            }
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || _visual == null) return;
            Vector3 center = transform.position + Vector3.up * (Hover + 0.15f);
            float dist = Vector3.Distance(cam.transform.position, center);
            // línea de visión desde la cámara (escalonada: unas 3 veces por segundo por objeto)
            if (dist <= SupplyVisibility.MarkerRange && Time.time >= _nextLos)
            {
                _nextLos = Time.time + 0.3f + Phase * 0.1f;
                _los = ClearLine(cam.transform.position, center);
            }
            bool show = SupplyVisibility.ShowMarker(dist, _los);
            if (show != _marker.gameObject.activeSelf) _marker.gameObject.SetActive(show);
            if (show)
            {
                VisibleMarkers++;
                float size = SupplyVisibility.MarkerSize(dist);
                _marker.position = transform.position + Vector3.up * (Hover + 0.55f + 0.04f * Mathf.Sin(Time.time * 2.4f + Phase * 6.28f));
                _marker.rotation = Quaternion.LookRotation(_marker.position - cam.transform.position, cam.transform.up);
                _marker.localScale = Vector3.one * size;
            }
            if (_beam.gameObject.activeSelf)
            {
                // la columna gira sobre su eje para mirar siempre a la cámara
                Vector3 to = cam.transform.position - transform.position; to.y = 0f;
                if (to.sqrMagnitude > 0.01f) _beam.rotation = Quaternion.LookRotation(-to.normalized, Vector3.up);
                _beam.position = transform.position + Vector3.up * 2.5f;
            }
        }

        private static readonly RaycastHit[] LosHits = new RaycastHit[8];

        /// <summary>Línea de visión de la cámara al objeto sin contar al propio jugador (en tercera persona suele estar delante).</summary>
        private static bool ClearLine(Vector3 from, Vector3 to)
        {
            int solid = ~((1 << 2) | SevenDays.Combat.HitboxLayer.Mask);
            Vector3 d = to - from; float len = d.magnitude;
            if (len < 0.01f) return true;
            int n = Physics.RaycastNonAlloc(from, d / len, LosHits, len, solid, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!LosHits[i].collider.transform.root.CompareTag("Player")) return false;
            return true;
        }

        // ---------------------------------------------------------------- aspecto
        private void BuildVisual()
        {
            if (_visual != null) Destroy(_visual.gameObject);
            var root = new GameObject("Loot Visual").transform;
            root.SetParent(transform, false);
            root.localPosition = Vector3.up * Hover;
            root.localScale = Vector3.one * VisualScale;
            _visual = root;
            if (Kind == SupplyKind.Weapon)
            {
                // el arma con su modelo real (las mismas piezas que en la mano) y su acento de rareza, algo inclinada para que se lea su silueta
                var d = WeaponLibrary.Get(Item.id);
                if (d != null)
                {
                    var gun = GunParts.Build(d, root);
                    gun.transform.localPosition = new Vector3(0f, 0.08f, -d.length * 0.4f);
                    gun.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
                    foreach (var r in gun.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                    if (gun.Trail != null) gun.Trail.enabled = false;
                }
            }
            else
            {
                var go = new GameObject("Modelo");
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = SupplyArt.MeshFor(Kind);
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = SupplyArt.MaterialsFor(Kind);
                r.shadowCastingMode = ShadowCastingMode.On;
                var ol = new GameObject("Contorno");
                ol.transform.SetParent(root, false);
                ol.AddComponent<MeshFilter>().sharedMesh = SupplyArt.OutlineFor(Kind);
                var orr = ol.AddComponent<MeshRenderer>();
                orr.sharedMaterial = SupplyArt.OutlineMaterial(Kind);
                orr.shadowCastingMode = ShadowCastingMode.Off; orr.receiveShadows = false;
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
        }

        /// <summary>Destello suave: pocas chispas que suben despacio (las enciende el gestor solo cerca).</summary>
        private static ParticleSystem Sparkles(Transform parent)
        {
            var go = new GameObject("Destello");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * 0.3f;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.maxParticles = 12; main.gravityModifier = -0.05f;
            var em = ps.emission; em.rateOverTime = 5f; em.enabled = false;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.18f;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = SevenDays.Look.ParticleMaterials.SoftAdditive; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            go.SetActive(true);
            return ps;
        }

        public static string ItemName(LootItem i)
        {
            if (i.kind == "weapon") return WeaponLibrary.Get(i.id)?.displayName ?? i.id;
            if (i.kind == "ammo") return "munición de " + PlayerInventory.AmmoName(i.AmmoTypeValue);
            return MedkitData.For(i.MedkitKindValue).Name.ToLowerInvariant();
        }
    }
}
