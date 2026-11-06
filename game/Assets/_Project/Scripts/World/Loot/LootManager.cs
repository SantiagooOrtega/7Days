using System.Collections.Generic;
using UnityEngine;
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

        private ObjectPool<LootPickup> _pool;
        private readonly List<LootPickup> _active = new List<LootPickup>();
        private readonly Dictionary<string, SearchableContainer> _containers = new Dictionary<string, SearchableContainer>();
        private readonly List<ILootGlint> _glints = new List<ILootGlint>();
        private System.Random _rng;
        private Transform _player;
        private PlayerInventory _inventory;
        private float _pickTimer, _glintTimer;

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

        public void SetPlayer(GameObject player)
        {
            _player = player.transform;
            _inventory = player.GetComponent<PlayerInventory>();
        }

        public int ContainerCount => _containers.Count;

        /// <summary>Resumen para la consola al (re)construir el mapa: puntos del layout, contenedores creados y pickups activos.</summary>
        public string Describe()
        {
            int ground = 0, weapons = 0;
            foreach (var p in Layout.points) if (p.container == "ground") ground++;
            foreach (var a in _active) if (a != null && a.Item != null && a.Item.kind == "weapon") weapons++;
            return "[7Days] Loot reconstruido: " + Layout.points.Length + " puntos (" + ground + " en el suelo), " + _containers.Count + " contenedores buscables, "
                + SkippedPoints + " puntos omitidos (sin mueble/coche), " + _active.Count + " pickups activos (" + weapons + " armas). Registro de interacción: " + PlayerInteract.Registry.Count + ".";
        }

        public void RegisterContainer(SearchableContainer c) { _containers[c.Point.id] = c; _glints.Add(c); }

        // ---------------------------------------------------------------- spawn
        /// <summary>Deja un objeto en el suelo. Devuelve null si se llegó al tope de pickups activos.</summary>
        public LootPickup Spawn(LootItem item, Vector3 position, string pointId = null)
        {
            if (item == null || !State.CanSpawn(_active.Count)) return null;
            var p = _pool.Get();
            p.Setup(item, position, pointId, this);
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

        /// <summary>Al empezar un día, vuelve a armar puntos de arma garantizada hasta que haya al menos guaranteedMin armas disponibles (sin pasar de las que le faltan al jugador).</summary>
        private void RearmGuaranteedWeapons()
        {
            var owned = OwnedWeapons();
            int unowned = 0;
            foreach (var w in WeaponCatalog.Defaults()) if (!owned.Contains(w.id)) unowned++;
            var guaranteed = new List<SearchableContainer>();
            foreach (var c in _containers.Values) if (c != null && c.Point.guaranteeWeapon) guaranteed.Add(c);
            int available = 0, onGround = 0;
            foreach (var c in guaranteed) if (!c.Searched) available++;
            foreach (var p in _active) if (p != null && p.Item != null && p.Item.kind == "weapon" && !owned.Contains(p.Item.id)) onGround++;
            int need = WeaponGuarantee.ToRearm(available, onGround, unowned, Layout.weaponSearch.guaranteedMin);
            for (int i = 0; i < guaranteed.Count && need > 0; i++)
            {
                if (!guaranteed[i].Searched) continue;
                guaranteed[i].Refill();
                foreach (var ps in State.Points) if (ps.Point.id == guaranteed[i].Point.id) ps.Active = true;
                need--;
            }
            if (need < 0 || available + onGround < Layout.weaponSearch.guaranteedMin) HudMessages.Add("Se oye ruido: alguien dejó un arma por ahí", new Color(1f, 0.85f, 0.3f), 3f);
        }

        public void Collected(LootPickup p)
        {
            _active.Remove(p);
            _glints.Remove(p);
            if (p.SourcePointId != null) State.MarkTaken(p.SourcePointId);
            _pool.Release(p);
        }

        /// <summary>Drop al matar un zombie de este tipo (con las probabilidades de la tabla y los topes por día y de suelo).</summary>
        public bool TryDrop(string zombieKind, Vector3 position)
        {
            var item = LootRoller.RollDrop(Layout, zombieKind, _rng);
            if (item == null) return false;
            if (!State.TryRegisterDrop(_active.Count)) return false;
            return Spawn(item, new Vector3(position.x, 0.35f, position.z), null) != null;
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
        public void NewDay(int day)
        {
            foreach (var point in State.NewDay(day, _active.Count, _rng))
            {
                if (point.container == "ground") SpawnGroundPoint(point);
                else if (_containers.TryGetValue(point.id, out var c)) c.Refill();
            }
            RearmGuaranteedWeapons();
            HudMessages.Add("Día " + day + ": reaparece algo de botín", new Color(1f, 0.92f, 0.65f));
        }

        // ---------------------------------------------------------------- bucle
        private void Update()
        {
            _pickTimer -= Time.deltaTime;
            if (_pickTimer <= 0f) { _pickTimer = 0.1f; AutoPickup(); }
            _glintTimer -= Time.deltaTime;
            if (_glintTimer <= 0f) { _glintTimer = 0.5f; AssignGlints(); }
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
            order.Sort((a, b) => (a.GlintPosition - _player.position).sqrMagnitude.CompareTo((b.GlintPosition - _player.position).sqrMagnitude));
            int on = 0;
            foreach (var g in order)
            {
                if (g.GlintLight == null) continue;
                bool searched = g is SearchableContainer sc && sc.Searched;
                bool enable = !searched && on < MaxGlintLights;
                g.GlintLight.enabled = enable;
                if (enable) on++;
            }
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }

    /// <summary>Punto de entrada para el sistema de zombies (Fase 3): al morir uno, `DropSystem.OnZombieKilled("brute", posición)`.</summary>
    public static class DropSystem
    {
        /// <summary>zombieKind: common | runner | brute | spitter | boss. Devuelve true si soltó algo.</summary>
        public static bool OnZombieKilled(string zombieKind, Vector3 position) => LootManager.Instance != null && (zombieKind == "boss" ? LootManager.Instance.DropBoss(position) : LootManager.Instance.TryDrop(zombieKind, position));
    }
}
