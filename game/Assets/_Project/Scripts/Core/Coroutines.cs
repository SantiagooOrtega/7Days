using System.Collections;

namespace SevenDays.Core
{
    public static class Coroutines
    {
        /// <summary>Avanza varias corrutinas a la vez (un paso por frame cada una) y termina cuando todas terminaron. Sirve para generar texturas en paralelo.</summary>
        public static IEnumerator All(params IEnumerator[] routines)
        {
            var active = new System.Collections.Generic.List<IEnumerator>(routines);
            while (active.Count > 0)
            {
                for (int i = active.Count - 1; i >= 0; i--)
                    if (!active[i].MoveNext()) active.RemoveAt(i);
                if (active.Count > 0) yield return null;
            }
        }
    }
}
