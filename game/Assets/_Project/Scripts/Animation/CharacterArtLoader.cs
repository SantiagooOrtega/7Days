using UnityEngine;

namespace SevenDays.Animation
{
    /// <summary>Pone en los materiales las texturas que van terminando los hilos (hilo principal, una vez por frame).</summary>
    public class CharacterArtLoader : MonoBehaviour
    {
        private static CharacterArtLoader _instance;

        public static void Ensure()
        {
            if (_instance != null || !Application.isPlaying) return;
            var go = new GameObject("CharacterArtLoader") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CharacterArtLoader>();
        }

        private void Update() { CharacterArt.Poll(); ZombieArt.Poll(); ZombieMeshes.Pump(); SevenDays.Combat.ZombieSoundBank.Pump(); }
    }
}
