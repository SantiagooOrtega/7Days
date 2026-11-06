using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using SevenDays.Core;
using SevenDays.Player;
using SevenDays.World;
using SevenDays.Procedural;

namespace SevenDays.Combat
{
    /// <summary>
    /// Gestor de zombies: genera el NavMesh en tiempo de ejecución (el mapa se construye al iniciar), mantiene un pool de zombies reutilizables
    /// con un tope de simultáneos y reparte los parámetros de dificultad. Los zombies de más esperan: Spawn devuelve null al llegar al tope.
    /// </summary>
    public class ZombieManager : MonoBehaviour
    {
        public static ZombieManager Instance { get; private set; }
        public int MaxZombies = 24;
        public int ActiveCount { get { int n = 0; foreach (var z in _all) if (z.InUse && !z.Health.IsDead) n++; return n; } }
        public IReadOnlyList<ZombieController> All => _all;
        public bool NavMeshReady { get; private set; }
        public event System.Action<ZombieController> ZombieKilled;

        private readonly List<ZombieController> _all = new List<ZombieController>();
        private readonly Dictionary<string, ZombieTypeData> _types = new Dictionary<string, ZombieTypeData>();
        private Transform _player;
        private Health _playerHealth;
        private NavMeshSurface _surface, _giantSurface;
        /// <summary>Tipo de agente de NavMesh de los gigantes (radio grande: no caben por puertas ni ventanas). -1 hasta que se hornea.</summary>
        public int GiantAgentTypeId { get; private set; } = -1;
        public const float GiantAgentRadius = 0.8f;
        private PlayerCover _cover;
        private bool _navDirty;
        private float _nextNavRefresh;
        public ZombieController ActiveBoss { get; private set; }
        /// <summary>Exposición del jugador (1 = a la vista; baja al cubrirse). Los zombis ven menos lejos a un jugador cubierto.</summary>
        public float PlayerExposure => _cover != null ? _cover.ExposureValue : 1f;
        /// <summary>Parámetros del día que fija la IA director (multiplicadores y proporciones). Todos los spawns los respetan.</summary>
        public DirectorTuning Tuning { get; private set; } = new DirectorTuning();
        private readonly System.Random _rng = new System.Random(20240601);
        public Transform PlayerTransform => _player;
        public Health PlayerHealth => _playerHealth;
        /// <summary>Cuánto lleva el jugador seguido en cobertura (la IA del lanzador decide con esto cuándo flanquear).</summary>
        public float PlayerCoverSeconds { get; private set; }

        public static ZombieManager Create(Transform parent, List<ZombieTypeData> types = null)
        {
            var go = new GameObject("ZombieManager");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<ZombieManager>();
            Instance = m;
            foreach (var t in types ?? ZombieCatalog.Defaults()) m._types[t.id] = t;
            HitboxLayer.ConfigureCollisions();
            return m;
        }

        public void SetPlayer(Transform player)
        {
            _player = player;
            _playerHealth = player.GetComponent<Health>();
            _cover = player.GetComponent<PlayerCover>();
            foreach (var z in _all) z.SetPlayer(_player, _playerHealth);
        }

        public void SetTuning(DirectorTuning t)
        {
            var errors = t.Validate();
            if (errors.Count > 0) { Debug.LogWarning("[7Days] DirectorTuning inválido, se ignora: " + string.Join("; ", errors)); return; }
            Tuning = t.Clone();
        }

        /// <summary>Genera un zombi cuyo tipo sale de las proporciones del director.</summary>
        public ZombieController SpawnByTuning(Vector3 position) => Spawn(Tuning.Pick(_rng.NextDouble()), position);

        public ZombieTypeData TypeById(string id) => _types.TryGetValue(id, out var t) ? t : null;

        /// <summary>Genera el NavMesh con los colliders del nivel (no incluye la capa del jugador ni los hitboxes). Llamar cuando el nivel ya está construido.</summary>
        public void BakeNavMesh()
        {
            if (_surface == null)
            {
                _surface = gameObject.AddComponent<NavMeshSurface>();
                _surface.collectObjects = CollectObjects.All;
                _surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                _surface.layerMask = ~((1 << 2) | HitboxLayer.Mask);
                _surface.defaultArea = 0;
            }
            Physics.SyncTransforms();
            _surface.BuildNavMesh();
            BakeGiantNavMesh();
            NavMeshReady = true;
        }

        /// <summary>
        /// NavMesh para los gigantes: un tipo de agente con radio 0.8 m (1.6 m de ancho) que no cabe por las puertas (1.1–1.2 m). Además, sobre cada casa se pone un volumen
        /// "no caminable" que solo afecta a ese agente, así que aunque una puerta fuera ancha, el interior no es transitable para él.
        /// </summary>
        private void BakeGiantNavMesh()
        {
            var settings = NavMesh.CreateSettings();
            settings.agentRadius = GiantAgentRadius;
            settings.agentHeight = 2.8f;
            settings.agentClimb = 0.4f;
            settings.agentSlope = 40f;
            GiantAgentTypeId = settings.agentTypeID;

            foreach (var f in HouseController.Footprints)
            {
                var go = new GameObject("House NoGiants");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(f.Cx, 1.5f, f.Cz);
                var vol = go.AddComponent<NavMeshModifierVolume>();
                vol.size = new Vector3(f.Hx * 2f + 0.4f, 6f, f.Hz * 2f + 0.4f);
                vol.area = 1;                                   // Not Walkable
                RestrictToAgent(vol, GiantAgentTypeId);
            }

            var child = new GameObject("GiantNavMesh");
            child.transform.SetParent(transform, false);
            _giantSurface = child.AddComponent<NavMeshSurface>();
            _giantSurface.agentTypeID = GiantAgentTypeId;
            _giantSurface.collectObjects = CollectObjects.All;
            _giantSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            _giantSurface.layerMask = ~((1 << 2) | HitboxLayer.Mask);
            _giantSurface.defaultArea = 0;
            _giantSurface.BuildNavMesh();
        }

        /// <summary>
        /// NavMeshModifierVolume no expone en código qué agentes afecta (solo en el Inspector); su lista privada m_AffectedAgents vale [-1] = todos.
        /// Se fija por reflexión a solo el gigante. Si el campo cambiara en otra versión del paquete, el volumen afectaría a todos: se avisa y se quita.
        /// </summary>
        private static void RestrictToAgent(NavMeshModifierVolume vol, int agentTypeId)
        {
            var field = typeof(NavMeshModifierVolume).GetField("m_AffectedAgents", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null && field.GetValue(vol) is List<int> list) { list.Clear(); list.Add(agentTypeId); return; }
            Debug.LogWarning("[7Days] No se pudo limitar el volumen de la casa al agente gigante (versión de AI Navigation distinta): se omite; quedan el radio del agente y la barrera de seguridad.");
            Destroy(vol.gameObject);
        }

        public ZombieController Spawn(string typeId, Vector3 position, float healthMult = 1f, float speedMult = 1f, float damageMult = 1f, bool ignoreCap = false)
        {
            var type = TypeById(typeId);
            return type == null ? null : Spawn(type, position, healthMult, speedMult, damageMult, ignoreCap);
        }

        public ZombieController Spawn(ZombieTypeData type, Vector3 position, float healthMult = 1f, float speedMult = 1f, float damageMult = 1f, bool ignoreCap = false)
        {
            if (ActiveCount >= MaxZombies && !type.isBoss && !ignoreCap) return null;
            ZombieController z = null;
            foreach (var c in _all) if (!c.InUse) { z = c; break; }
            if (z == null)
            {
                if (_all.Count >= MaxZombies + 4) return null; // los jefes y los invocados pueden pasarse un poco del tope
                var go = new GameObject("Zombie");
                go.transform.SetParent(transform, false);
                go.AddComponent<Health>();
                z = go.AddComponent<ZombieController>();
                z.Build();
                z.Killed += OnKilled;
                z.SetPlayer(_player, _playerHealth);
                _all.Add(z);
            }
            // los parámetros del director multiplican a los pedidos; la vida de un jefe no se toca (siempre 10–30 veces la de un común)
            z.Activate(type, position, healthMult * (type.isBoss ? 1f : Tuning.healthMultiplier), speedMult * Tuning.speedMultiplier, damageMult * Tuning.damageMultiplier);
            if (type.isBoss && z.Boss != null && z.Boss.Brain != null) z.Boss.Brain.Aggressiveness = Tuning.bossAggressiveness;
            if (type.isBoss) ActiveBoss = z;
            return z;
        }

        private void OnKilled(ZombieController z) => ZombieKilled?.Invoke(z);

        private void Update()
        {
            PlayerCoverSeconds = _cover != null && _cover.Active ? PlayerCoverSeconds + Time.deltaTime : 0f;
            // si una cobertura se rompió, el NavMesh se actualiza (como mucho cada 2 s, no en cada golpe)
            if (_navDirty && NavMeshReady && Time.time >= _nextNavRefresh)
            {
                _navDirty = false;
                _nextNavRefresh = Time.time + 2f;
                _surface.UpdateNavMesh(_surface.navMeshData);
                if (_giantSurface != null) _giantSurface.UpdateNavMesh(_giantSurface.navMeshData);
            }
        }

        public void RequestNavMeshRefresh() { _navDirty = true; }

        public void DespawnAll() { foreach (var z in _all) if (z.InUse) z.Deactivate(); }

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
