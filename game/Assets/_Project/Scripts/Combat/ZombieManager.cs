using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using SevenDays.Core;
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
        private NavMeshSurface _surface;

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
            foreach (var z in _all) z.SetPlayer(_player, _playerHealth);
        }

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
            NavMeshReady = true;
        }

        public ZombieController Spawn(string typeId, Vector3 position, float healthMult = 1f, float speedMult = 1f, float damageMult = 1f)
        {
            var type = TypeById(typeId);
            return type == null ? null : Spawn(type, position, healthMult, speedMult, damageMult);
        }

        public ZombieController Spawn(ZombieTypeData type, Vector3 position, float healthMult = 1f, float speedMult = 1f, float damageMult = 1f)
        {
            if (ActiveCount >= MaxZombies && !type.isBoss) return null;
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
            z.Activate(type, position, healthMult, speedMult, damageMult);
            return z;
        }

        private void OnKilled(ZombieController z) => ZombieKilled?.Invoke(z);

        public void DespawnAll() { foreach (var z in _all) if (z.InUse) z.Deactivate(); }

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
