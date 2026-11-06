using System.Collections.Generic;
using UnityEngine;

namespace SevenDays.Core
{
    /// <summary>Avisos cortos para el HUD ("+6 balas de pistola", "Munición llena"...). Se muestran unos segundos y desaparecen.</summary>
    public static class HudMessages
    {
        public struct Message { public string Text; public Color Color; public float Until; }

        private static readonly List<Message> Items = new List<Message>();
        public static IReadOnlyList<Message> Current { get { Prune(); return Items; } }

        public static void Add(string text, Color? color = null, float seconds = 2.6f)
        {
            Prune();
            if (Items.Count > 0 && Items[Items.Count - 1].Text == text) return; // no repetir el mismo aviso seguido
            Items.Add(new Message { Text = text, Color = color ?? Color.white, Until = Time.unscaledTime + seconds });
            if (Items.Count > 5) Items.RemoveAt(0);
        }

        public static void Clear() => Items.Clear();

        private static void Prune() => Items.RemoveAll(m => Time.unscaledTime > m.Until);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Items.Clear();
    }
}
