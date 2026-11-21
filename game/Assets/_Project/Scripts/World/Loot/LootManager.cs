using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;
using SevenDays.Core;
using SevenDays.Player;
using SevenDays.Procedural;
using SevenDays.Weapons;

namespace SevenDays.World.Loot
{
    /// <summary>
    /// Gestor del botín: pool de pickups, topes de escasez (pickups activos y drops por día), recogida automática de munición y botiquines al acercarse
    /// (con aviso "lleno" sin consumirlos), luces suaves solo en los más cercanos, drops de zombies y respawn lento entre días (lo controla el director).
    /// </summary>
    public class LootManager : MonoBehaviour
    {
        public static LootManager Instance { get; private set; }

        public LootLayoutData Layout { get; private set; }
        public LootDirectorState State { get; private set; }
        public int ActiveCount => _active.Count;
        public int MaxGlintLights = 8;
        /// <summary>Luces de botín encendidas ahora (panel F6).</summary>
        public int LightsOn { get; private set; }
        /// <summary>Distancia a la que un objeto suelta su destello de partículas.</summary>
        public const float SparkleRange = 25f;

        private ObjectPool<LootPickup> _pool;
        private readonly List<LootPickup> _active = new List<LootPickup>();
        private readonly Dictionary<string, SearchableContainer> _containers = new Dictionary<string, SearchableContainer>();
        private readonly List<ILootGlint> _glints = new List<ILootGlint>();
        private System.Random _rng;
        private Transform _player;
        private PlayerInventory _inventory;
        private float _pickTimer, _glintTimer, _boundsTimer;

        // ---------------------------------------------------------------- regla dura de colocación
        private float[][] _perimeter;
        private Vector3 _start;
        private bool _navReady;
        private NavMeshPath _path;
        private readonly Dictionary<long, bool> _reach = new Dictionary<long, bool>();
        private readonly List<LootPoint> _validPoints = new List<LootPoint>();
        public float HalfSize { get; private set; }
        /// <summary>Pickups movidos a un sitio válido y descartados por no tener ninguno (se avisa en la Console).</summary>
        public int Relocated { get; private set; }
        public int Discarded { get; private set; }
        public bool NavReady => _navReady;

        public static LootManager Create(Transform root, LootLayoutData layout, int seed)
        {
            var go = new GameObject("Loot Manager");
            go.transform.SetParent(root, false);
            var m = go.AddComponent<LootManager>();
            m.Layout = layout;
            m.State = new LootDirectorState(layout);
            m._rng = new System.Random(seed);
            m._pool = new ObjectPool<LootPickup>(() => LootPickup.Create(go.transform), null, p => p.Release(), p => { if (p != null) Destroy(p.gameObject); }, false, 16, 64);
            SceneStatics.ResetAll();   // nada de la partida anterior debe sobrevivir
            Instance = m;
            return m;
        }

        /// <summary>Perímetro del mapa (cuadrado ±halfSize) e inicio del jugador (de ahí se mide si algo es alcanzable).</summary>
        public void SetBounds(float halfSize, Vector3 playerStart)
        {
            HalfSize = halfSize;
            _perimeter = LootPlacement.Square(halfSize);
            _start = playerStart;
            int bad = 0;
            foreach (var id in LootPlacement.Outside(Layout, halfSize)) { bad++; Debug.LogWarning("[7Days] Punto de botín " + id + " fuera del perímetro: no se usa."); }
            _validPoints.Clear();
            foreach (var p in Layout.points) if (LootPlacement.Inside(_perimeter, p.x, p.z)) _validPoints.Add(p);
            if (bad > 0) Debug.LogWarning("[7Days] " + bad + " puntos de botín del JSON fuera del perímetro.");
        }

        /// <summary>Puntos del mapa donde puede aparecer botín (dentro del perímetro y, con el NavMesh listo, alcanzables).</summary>
        public IReadOnlyList<LootPoint> ValidPoints => _perimeter == null ? (IReadOnlyList<LootPoint>)Layout.points : _validPoints;

        /// <summary>El NavMesh ya está horneado: revisa los puntos y lo que ya hay en el suelo, recoloca lo que no valga y escribe la auditoría.</summary>
        public void OnNavMeshReady()
        {
            _navReady = true;
            _reach.Clear();
            _validPoints.RemoveAll(p => !TryPlace(new Vector3(p.x, 0.35f, p.z), out _, out _));
            for (int i = _active.Count - 1; i >= 0; i--) Revalidate(_active[i]);
            Audit("al construir el mapa");
        }

        /// <summary>
        /// Valida una posición de botín: dentro del perímetro con 1 m de margen, sobre el NavMesh y con camino completo desde el inicio del jugador. Si no vale,
        /// la acerca al centro, la pega al NavMesh o usa el punto válido más cercano. false si no hay ninguna (se descarta).
        /// </summary>
        public bool TryPlace(Vector3 wanted, out Vector3 placed, out bool moved)
        {
            placed = wanted; moved = false;
            if (_perimeter == null) return true;
            float lift = Mathf.Clamp(wanted.y, 0.3f, 0.6f);
            var c = LootPlacement.Clamp(_perimeter, wanted.x, wanted.z);
            var p = new Vector3(c[0], wanted.y, c[1]);
            moved = (p - wanted).sqrMagnitude > 0.01f;
            if (!_navReady) { placed = p; return true; }
            if (NavMesh.SamplePosition(p, out var hit, 2.5f, NavMesh.AllAreas) || NavMesh.SamplePosition(p, out hit, 8f, NavMesh.AllAreas))
            {
                if (LootPlacement.Inside(_perimeter, hit.position.x, hit.position.z) && Reachable(hit.position))
                {
                    var flat = new Vector3(hit.position.x - wanted.x, 0f, hit.position.z - wanted.z);
                    moved |= flat.sqrMagnitude > 0.6f * 0.6f;
                    placed = hit.position + Vector3.up * lift;   // siempre sobre el punto del NavMesh que se validó
                    return true;
                }
            }
            // el punto válido más cercano del mapa
            float best = float.MaxValue; bool found = false;
            foreach (var pt in _validPoints)
            {
                float d = (pt.x - p.x) * (pt.x - p.x) + (pt.z - p.z) * (pt.z - p.z);
                if (d >= best) continue;
                if (!NavMesh.SamplePosition(new Vector3(pt.x, 0.35f, pt.z), out var h2, 2.5f, NavMesh.AllAreas) || !Reachable(h2.position)) continue;
                best = d; placed = h2.position + Vector3.up * lift; found = true;
            }
            moved = true;
            return found;
        }

        private bool Reachable(Vector3 target)
        {
            // caché por celda de 10 cm: con celdas grandes, un drop en una isla del NavMesh (rodeada de obstáculos) heredaba el "sí" de su vecino
            long key = ((long)Mathf.RoundToInt(target.x * 10f) << 40) ^ ((long)(Mathf.RoundToInt(target.z * 10f) & 0xFFFFF) << 16) ^ (Mathf.RoundToInt(target.y * 2f) & 0xFFFF);
            if (_reach.TryGetValue(key, out bool ok)) return ok;
            if (_path == null) _path = new NavMeshPath();
            ok = NavMesh.SamplePosition(_start, out var s, 4f, NavMesh.AllAreas) && NavMesh.CalculatePath(s.position, target, NavMesh.AllAreas, _path) && _path.status == NavMeshPathStatus.PathComplete;
            _reach[key] = ok;
            return ok;
        }

        /// <summary>Recoloca un pickup que no valga (o lo quita si no hay sitio). true si sigue en el suelo.</summary>
        private bool Revalidate(LootPickup p)
        {
            if (p == null || !p.gameObject.activeSelf) return false;
            var at = p.transform.position + Vector3.up * 0.35f;
            if (!TryPlace(at, out var placed, out bool moved))
            {
                Discarded++;
                Debug.LogWarning("[7Days] Botín descartado (sin sitio alcanzable): " + p.Item.kind + " " + p.Item.id + " en " + at.ToString("0.0"));
                _active.Remove(p); _glints.Remove(p); _pool.Release(p);
                return false;
            }
            if (moved)
            {
                Relocated++;
                Debug.Log("[7Days] Botín recolocado: " + p.Item.kind + " " + p.Item.id + " de " + at.ToString("0.0") + " a " + placed.ToString("0.0"));
            }
            p.transform.position = new Vector3(placed.x, placed.y - 0.35f, placed.z);
            p.Reachable = _navReady;
            return true;
        }

        /// <summary>Auditoría: "Loot: N válidos, M fuera de rango" (pickups en el suelo y puntos del mapa).</summary>
        public int Audit(string when)
        {
            int ok = 0, bad = 0;
            foreach (var p in _active)
            {
                if (p == null || !p.gameObject.activeSelf) continue;
                var pos = p.transform.position;
                bool inside = _perimeter == null || LootPlacement.Inside(_perimeter, pos.x, pos.z);
                bool nav = !_navReady || (NavMesh.SamplePosition(pos, out var h, 1.5f, NavMesh.AllAreas) && Reachable(h.position));
                if (inside && nav && pos.y > -1f) ok++; else bad++;
            }
            int pointsOut = Layout.points.Length - _validPoints.Count;
            Debug.Log("[7Days] Loot: " + (ok + _validPoints.Count) + " válidos, " + (bad + pointsOut) + " fuera de rango (" + when + "; " + ok + " pickups en el suelo, " + _validPoints.Count + " puntos; " + Relocated + " recolocados, " + Discarded + " descartados).");
            return bad + pointsOut;
        }

        public void SetPlayer(GameObject player)
        {
            _player = player.transform;
            _inventory = player.GetComponent<PlayerInventory>();
            var shooting = player.GetComponent<PlayerShooting>();
            if (shooting != null) shooting.WeaponDropped += (w, at) => SpawnForced(LootItem.Weapon(w.Data.id, w.Magazine), at);
        }

        public PlayerInventory Inventory => _inventory;

        public int ContainerCount => _containers.Count;

        /// <summary>Resumen para la consola al (re)construir el mapa: puntos del layout, contenedores creados y pickups activos.</summary>
        public string Describe()
        {
            int ground = 0, weapons = 0;
            foreach (var p in Layout.points) if (p.container == "ground") ground++;
            foreach (var a in _active) if (a != null && a.Item != null && a.Item.kind == "weapon") weapons++;
            return "[7Days] Loot reconstruido: " + Layout.points.Length + " puntos (" + ground + " en el suelo), " + _containers.Count + " contenedores buscables, "
                + SkippedPoints + " puntos omitidos (sin mueble/coche), " + _active.Count + " pickups activos (" + weapons + " armas, " + GuaranteedWeaponCount + " garantizadas). Drops de zombis: " + DropsSpawned + " en " + DropAttempts + " muertes. Registro de interacción: " + PlayerInteract.Registry.Count + ".";
        }

        public void RegisterContainer(SearchableContainer c) { _containers[c.Point.id] = c; _glints.Add(c); }

        // ---------------------------------------------------------------- spawn
        /// <summary>Deja un objeto en el suelo. Devuelve null si se llegó al tope de pickups activos.</summary>
        public LootPickup Spawn(LootItem item, Vector3 position, string pointId = null) => Spawn(item, position, pointId, false);

        /// <summary>Como Spawn pero sin el tope de pickups: armas que suelta el jugador y el drop de pity (nunca se pierden).</summary>
        public LootPickup SpawnForced(LootItem item, Vector3 position) => Spawn(item, position, null, true);

        private LootPickup Spawn(LootItem item, Vector3 position, string pointId, bool force)
        {
            if (item == null || (!force && !State.CanSpawn(_active.Count))) return null;
            // regla dura: nada fuera del perímetro, del NavMesh ni sin camino desde el inicio
            if (!TryPlace(position, out var placed, out bool moved))
            {
                Discarded++;
                Debug.LogWarning("[7Days] Botín descartado (sin sitio alcanzable): " + item.kind + " " + item.id + " en " + position.ToString("0.0"));
                return null;
            }
            if (moved) { Relocated++; Debug.Log("[7Days] Botín recolocado: " + item.kind + " " + item.id + " de " + position.ToString("0.0") + " a " + placed.ToString("0.0")); }
            var p = _pool.Get();
            p.Setup(item, placed, pointId, this);
            p.Reachable = _navReady || _perimeter == null;
            _active.Add(p);
            _glints.Add(p);
            return p;
        }

        /// <summary>Punto "ground": se tira en su tabla y el objeto queda en el suelo.</summary>
        public bool SpawnGroundPoint(LootPoint point)
        {
            var item = LootRoller.RollPoint(Layout, point, _rng);
            return item != null && Spawn(item, new Vector3(point.x, 0.35f, point.z), point.id) != null;
        }

        /// <summary>El jugador terminó de buscar: tira en la tabla y deja el botín junto al contenedor. false = el suelo está lleno (reintentar luego).</summary>
        public bool SearchResult(SearchableContainer c)
        {
            if (!State.CanSpawn(_active.Count)) return false;
            // armas: probabilidad por contenedor con pity timer y puntos con arma garantizada; nunca una que el jugador ya tiene (WeaponSearch)
            var item = WeaponSearch.RollSearch(Layout, c.Point, _rng, OwnedWeapons(), State.Search, out bool gaveWeapon);
            State.MarkTaken(c.Point.id);
            if (item == null) { HudMessages.Add("Está vacío", new Color(0.8f, 0.8f, 0.8f)); return true; }
            var pos = c.transform.position + new Vector3((float)(_rng.NextDouble() - 0.5) * 0.8f, 0.5f, (float)(_rng.NextDouble() - 0.5) * 0.8f);
            Spawn(item, pos, c.Point.id);
            HudMessages.Add(gaveWeapon ? "¡Encontraste un arma!" : "Encontraste algo", gaveWeapon ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.92f, 0.65f));
            return true;
        }

        private HashSet<string> OwnedWeapons()
        {
            var set = new HashSet<string>();
            if (_inventory != null) foreach (var w in _inventory.Weapons) set.Add(w.Data.id);
            else foreach (var id in Layout.startWeapons) set.Add(id);
            return set;
        }

        public int SkippedPoints;   // puntos del layout que no encontraron su mueble/coche (se avisa en la consola)

        // Armas garantizadas: son pickups VISIBLES (con brillo) colocados sobre su punto, no un resultado de búsqueda. Antes solo daban arma al buscar, así que no había
        // ninguna arma en el suelo y la consola decía "0 armas".
        private readonly Dictionary<string, LootPickup> _guaranteedPickups = new Dictionary<string, LootPickup>();
        public int GuaranteedWeaponCount { get { int n = 0; foreach (var kv in _guaranteedPickups) if (kv.Value != null && kv.Value.gameObject.activeSelf) n++; return n; } }

        /// <summary>Coloca un arma (que el jugador no tenga ni haya ya en el suelo) sobre cada punto garantizado sin pickup vivo, hasta llegar al mínimo. Devuelve cuántas colocó.</summary>
        public int PlaceGuaranteedWeapons(int maxToPlace = int.MaxValue)
        {
            var cfg = Layout.weaponSearch;
            var taken = OwnedWeapons();
            foreach (var p in _active) if (p != null && p.Item != null && p.Item.kind == "weapon") taken.Add(p.Item.id);
            int unowned = 0;
            foreach (var w in WeaponCatalog.Defaults()) if (!taken.Contains(w.id)) unowned++;

            int available = GuaranteedWeaponCount;
            int need = Mathf.Min(maxToPlace, WeaponGuarantee.ToRearm(available, 0, unowned + available, cfg.guaranteedMin));
            int placed = 0;
            foreach (var pt in Layout.points)
            {
                if (need <= 0) break;
                if (!pt.guaranteeWeapon) continue;
                if (_guaranteedPickups.TryGetValue(pt.id, out var existing) && existing != null && existing.gameObject.activeSelf) continue;
                string id = WeaponSearch.PickWeapon(cfg, pt.risk, taken, _rng);
                if (id == null) break;
                taken.Add(id);
                var pickup = Spawn(LootItem.Weapon(id), new Vector3(pt.x, 0.55f, pt.z), pt.id);
                if (pickup == null) continue;   // tope de pickups en el suelo
                _guaranteedPickups[pt.id] = pickup;
                if (_containers.TryGetValue(pt.id, out var c)) c.MarkEmpty();   // el arma ya está a la vista: el mueble/caja no la vuelve a dar
                placed++; need--;
            }
            return placed;
        }

        private void RearmGuaranteedWeapons()
        {
            int placed = PlaceGuaranteedWeapons();
            if (placed > 0) HudMessages.Add("Se oye ruido: alguien dejó un arma por ahí", new Color(1f, 0.85f, 0.3f), 3f);
        }

        public void Collected(LootPickup p)
        {
            _active.Remove(p);
            _glints.Remove(p);
            if (p.SourcePointId != null) State.MarkTaken(p.SourcePointId);
            _pool.Release(p);
        }

        /// <summary>Drop al matar un zombie de este tipo (con las probabilidades de la tabla y los topes por día y de suelo).</summary>
        public int DropAttempts { get; private set; }
        public int DropsSpawned { get; private set; }

        public bool TryDrop(string zombieKind, Vector3 position)
        {
            DropAttempts++;
            var item = LootRoller.RollDrop(Layout, zombieKind, _rng);
            if (item == null) return false;
            if (item.kind == "weapon" && OwnedWeapons().Contains(item.id))
            {
                // nunca un arma repetida: se queda en munición de su tipo
                var data = WeaponCatalog.Get(item.id);
                item = data != null && data.ammoType != AmmoType.None ? LootItem.Ammo(data.ammoType, Mathf.Max(2, data.magazineSize / 3)) : null;
                if (item == null) return false;
            }
            if (!State.TryRegisterDrop(_active.Count)) { Debug.Log("[7Days] Drop de '" + zombieKind + "' descartado por el tope (hoy " + State.DropsToday + "/" + Layout.maxDropsPerDay + ", suelo " + _active.Count + "/" + Layout.maxActivePickups + ")."); return false; }
            var p = Spawn(item, new Vector3(position.x, 0.35f, position.z), null);
            if (p == null) return false;
            DropsSpawned++;
            Debug.Log("[7Days] Drop #" + DropsSpawned + " de '" + zombieKind + "': " + item.kind + " " + item.id + " x" + item.amount + " (hoy " + State.DropsToday + "/" + Layout.maxDropsPerDay + ", intentos " + DropAttempts + ").");
            return true;
        }

        /// <summary>El jefe siempre suelta algo raro o mejor: no cuenta para el tope diario de drops ni se pierde si hay muchos pickups en el suelo.</summary>
        public bool DropBoss(Vector3 position)
        {
            var item = LootRoller.RollDrop(Layout, "boss", _rng);
            if (item == null) return false;
            HudMessages.Add("¡El jefe soltó un botín raro!", new Color(1f, 0.85f, 0.3f), 3.5f);
            return Spawn(item, new Vector3(position.x, 0.35f, position.z), null) != null;
        }

        /// <summary>Solo para pruebas (tecla L): un drop de la tabla de zombies especiales, sin tirar la probabilidad pero con los mismos topes.</summary>
        public bool DebugDrop(Vector3 position)
        {
            var item = LootRoller.Roll(Layout, "drop_special", _rng);
            if (item == null || !State.TryRegisterDrop(_active.Count)) { HudMessages.Add("Tope de drops alcanzado"); return false; }
            return Spawn(item, new Vector3(position.x, 0.35f, position.z), null) != null;
        }

        /// <summary>Empieza un día: reaparece una fracción pequeña de lo vaciado (nunca más del tope de pickups ni de los puntos "once").</summary>
        /// <summary>Devuelve las balas esperadas de lo que reapareció (la economía de munición completa el resto del día).</summary>
        public double NewDay(int day)
        {
            var refilled = State.NewDay(day, _active.Count, _rng);
            foreach (var point in refilled)
            {
                if (point.container == "ground") SpawnGroundPoint(point);
                else if (_containers.TryGetValue(point.id, out var c)) c.Refill();
            }
            RearmGuaranteedWeapons();
            Audit("reposición del día " + day);
            HudMessages.Add("Día " + day + ": reaparece algo de botín", new Color(1f, 0.92f, 0.65f));
            return AmmoEconomy.RefillRounds(Layout, refilled);
        }

        /// <summary>Posición de un punto del mapa (para las tandas de munición durante la oleada).</summary>
        public System.Collections.Generic.IReadOnlyList<LootPoint> Points => Layout.points;

        // ---------------------------------------------------------------- bucle
        private void Update()
        {
            LootPickup.VisibleMarkers = 0;   // los cuenta cada pickup en su LateUpdate (panel F6)
            PulseContainers();
            _pickTimer -= Time.deltaTime;
            if (_pickTimer <= 0f) { _pickTimer = 0.1f; AutoPickup(); }
            _glintTimer -= Time.deltaTime;
            if (_glintTimer <= 0f) { _glintTimer = 0.5f; AssignGlints(); }
            // red de seguridad: un pickup que acabe fuera del perímetro o bajo el suelo (empujes, explosiones) se recoloca o se quita
            _boundsTimer -= Time.deltaTime;
            if (_boundsTimer <= 0f && _perimeter != null)
            {
                _boundsTimer = 2f;
                for (int i = _active.Count - 1; i >= 0; i--)
                {
                    var p = _active[i]; if (p == null) continue;
                    var pos = p.transform.position;
                    if (pos.y < -1f || !LootPlacement.Inside(_perimeter, pos.x, pos.z)) Revalidate(p);
                }
            }
        }

        /// <summary>Munición y botiquines se recogen al acercarse. Si no caben: aviso y el pickup queda (no se consume).</summary>
        private void AutoPickup()
        {
            if (_player == null || _inventory == null) return;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var p = _active[i];
                if (p.Item.kind == "weapon") continue;
                float dx = _player.position.x - p.transform.position.x, dz = _player.position.z - p.transform.position.z;
                if (dx * dx + dz * dz > 1.5f * 1.5f || Mathf.Abs(_player.position.y - p.transform.position.y) > 2f) continue;
                var result = _inventory.TryPickup(p.Item, out string message);
                if (result == PickupResult.Picked)
                {
                    HudMessages.Add(message, new Color(0.6f, 1f, 0.6f));
                    Collected(p);
                }
                else if (Time.time >= p.NextFullToast)
                {
                    HudMessages.Add(message, new Color(1f, 0.7f, 0.4f));
                    p.NextFullToast = Time.time + 2.5f;
                }
            }
        }

        /// <summary>Solo las luces de los MaxGlintLights objetos más cercanos están encendidas.</summary>
        private void AssignGlints()
        {
            if (_player == null) return;
            _glints.RemoveAll(g => g == null || (g is LootPickup lp && !lp.gameObject.activeSelf));
            var order = new List<ILootGlint>(_glints);
            // los drops de zombis (sin punto de origen) cuentan como si estuvieran más cerca: su brillo suave tiene prioridad para localizarlos
            float Key(ILootGlint g) => (g.GlintPosition - _player.position).sqrMagnitude * (g is LootPickup lp && lp.SourcePointId == null ? 0.25f : 1f);
            order.Sort((a, b) => Key(a).CompareTo(Key(b)));
            int on = 0, sparkles = 0;
            foreach (var g in order)
            {
                if (g.GlintLight == null) continue;
                bool searched = g is SearchableContainer sc && sc.Searched;
                bool enable = !searched && on < MaxGlintLights;
                g.GlintLight.enabled = enable;
                if (enable) on++;
                // destello de partículas: solo los que están a menos de SparkleRange (los de lejos no se distinguen y cuestan)
                if (g is LootPickup pickup)
                {
                    bool near = (g.GlintPosition - _player.position).sqrMagnitude < SparkleRange * SparkleRange;
                    pickup.SetSparkles(near);
                    if (near) sparkles++;
                }
            }
            LightsOn = on;
            LootPickup.EmittingSparkles = sparkles;
        }

        /// <summary>Los contenedores sin buscar laten con un brillo tenue (solo los que tienen la luz encendida, como mucho MaxGlintLights).</summary>
        private void PulseContainers()
        {
            foreach (var kv in _containers)
            {
                var c = kv.Value;
                if (c == null || c.Searched || c.GlintLight == null || !c.GlintLight.enabled) continue;
                c.GlintLight.intensity = 0.4f + 0.3f * SupplyVisibility.Pulse(Time.time * 0.8f, (kv.Key.GetHashCode() & 255) / 255f);
            }
        }

        /// <summary>Pickups activos (para el HUD de suministros).</summary>
        public System.Collections.Generic.IReadOnlyList<LootPickup> Active => _active;

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }

    /// <summary>Punto de entrada para el sistema de zombies (Fase 3): al morir uno, `DropSystem.OnZombieKilled("brute", posición)`.</summary>
    public static class DropSystem
    {
        /// <summary>zombieKind: common | runner | brute | spitter | boss. Devuelve true si soltó algo.</summary>
        public static bool OnZombieKilled(string zombieKind, Vector3 position) => LootManager.Instance != null && (zombieKind == "boss" ? LootManager.Instance.DropBoss(position) : LootManager.Instance.TryDrop(zombieKind, position));
    }
}
