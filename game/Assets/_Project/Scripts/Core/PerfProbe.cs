using System.Diagnostics;

namespace SevenDays.Core
{
    public enum PerfSection { IA, Animacion, Hitbox, Audio, Vigia, UI, Loot, Otros }

    /// <summary>
    /// Cronómetro por sistema para el panel CPU (F6) y las pruebas de rendimiento: Begin/End anidables (cada sección cuenta su tiempo EXCLUSIVO: la animación
    /// llamada desde la IA no se suma a la IA). Sin memoria por frame: acumuladores fijos; el frame se cierra solo al cambiar Time.frameCount.
    /// </summary>
    public static class PerfProbe
    {
        public static readonly int Count = System.Enum.GetValues(typeof(PerfSection)).Length;
        public static readonly string[] Names = { "IA / NavMesh (C#)", "Animación procedural", "Hitboxes", "Audio", "VIGÍA", "UI / IMGUI (C#)", "Loot y marcadores", "Otros" };
        private static readonly long[] Acc = new long[8], Last = new long[8], AccB = new long[8], LastB = new long[8];
        private static readonly int[] StackSec = new int[32];
        private static readonly long[] StackStart = new long[32], StackChild = new long[32], StackB = new long[32], StackChildB = new long[32];
        private static int _depth, _frame = -1;
        public static bool Enabled = true;
        private static readonly double TickMs = 1000.0 / Stopwatch.Frequency;

        private static void Roll()
        {
            int f = UnityEngine.Time.frameCount;
            if (f == _frame) return;
            _frame = f;
            for (int i = 0; i < Count; i++) { Last[i] = Acc[i]; Acc[i] = 0; LastB[i] = AccB[i]; AccB[i] = 0; }
        }

        public static void Begin(PerfSection s)
        {
            if (!Enabled || _depth >= StackSec.Length) { _depth++; return; }
            Roll();
            StackSec[_depth] = (int)s; StackB[_depth] = System.GC.GetTotalMemory(false); StackChildB[_depth] = 0; StackStart[_depth] = Stopwatch.GetTimestamp(); StackChild[_depth] = 0; _depth++;
        }

        public static void End()
        {
            if (_depth <= 0) return;
            _depth--;
            if (!Enabled || _depth >= StackSec.Length) return;
            long el = Stopwatch.GetTimestamp() - StackStart[_depth];
            long b = System.GC.GetTotalMemory(false) - StackB[_depth]; if (b < 0) b = 0;   // una recolección en medio: no cuenta
            Acc[StackSec[_depth]] += el - StackChild[_depth];
            AccB[StackSec[_depth]] += b - StackChildB[_depth];
            if (_depth > 0) { StackChild[_depth - 1] += el; StackChildB[_depth - 1] += b; }
        }

        /// <summary>Milisegundos del último frame completo de una sección.</summary>
        public static float LastMs(PerfSection s) { Roll(); return (float)(Last[(int)s] * TickMs); }
        public static float LastMs(int s) { Roll(); return (float)(Last[s] * TickMs); }

        /// <summary>Bytes de memoria gestionada reservados por una sección en el último frame (basura que acabará en una recolección).</summary>
        public static long LastBytes(int s) { Roll(); return LastB[s]; }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { _depth = 0; _frame = -1; for (int i = 0; i < Acc.Length; i++) Acc[i] = Last[i] = 0; }
    }
}
