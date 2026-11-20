using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using SevenDays.Core;
using SevenDays.Player;
using SevenDays.Procedural;

namespace SevenDays.World.Loot
{
    /// <summary>
    /// Aplica la economía de munición (AmmoEconomy, pura) en la partida: al empezar cada día calcula cuánto falta para el presupuesto, lo reparte en tandas lentas
    /// durante la oleada (en puntos del mapa que no se hayan usado en 60 s) y como crédito que sueltan los zombis al morir; y si el jugador pasa 90 s sin munición
    /// en ninguna arma, deja un paquete cerca (pity timer). Todo respeta el tope de pickups activos (lo que no cabe se queda como crédito).
    /// </summary>
    public class AmmoSupply : MonoBehaviour
    {
        public static AmmoSupply Instance { get; private set; }
        public DayAmmo Today { get; private set; }
        public double Credit { get; private set; }
        public int RoundsGiven { get; private set; }
        public AmmoPity Pity { get; } = new AmmoPity();

        private LootManager _loot;
        private PlayerInventory _inventory;
        private Transform _player;
        private readonly Dictionary<string, float> _lastUse = new Dictionary<string, float>();
        private double _killCreditEach, _trickleEach;
        private int _batchesLeft;
        private float _nextBatch;
        private bool _wave;

        public static AmmoSupply Create(Transform parent, LootManager loot, GameObject player)
        {
            var go = new GameObject("AmmoSupply");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AmmoSupply>();
            a._loot = loot; a._player = player.transform; a._inventory = player.GetComponent<PlayerInventory>();
            loot.State.RespawnScale = AmmoEconomy.RefillScale;   // relleno entre días: más cantidad y más veces por punto
            loot.State.ExtraRespawns = AmmoEconomy.ExtraRespawns;
            Instance = a;
            return a;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Empieza un día con el relleno que ya hubo (balas esperadas).</summary>
        public void BeginDay(int day, double refillRounds)
        {
            Today = AmmoEconomy.Plan(_loot.Layout, day, refillRounds);
            _killCreditEach = Today.Zombies > 0 ? Today.KillCredit / Today.Zombies : 0;
            _trickleEach = Today.Trickle / AmmoEconomy.TrickleBatches;
            _batchesLeft = AmmoEconomy.TrickleBatches;
            _wave = false;
            Debug.Log("[7Days] Munición día " + day + ": presupuesto " + Today.Budget + " balas para " + Today.Zombies + " zombis (relleno " + Today.Refill.ToString("0") + ", drops " + Today.TableDrops.ToString("0") + ", tandas " + Today.Trickle.ToString("0") + ", por bajas " + Today.KillCredit.ToString("0") + ").");
        }

        public void BeginWave() { _wave = true; _nextBatch = Time.time + AmmoEconomy.TrickleFirstSeconds; }

        /// <summary>Oleada limpia: las tandas que no salieron aparecen ya (para la preparación) y el crédito sobrante se suelta en un punto.</summary>
        public void EndWave()
        {
            _wave = false;
            while (_batchesLeft > 0) { _batchesLeft--; Credit += _trickleEach; }
            while (Credit >= 1.0 && TrySpawnAtPoint()) { }
        }

        /// <summary>Un zombi de la oleada murió: suma su crédito y, si llega para un paquete, lo suelta donde cayó.</summary>
        public void OnKill(Vector3 at)
        {
            Credit += _killCreditEach;
            var t = PickType();
            int pack = AmmoEconomy.PackSize(t);
            if (Credit < pack) return;
            if (_loot.Spawn(LootItem.Ammo(t, pack), new Vector3(at.x + 0.4f, 0.35f, at.z), null) != null) { Credit -= pack; RoundsGiven += pack; }
        }

        private void Update()
        {
            if (_inventory == null) return;
            if (_wave && _batchesLeft > 0 && Time.time >= _nextBatch)
            {
                _batchesLeft--;
                _nextBatch = Time.time + AmmoEconomy.TrickleIntervalSeconds;
                Credit += _trickleEach;
                TrySpawnAtPoint();
            }
            // pity timer: nunca sin forma de conseguir munición
            if (Pity.Tick(Time.deltaTime, _inventory.HasAnyAmmo)) PityDrop();
        }

        private AmmoType PickType()
        {
            var owned = new List<AmmoType>();
            foreach (var w in _inventory.Weapons) if (w.Data.UsesAmmo) owned.Add(w.Data.ammoType);
            return AmmoEconomy.PickType(owned, _inventory.Ammo.Count, _inventory.Ammo.Max);
        }

        /// <summary>Paquete en un punto del mapa al azar que no se haya usado en los últimos 60 s y a más de 8 m del jugador.</summary>
        private bool TrySpawnAtPoint()
        {
            var t = PickType();
            int pack = Mathf.Min(AmmoEconomy.PackSize(t), Mathf.Max(1, (int)Credit));
            var pts = _loot.Points;
            if (pts == null || pts.Count == 0 || Credit < 1.0) return false;
            int start = Random.Range(0, pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[(start + i) % pts.Count];
                if (_lastUse.TryGetValue(p.id, out float used) && Time.time - used < AmmoEconomy.PointCooldownSeconds) continue;
                var pos = new Vector3(p.x, 0.35f, p.z);
                if ((pos - _player.position).sqrMagnitude < 64f) continue;
                if (_loot.Spawn(LootItem.Ammo(t, pack), pos, null) == null) return false;   // suelo lleno: queda como crédito
                _lastUse[p.id] = Time.time;
                Credit -= pack; RoundsGiven += pack;
                if ((pos - _player.position).sqrMagnitude < 35f * 35f) SevenDays.Vigia.VigiaVoice.Say("suministro_cercano");
                return true;
            }
            return false;
        }

        private void PityDrop()
        {
            var t = PickType();
            Vector3 at = _player.position + _player.forward * 3f;
            if (NavMesh.SamplePosition(at, out var hit, 3f, NavMesh.AllAreas)) at = hit.position;
            int pack = AmmoEconomy.PackSize(t) * 2;
            if (_loot.SpawnForced(LootItem.Ammo(t, pack), new Vector3(at.x, 0.35f, at.z)) != null)
            {
                RoundsGiven += pack;
                HudMessages.Add("Encontraste munición cerca", new Color(1f, 0.85f, 0.3f), 3.5f);
            }
        }
    }
}
