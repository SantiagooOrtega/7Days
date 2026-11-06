using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Limpieza de listas estáticas que pueden conservar objetos ya destruidos tras recargar la escena (probada en EditMode).</summary>
    public static class StaleList
    {
        /// <summary>Quita los elementos nulos o "muertos" y devuelve cuántos quitó.</summary>
        public static int PruneDead<T>(List<T> list, Func<T, bool> isAlive)
        {
            int removed = 0;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i] == null || !isAlive(list[i])) { list.RemoveAt(i); removed++; }
            return removed;
        }
    }
}
