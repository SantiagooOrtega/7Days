using System;
using System.Threading.Tasks;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Net
{
    [Serializable] public class ProgressBody { public int currentDay = 1; public CombatStatsRecord stats = new CombatStatsRecord(); }
    [Serializable] public class ProgressResponse { public ProgressBody progress = new ProgressBody(); }
    /// <summary>Cuerpo de PUT /api/progress. El inventario se envía vacío mientras el guardado del inventario no esté hecho (Fase 6).</summary>
    [Serializable] public class ProgressRequest { public int currentDay = 1; public EmptyResponse inventory = new EmptyResponse(); public CombatStatsRecord stats = new CombatStatsRecord(); }

    /// <summary>
    /// Guarda y carga el progreso del jugador en el backend (GET/PUT /api/progress): por ahora el día y las estadísticas de combate (cobertura, fatiga, precisión...),
    /// que la IA director lee para sus reglas. Sin sesión (modo prueba) no hace nada; un fallo de red solo se avisa en la consola.
    /// </summary>
    public static class ProgressService
    {
        /// <summary>Estadísticas guardadas del jugador, o null si no hay sesión, hay un error o los datos no son válidos.</summary>
        public static async Task<CombatStatsRecord> LoadStats()
        {
            if (!Session.IsLoggedIn) return null;
            var res = await ApiClient.Get<ProgressResponse>("/api/progress");
            if (!res.Ok || res.Data?.progress?.stats == null) { Debug.LogWarning("[7Days] No se pudieron cargar las estadísticas: " + res.Error); return null; }
            var errors = res.Data.progress.stats.Validate();
            if (errors.Count > 0) { Debug.LogWarning("[7Days] Estadísticas guardadas inválidas, se ignoran: " + string.Join("; ", errors)); return null; }
            return res.Data.progress.stats;
        }

        public static async Task<bool> SaveStats(int day, CombatStatsRecord stats)
        {
            if (!Session.IsLoggedIn || stats == null) return false;
            var errors = stats.Validate();
            if (errors.Count > 0) { Debug.LogWarning("[7Days] Estadísticas sin guardar (inválidas): " + string.Join("; ", errors)); return false; }
            var res = await ApiClient.Put<ProgressResponse>("/api/progress", new ProgressRequest { currentDay = Mathf.Clamp(day, 1, 7), stats = stats });
            if (!res.Ok) Debug.LogWarning("[7Days] No se pudieron guardar las estadísticas: " + res.Error);
            return res.Ok;
        }
    }
}
