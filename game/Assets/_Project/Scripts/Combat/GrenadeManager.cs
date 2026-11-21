using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.Combat
{
    /// <summary>
    /// Granadas: pool de 6 (sin reservar memoria al lanzar ni por frame) y la detonación: daño de área con caída (GrenadeRules), cobertura sólida que la frena,
    /// empuje, cobertura ligera rota, daño al jugador si está muy cerca, sacudida de cámara, explosión con humo y sonido (BlastFx) y fuego breve.
    /// Cuenta las bajas por granada (estadísticas del día y del director).
    /// </summary>
    public class GrenadeManager : MonoBehaviour
    {
        public static GrenadeManager Instance { get; private set; }
        public const int PoolSize = GrenadeRules.Max;

        /// <summary>Bajas por granada desde que empezó la partida (DayLoop resta para sacar las del día).</summary>
        public int Kills { get; private set; }
        public int Thrown { get; private set; }
        public int Detonations { get; private set; }
        public event System.Action<Vector3, int> Detonated;   // centro y bajas de esa granada

        private readonly List<Grenade> _pool = new List<Grenade>();
        private PhysicsMaterial _bounce;
        private Transform _player;
        private Health _playerHealth;
        private static readonly Collider[] Buf = new Collider[48];

        public static GrenadeManager Create(Transform parent)
        {
            var go = new GameObject("Grenades");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<GrenadeManager>();
            m._bounce = new PhysicsMaterial("Grenade") { bounciness = 0.42f, dynamicFriction = 0.55f, staticFriction = 0.6f, bounceCombine = PhysicsMaterialCombine.Maximum };
            for (int i = 0; i < PoolSize; i++) m._pool.Add(Grenade.Create(go.transform, m, m._bounce));
            Instance = m;
            return m;
        }

        public void SetPlayer(GameObject player)
        {
            _player = player.transform; _playerHealth = player.GetComponent<Health>();
            var pc = player.GetComponent<Collider>();
            if (pc != null) foreach (var g in _pool) Physics.IgnoreCollision(g.Collider, pc, true);   // no rebota en quien la lanza
        }

        /// <summary>Lanza una granada del pool. false si las 6 están en el aire.</summary>
        public bool Throw(Vector3 origin, Vector3 velocity)
        {
            foreach (var g in _pool)
            {
                if (g.gameObject.activeSelf) continue;
                g.Launch(origin, velocity, GrenadeRules.FuseSeconds);
                Thrown++;
                return true;
            }
            return false;
        }

        public void Recycle(Grenade g) { g.Body.linearVelocity = Vector3.zero; g.gameObject.SetActive(false); }

        public void Detonate(Grenade g)
        {
            Vector3 center = g.transform.position + Vector3.up * 0.15f;
            Recycle(g);
            Detonations++;
            int kills = Explode(center);
            Detonated?.Invoke(center, kills);
        }

        /// <summary>La explosión en sí (también la usan las pruebas). Devuelve cuántos zombis mató.</summary>
        public int Explode(Vector3 center)
        {
            float radius = GrenadeRules.Radius;
            int solid = ~((1 << 2) | HitboxLayer.Mask);
            BlastFx.Instance?.Explode(center, radius * 0.6f);
            int kills = 0;

            var mgr = ZombieManager.Instance;
            if (mgr != null)
                foreach (var z in mgr.All)
                {
                    if (z == null || !z.InUse || z.Health.IsDead) continue;
                    Vector3 zc = z.transform.position + Vector3.up;
                    float d = Vector3.Distance(center, zc);
                    if (d >= radius) continue;
                    bool blocked = Physics.Linecast(center, zc, solid, QueryTriggerInteraction.Ignore);
                    float dmg = GrenadeRules.Damage(d, blocked, GrenadeRules.TypeFactor(z.Type != null ? z.Type.id : "", z.Type != null && z.Type.isBoss));
                    Vector3 dir = z.transform.position - center; dir.y = 0f; dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
                    float push = GrenadeRules.Push(d, blocked) * (z.Type != null && z.Type.isBoss ? 0.2f : 1f);
                    if (push > 0f) z.Push(dir * push);
                    if (dmg <= 0f) continue;
                    z.Health.TakeDamage(new DamageInfo { Amount = dmg, Point = center, Direction = dir, Source = gameObject });
                    if (z.Health.IsDead) kills++;
                }
            Kills += kills;

            // el jugador, solo muy cerca (y también lo protege la cobertura sólida)
            if (_player != null && _playerHealth != null && !_playerHealth.IsDead)
            {
                Vector3 chest = _player.position + Vector3.up * 1.1f;
                float d = Vector3.Distance(center, chest);
                bool blocked = d > 0.5f && Physics.Linecast(center, chest, solid, QueryTriggerInteraction.Ignore);
                float dmg = GrenadeRules.PlayerDamage(d, blocked);
                Vector3 away = _player.position - center; away.y = 0f; away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back;
                if (dmg > 0f) _playerHealth.TakeDamage(new DamageInfo { Amount = dmg, Point = center, Direction = away, Source = gameObject });
                float push = GrenadeRules.Push(d, blocked) * 0.5f;
                if (push > 0f) { var motor = _player.GetComponent<SevenDays.Player.PlayerMotor>(); if (motor != null) motor.Push(away * push + Vector3.up * push * 0.2f); }
                var imp = _player.GetComponent<SevenDays.Player.PlayerImpulse>();
                if (imp != null) imp.Shake(1.2f * Mathf.Clamp01(1f - d / (radius * 4f)));
            }

            // cobertura ligera (cajas, barriles, tablones) se rompe; la sólida (muros, hormigón) no
            int n = Physics.OverlapSphereNonAlloc(center, radius * 0.6f, Buf, ~HitboxLayer.Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) { var dst = Buf[i].GetComponentInParent<World.Destructible>(); if (dst != null) dst.TrySmash(0, center, (Buf[i].transform.position - center).normalized); }

            // fuego breve
            var fire = World.FireManager.Instance != null ? World.FireManager.Instance.Spawn(FireKind.Campfire, center - Vector3.up * 0.15f) : null;
            if (fire != null) StartCoroutine(Extinguish(fire, 2.5f));
            return kills;
        }

        private static IEnumerator Extinguish(SevenDays.Look.FireEffect fx, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (World.FireManager.Instance != null) World.FireManager.Instance.Despawn(fx);
        }

        public int Airborne { get { int n = 0; foreach (var g in _pool) if (g.gameObject.activeSelf) n++; return n; } }

        private void OnDestroy() { if (Instance == this) Instance = null; if (_bounce != null) Destroy(_bounce); }
    }
}
