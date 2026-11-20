using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Fases de un día: preparación (buscar botín, curarse) -> oleada -> resumen -> siguiente día. Victoria al terminar el día 7; derrota al morir.</summary>
    public enum DayPhase { Preparation, Wave, Summary, Victory, Defeat }

    /// <summary>Qué trae la oleada de un día: cuántos zombis, cuántos a la vez, cada cuánto aparecen y si hay jefe.</summary>
    [Serializable]
    public class DayPlan
    {
        public int day;
        public int totalZombies;      // zombis normales de la oleada (sin contar el jefe ni sus invocados)
        public int maxAlive;          // tope de zombis vivos de la oleada a la vez
        public float spawnInterval;   // segundos entre apariciones
        public float prepSeconds;     // duración de la preparación (Enter la salta)
        public string bossId;         // null si el día no tiene jefe
        public float bossAtFraction;  // el jefe sale cuando ha muerto esta fracción de la oleada
        public string[] unlocked;     // tipos que pueden salir hoy
    }

    /// <summary>Reglas fijas de los 7 días (puras y probadas): tamaño de la oleada, jefes en los días 3, 5 y 7 y tipos especiales que se desbloquean por día.</summary>
    public static class DayPlans
    {
        public const int Days = 7;
        public static readonly int[] BossDays = { 3, 5, 7 };

        /// <summary>Día en que aparece por primera vez cada tipo.</summary>
        public static int UnlockDay(string typeId)
        {
            switch (typeId)
            {
                case ZombieIds.Common: return 1;
                case ZombieIds.Runner: return 2;
                case ZombieIds.Spitter: return 3;
                case ZombieIds.Explosive: return 4;
                case ZombieIds.Armored: return 4;
                case ZombieIds.Tank: return 5;
                default: return int.MaxValue;
            }
        }

        public static string[] Unlocked(int day)
        {
            var list = new List<string>();
            foreach (var id in new[] { ZombieIds.Common, ZombieIds.Runner, ZombieIds.Tank, ZombieIds.Armored, ZombieIds.Spitter, ZombieIds.Explosive })
                if (UnlockDay(id) <= day) list.Add(id);
            return list.ToArray();
        }

        /// <summary>Tipos nuevos que aparecen justo hoy (para el aviso del HUD).</summary>
        public static string[] NewToday(int day)
        {
            var list = new List<string>();
            foreach (var id in Unlocked(day)) if (UnlockDay(id) == day) list.Add(id);
            return list.ToArray();
        }

        public static bool IsBossDay(int day) => Array.IndexOf(BossDays, day) >= 0;

        public static string BossFor(int day)
        {
            switch (day) { case 3: return ZombieIds.BossBrute; case 5: return ZombieIds.BossLauncher; case 7: return ZombieIds.BossFinal; default: return null; }
        }

        public static DayPlan For(int day, int globalCap = 24)
        {
            day = Math.Max(1, Math.Min(Days, day));
            return new DayPlan
            {
                day = day,
                totalZombies = 10 + (day - 1) * 5,                       // 10, 15, 20 ... 40
                maxAlive = Math.Max(4, Math.Min(globalCap, 6 + day * 2)), // 8 ... 20
                spawnInterval = Math.Max(0.8f, 2.2f - 0.2f * (day - 1)),
                prepSeconds = day == 1 ? 40f : 30f,
                bossId = BossFor(day),
                bossAtFraction = 0.5f,
                unlocked = Unlocked(day),
            };
        }

        /// <summary>Quita del reparto del director los tipos que aún no tocan (peso 0). Devuelve una copia; el común siempre queda.</summary>
        public static DirectorTuning ApplyUnlocks(DirectorTuning t, int day)
        {
            var c = t.Clone();
            for (int i = 0; i < c.typeIds.Length; i++) if (UnlockDay(c.typeIds[i]) > day) c.typeWeights[i] = 0f;
            int common = Array.IndexOf(c.typeIds, ZombieIds.Common);
            float sum = 0f; foreach (var w in c.typeWeights) sum += w;
            if (sum <= 0f && common >= 0) c.typeWeights[common] = 1f;
            return c;
        }
    }

    /// <summary>
    /// Cuenta de la oleada (pura): cuántos se han generado, cuándo sale el jefe y cuándo está limpia. Unity le pasa cuántos zombis de la oleada siguen vivos.
    /// </summary>
    public class WaveState
    {
        public readonly DayPlan Plan;
        public int Spawned { get; private set; }
        public bool BossSpawned { get; private set; }
        public bool BossDead { get; private set; }
        public int Killed { get; private set; }
        private float _timer;

        public WaveState(DayPlan plan) { Plan = plan; }

        public bool HasBoss => !string.IsNullOrEmpty(Plan.bossId);
        public bool AllSpawned => Spawned >= Plan.totalZombies;

        /// <summary>¿Toca generar un zombi normal ahora? (intervalo y tope de vivos).</summary>
        public bool WantsSpawn(float dt, int alive)
        {
            _timer -= dt;
            if (AllSpawned || alive >= Plan.maxAlive || _timer > 0f) return false;
            return true;
        }

        /// <summary>Unity confirma que el zombi apareció (si el pool está lleno no se llama y se reintenta).</summary>
        public void OnSpawned() { Spawned++; _timer = Plan.spawnInterval; }

        public void OnKilled() => Killed++;

        public bool WantsBoss => HasBoss && !BossSpawned && Killed >= (int)Math.Ceiling(Plan.totalZombies * Plan.bossAtFraction);
        public void OnBossSpawned() => BossSpawned = true;
        public void OnBossDead() => BossDead = true;

        /// <summary>Zombis que quedan por matar (los vivos de la oleada más los que faltan por salir, y el jefe si no ha muerto).</summary>
        public int Remaining(int alive) => Math.Max(0, Plan.totalZombies - Spawned) + alive + (HasBoss && !BossDead ? 1 : 0);   // alive no incluye al jefe

        public bool IsCleared(int alive) => AllSpawned && alive == 0 && (!HasBoss || BossDead);
    }

    /// <summary>Máquina de estados del día (pura). La usa DayLoop; las pruebas la recorren sin Unity.</summary>
    public class DayCycle
    {
        public int Day { get; private set; } = 1;
        public DayPhase Phase { get; private set; } = DayPhase.Preparation;
        public float PhaseTime { get; private set; }
        public DayPlan Plan { get; private set; }
        public event Action<DayPhase> PhaseChanged;

        public DayCycle(int startDay = 1) { Day = Math.Max(1, Math.Min(DayPlans.Days, startDay)); Plan = DayPlans.For(Day); }

        public bool IsOver => Phase == DayPhase.Victory || Phase == DayPhase.Defeat;
        public float PrepLeft => Phase == DayPhase.Preparation ? Math.Max(0f, Plan.prepSeconds - PhaseTime) : 0f;

        /// <summary>Avanza el reloj; la preparación termina sola al acabarse el tiempo.</summary>
        public void Tick(float dt)
        {
            PhaseTime += dt;
            if (Phase == DayPhase.Preparation && PhaseTime >= Plan.prepSeconds) StartWave();
        }

        public void StartWave() { if (Phase == DayPhase.Preparation) Set(DayPhase.Wave); }

        /// <summary>La oleada quedó limpia: resumen del día o victoria si era el día 7.</summary>
        public void WaveCleared() { if (Phase == DayPhase.Wave) Set(Day >= DayPlans.Days ? DayPhase.Victory : DayPhase.Summary); }

        public void PlayerDied() { if (!IsOver) Set(DayPhase.Defeat); }

        /// <summary>Del resumen al día siguiente (empieza su preparación).</summary>
        public void NextDay()
        {
            if (Phase != DayPhase.Summary) return;
            Day++;
            Plan = DayPlans.For(Day);
            Set(DayPhase.Preparation);
        }

        private void Set(DayPhase p) { Phase = p; PhaseTime = 0f; PhaseChanged?.Invoke(p); }
    }
}
