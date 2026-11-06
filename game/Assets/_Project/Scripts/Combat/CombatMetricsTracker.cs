using UnityEngine;
using SevenDays.Core;
using SevenDays.Player;
using SevenDays.Procedural;

namespace SevenDays.Combat
{
    /// <summary>
    /// Conecta los eventos de combate de Unity con CombatMetrics (lógica pura) para que la IA director (Fase 4) lea el rendimiento del jugador:
    /// precisión, headshots, tiempo para matar, daño recibido por tipo y tiempo en cobertura. F4 muestra un panel de depuración.
    /// </summary>
    public class CombatMetricsTracker : MonoBehaviour
    {
        public static CombatMetricsTracker Instance { get; private set; }
        public CombatMetrics Metrics { get; } = new CombatMetrics();
        /// <summary>Lo que lee el director: métricas del día actual.</summary>
        public CombatSnapshot Snapshot => Metrics.Snapshot();

        private PlayerShooting _shooting;
        private PlayerCover _cover;
        private Health _playerHealth;
        private bool _headThisShot, _showPanel;
        private GUIStyle _style;

        public static CombatMetricsTracker Create(Transform parent, GameObject player, ZombieManager zombies)
        {
            var go = new GameObject("CombatMetrics");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<CombatMetricsTracker>();
            t.Init(player, zombies);
            return t;
        }

        private void Init(GameObject player, ZombieManager zombies)
        {
            Instance = this;
            _shooting = player.GetComponent<PlayerShooting>();
            _cover = player.GetComponent<PlayerCover>();
            _playerHealth = player.GetComponent<Health>();
            _shooting.ShotFired += Metrics.RecordShot;
            _shooting.ShotHitReported += OnShotHit;
            _shooting.HitConfirmed += OnHitConfirmed;
            _playerHealth.Damaged += OnPlayerDamaged;
            zombies.ZombieKilled += OnKilled;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_shooting != null) { _shooting.ShotFired -= Metrics.RecordShot; _shooting.ShotHitReported -= OnShotHit; _shooting.HitConfirmed -= OnHitConfirmed; }
            if (_playerHealth != null) _playerHealth.Damaged -= OnPlayerDamaged;
        }

        private void OnShotHit(ShotHit s) { if (s.Report.Headshot) _headThisShot = true; }

        private void OnHitConfirmed() { Metrics.RecordHit(_headThisShot); _headThisShot = false; }

        private void OnPlayerDamaged(DamageInfo info)
        {
            string source = "other";
            if (info.Source != null)
            {
                var z = info.Source.GetComponent<ZombieController>();
                if (z != null && z.Type != null) source = z.Type.id;
            }
            Metrics.RecordDamageTaken(source, info.Amount);
        }

        private void OnKilled(ZombieController z) => Metrics.RecordKill(z.DiedByHeadshot, z.TimeToKill);

        private void Update()
        {
            Metrics.Tick(Time.deltaTime, _cover != null && _cover.Active);
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k != null && k.f4Key.wasPressedThisFrame) _showPanel = !_showPanel;
        }

        private void OnGUI()
        {
            if (!_showPanel) return;
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
            var s = Snapshot;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("MÉTRICAS DEL DIRECTOR (F4)");
            sb.AppendLine("precisión " + (s.accuracy * 100f).ToString("0") + "%  (" + s.shotsHit + "/" + s.shotsFired + ")");
            sb.AppendLine("headshots " + s.headshots + "  (" + (s.headshotRatio * 100f).ToString("0") + "% de aciertos)");
            sb.AppendLine("muertes " + s.kills + "  headshot " + s.headshotKills + "  TTK medio " + s.averageTimeToKill.ToString("0.0") + " s");
            sb.AppendLine("en cobertura " + s.timeInCover.ToString("0") + " s  (" + (s.coverRatio * 100f).ToString("0") + "%)");
            sb.AppendLine("daño recibido " + s.damageTaken.ToString("0"));
            for (int i = 0; i < s.damageSources.Length; i++) sb.AppendLine("   " + s.damageSources[i] + ": " + s.damageAmounts[i].ToString("0"));
            var mgr = ZombieManager.Instance;
            if (mgr != null)
            {
                var t = mgr.Tuning;
                sb.AppendLine("parámetros: vida x" + t.healthMultiplier.ToString("0.00") + "  vel x" + t.speedMultiplier.ToString("0.00") + "  daño x" + t.damageMultiplier.ToString("0.00")
                    + "  jefe x" + t.bossAggressiveness.ToString("0.00") + "  especiales x" + t.specialFrequency.ToString("0.00"));
                sb.AppendLine("zombis activos " + mgr.ActiveCount + "/" + mgr.MaxZombies);
            }
            GUI.Box(new Rect(10, 120, 470, 22f * (6 + s.damageSources.Length + 3)), GUIContent.none);
            GUI.Label(new Rect(18, 124, 460, 22f * (6 + s.damageSources.Length + 3)), sb.ToString(), _style);
        }
    }
}
