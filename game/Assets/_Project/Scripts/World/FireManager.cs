using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using SevenDays.Core;
using SevenDays.Look;
using SevenDays.Procedural;

namespace SevenDays.World
{
    /// <summary>
    /// Todos los fuegos del mapa. Los crea desde un pool (FireEffect), mantiene encendidas solo las MaxFireLights luces más cercanas al jugador,
    /// aplica el daño leve por quemadura si el jugador se queda dentro de un fuego y reajusta las luces cuando el gobernador recorta el ambiente.
    /// </summary>
    public class FireManager : MonoBehaviour
    {
        public static FireManager Instance { get; private set; }
        /// <summary>true mientras el jugador se está quemando (lo usa el HUD).</summary>
        public static bool PlayerBurning { get; private set; }

        private FireSettings _s;
        private ObjectPool<FireEffect> _pool;
        private readonly List<FireEffect> _fires = new List<FireEffect>();
        private Transform _player;
        private Health _health;
        private float _lightTimer, _burnTimer, _burningUntil;

        public int Count => _fires.Count;
        public IReadOnlyList<FireEffect> Fires => _fires;

        public static FireManager Create(Transform root, FireSettings settings)
        {
            var go = new GameObject("Fire Manager");
            go.transform.SetParent(root, false);
            var m = go.AddComponent<FireManager>();
            m._s = settings;
            m._pool = new ObjectPool<FireEffect>(() => FireEffect.Create(go.transform), null, fx => fx.Extinguish(), fx => { if (fx != null) Destroy(fx.gameObject); }, false, 8, 32);
            Instance = m;
            return m;
        }

        public void SetPlayer(GameObject player)
        {
            _player = player.transform;
            _health = player.GetComponent<Health>();
        }

        /// <summary>Enciende un fuego. Devuelve null si los fuegos están desactivados o se llegó al tope MaxFires.</summary>
        public FireEffect Spawn(FireKind kind, Vector3 position)
        {
            if (_s == null || !_s.Enabled) return null;
            if (_fires.Count >= _s.MaxFires) { Debug.LogWarning("[7Days] Tope de fuegos (" + _s.MaxFires + "): se ignora uno."); return null; }
            var fx = _pool.Get();
            fx.Ignite(position, kind, _s);
            _fires.Add(fx);
            AssignLights();
            return fx;
        }

        /// <summary>Apaga un fuego y lo devuelve al pool (por ejemplo, el director lo apaga al cambiar de día).</summary>
        public void Despawn(FireEffect fx)
        {
            if (fx == null || !_fires.Remove(fx)) return;
            _pool.Release(fx);
            AssignLights();
        }

        private void OnEnable() => AtmosphereBudget.LevelChanged += OnLevelChanged;
        private void OnDisable() => AtmosphereBudget.LevelChanged -= OnLevelChanged;
        private void OnDestroy() { if (Instance == this) { Instance = null; PlayerBurning = false; } }
        private void OnLevelChanged(int level) => AssignLights();

        private void Update()
        {
            _lightTimer -= Time.deltaTime;
            if (_lightTimer <= 0f) { _lightTimer = 0.4f; AssignLights(); }

            _burnTimer += Time.deltaTime;
            if (_burnTimer >= _s.BurnTickSeconds)
            {
                ApplyBurn(_burnTimer);
                _burnTimer = 0f;
            }
            PlayerBurning = Time.time < _burningUntil;
        }

        /// <summary>Solo las luces de los fuegos más cercanos al jugador quedan encendidas (el tope baja con el nivel de recorte).</summary>
        private void AssignLights()
        {
            if (_fires.Count == 0) return;
            Vector3 viewer = _player != null ? _player.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            var pos = new List<V3>(_fires.Count);
            var pri = new List<int>(_fires.Count);
            foreach (var f in _fires) { var p = f.transform.position; pos.Add(new V3(p.x, p.y, p.z)); pri.Add(f.Profile.Priority); }
            int max = FireMath.MaxLights(_s.MaxFireLights, AtmosphereBudget.Level);
            var chosen = new HashSet<int>(FireMath.NearestLights(pos, pri, new V3(viewer.x, viewer.y, viewer.z), max));
            for (int i = 0; i < _fires.Count; i++) _fires[i].Light.enabled = chosen.Contains(i);
        }

        private void ApplyBurn(float dt)
        {
            if (_player == null || _health == null || _health.IsDead || _s.BurnDps <= 0f) return;
            foreach (var f in _fires)
            {
                var fp = f.transform.position;
                float dx = _player.position.x - fp.x, dz = _player.position.z - fp.z;
                float horizontal = Mathf.Sqrt(dx * dx + dz * dz);
                float dmg = FireMath.BurnDamage(horizontal, _player.position.y - fp.y, f.Profile.BurnRadius, _s.BurnDps, dt);
                if (dmg <= 0f) continue;
                _health.TakeDamage(new DamageInfo { Amount = dmg, Point = _player.position, Direction = Vector3.up, Source = f.gameObject });
                _burningUntil = Time.time + 0.6f;
                return; // varios fuegos solapados no suman dos veces en el mismo tick
            }
        }
    }
}
