using UnityEngine;
using SevenDays.Core;
using SevenDays.Combat;
using SevenDays.Player;
using SevenDays.Procedural;

namespace SevenDays.Vigia
{
    /// <summary>
    /// Qué anuncia VIGÍA durante la partida (lo de días y oleadas lo pide DayLoop directamente): bajas (combos, rachas, headshots, distancia, cuerpo a cuerpo,
    /// explosiones en cadena, primera sangre, hitos), recursos (cargador vacío, munición baja o agotada, vida baja o crítica, botiquín) y amenazas (jefe en furia,
    /// explosivos, lanzadores y tanques cerca, flanqueo, rodeado). Las amenazas se miran cada 0.5 s. También decide si el combate es "intenso".
    /// </summary>
    public class VigiaEvents : MonoBehaviour
    {
        public static VigiaEvents Instance { get; private set; }
        public ComboTracker Combo { get; } = new ComboTracker();
        public VigiaProgress Progress { get; } = new VigiaProgress();
        public int TotalKills { get; private set; }
        public const float FarKill = 28f, IntenseRadius = 12f, SurroundRadius = 4f;
        public const int IntenseCount = 6, SurroundCount = 5;

        private GameObject _player;
        private Health _health;
        private PlayerShooting _shooting;
        private PlayerInventory _inventory;
        private PlayerHealing _healing;
        private ZombieManager _zombies;
        private bool _dayFirstKill = true, _wasHealing, _hadAmmo = true, _magWasEmpty, _lowAmmo, _rageSaid;
        private int _healthBand;   // 0 bien, 1 baja, 2 crítica
        private float _nextScan, _lastExplosiveDeath = -10f;
        private int _killsAfterExplosion, _emptyNearZombies;

        public static VigiaEvents Create(Transform parent, GameObject player, ZombieManager zombies)
        {
            var go = new GameObject("VIGIA Events");
            go.transform.SetParent(parent, false);
            var e = go.AddComponent<VigiaEvents>();
            e._player = player; e._zombies = zombies;
            e._health = player.GetComponent<Health>();
            e._shooting = player.GetComponent<PlayerShooting>();
            e._inventory = player.GetComponent<PlayerInventory>();
            e._healing = player.GetComponent<PlayerHealing>();
            zombies.ZombieKilled += e.OnKilled;
            Instance = e;
            return e;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_zombies != null) _zombies.ZombieKilled -= OnKilled;
        }

        /// <summary>Lo llama DayLoop al empezar cada oleada.</summary>
        public void NewWave() { _dayFirstKill = true; Progress.NewWave(); Combo.Reset(); _rageSaid = false; }

        private void OnKilled(ZombieController z)
        {
            TotalKills++;
            double now = Time.timeAsDouble;
            float dist = _player != null ? Vector3.Distance(z.transform.position, _player.transform.position) : 0f;
            string combo = Combo.OnKill(now, z.DiedByHeadshot);
            if (z.Type != null && z.Type.id == ZombieIds.Explosive) { _lastExplosiveDeath = Time.time; _killsAfterExplosion = 0; }
            else if (Time.time - _lastExplosiveDeath < 0.6f && ++_killsAfterExplosion == 2) combo = "explosion_cadena";
            if (combo == null)
            {
                if (_dayFirstKill) combo = "primera_sangre";
                else if (dist >= FarKill) combo = "baja_distancia";
                else if (_shooting != null && _shooting.Current.Data.IsMelee && dist < 3f) combo = "cuerpo_a_cuerpo";
            }
            _dayFirstKill = false;
            VigiaVoice.Say(combo);
            VigiaVoice.Say(Progress.OnTotalKills(TotalKills));
        }

        /// <summary>Zombis restantes de la oleada (lo llama DayLoop tras cada baja).</summary>
        public void OnRemaining(int remaining, int total) => VigiaVoice.Say(Progress.OnRemaining(remaining, total));

        private void Update() { SevenDays.Core.PerfProbe.Begin(SevenDays.Core.PerfSection.Vigia); UpdateMedido(); SevenDays.Core.PerfProbe.End(); }


        private void UpdateMedido()
        {
            if (_player == null || _health == null || _health.IsDead) return;
            Resources_();
            if (Time.time >= _nextScan) { _nextScan = Time.time + 0.5f; Threats(); }
        }

        private void Resources_()
        {
            // vida
            float hp = _health.Current / Mathf.Max(1f, _health.Max);
            int band = hp < 0.2f ? 2 : (hp < 0.4f ? 1 : 0);
            if (band > _healthBand) { VigiaVoice.Say(band == 2 ? "estado_critico" : "vida_baja"); _healthBand = band; }
            else if (hp >= 0.5f) _healthBand = 0;   // se curó: los avisos vuelven a valer
            // botiquín terminado
            if (_healing != null) { if (_wasHealing && !_healing.Healing && hp > 0.3f) VigiaVoice.Say("botiquin_usado"); _wasHealing = _healing.Healing; }
            // munición
            if (_inventory != null && _shooting != null)
            {
                bool any = _inventory.HasAnyAmmo;
                if (_hadAmmo && !any) VigiaVoice.Say("sin_municion");
                _hadAmmo = any;
                var cur = _shooting.Current;
                if (cur.Data.UsesAmmo)
                {
                    bool empty = cur.Magazine == 0;
                    int reserve = _inventory.Reserve(cur);
                    if (empty && !_magWasEmpty && reserve > 0)
                    {
                        VigiaVoice.Say(_emptyNearZombies >= 2 ? "consejo_recarga" : "cargador_vacio");
                        if (CountNear(8f) > 0) _emptyNearZombies++;
                    }
                    _magWasEmpty = empty;
                    bool low = any && cur.Magazine + reserve <= cur.Data.magazineSize && cur.Magazine + reserve > 0;
                    if (low && !_lowAmmo) VigiaVoice.Say("municion_baja");
                    _lowAmmo = low;
                }
            }
        }

        private int CountNear(float r)
        {
            int n = 0; Vector3 p = _player.transform.position;
            foreach (var z in _zombies.All) if (z.InUse && !z.Health.IsDead && (z.transform.position - p).sqrMagnitude < r * r) n++;
            return n;
        }

        private void Threats()
        {
            Vector3 p = _player.transform.position;
            var cam = Camera.main;
            Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : _player.transform.forward;
            int close = 0, intense = 0, behind = 0;
            var all = _zombies.All;
            for (int zi = 0; zi < all.Count; zi++)
            {
                var z = all[zi];
                if (!z.InUse || z.Health.IsDead || z.Type == null) continue;
                Vector3 d = z.transform.position - p; d.y = 0f;
                float dist = d.magnitude;
                if (dist < IntenseRadius) intense++;
                if (dist < SurroundRadius) close++;
                if (dist < 6f && Vector3.Dot(d.normalized, fwd) < -0.4f) behind++;
                if (z.Type.id == ZombieIds.Explosive && dist < 10f) VigiaVoice.Say("explosivo_cerca");
                else if (z.Type.id == ZombieIds.Spitter && dist < 20f && z.State == ZombieState.Chase) VigiaVoice.Say("lanzador_cerca");
                else if (z.Type.id == ZombieIds.Tank && dist < 30f) VigiaVoice.Say("tanque");
            }
            var boss = _zombies.ActiveBoss;
            if (boss != null && boss.InUse && !boss.Health.IsDead && boss.Boss != null && boss.Boss.Raging && !_rageSaid) { _rageSaid = true; VigiaVoice.Say("jefe_furia"); }
            if (VigiaVoice.Instance != null) VigiaVoice.Instance.Intense = intense >= IntenseCount;
            if (close >= SurroundCount) VigiaVoice.Say("rodeado");
            else if (behind > 0) VigiaVoice.Say("flanqueo");
        }

        /// <summary>Consejos al cerrar el día, según sus estadísticas.</summary>
        public static void DayTips(DayStats d)
        {
            if (d == null) return;
            if (d.shotsFired >= 30 && d.Accuracy < 0.25f) VigiaVoice.Say("consejo_precision");
            else if (d.CoverRatio > 0.7f) VigiaVoice.Say("consejo_cobertura");
        }
    }
}
