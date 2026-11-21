using UnityEngine;

namespace SevenDays.UI.Tactical
{
    /// <summary>Sonidos sobrios de la interfaz (generados una vez): mover (clic seco), aceptar (golpe grave) y atrás (dos clics).</summary>
    public static class UiSounds
    {
        private static AudioClip _move, _ok, _back;
        private static AudioSource _src;

        private static AudioClip Tone(string name, float f, float seconds, float decay, float noise)
        {
            const int rate = 44100; int n = (int)(rate * seconds); var d = new float[n]; var rng = new System.Random(name.Length);
            for (int i = 0; i < n; i++) { float t = i / (float)rate; d[i] = Mathf.Exp(-t * decay) * (0.5f * Mathf.Sin(2 * Mathf.PI * f * t) + noise * (float)(rng.NextDouble() * 2 - 1)); }
            var c = AudioClip.Create(name, n, 1, rate, false); c.SetData(d, 0); c.hideFlags = HideFlags.DontUnloadUnusedAsset; return c;
        }

        private static void Play(ref AudioClip clip, string name, float f, float s, float decay, float noise, float vol)
        {
            if (clip == null) clip = Tone(name, f, s, decay, noise);
            if (_src == null) { var go = new GameObject("UI audio"); Object.DontDestroyOnLoad(go); _src = go.AddComponent<AudioSource>(); _src.spatialBlend = 0f; _src.ignoreListenerPause = true; }
            _src.PlayOneShot(clip, vol);
        }

        public static void Move() => Play(ref _move, "ui_mover", 1800f, 0.03f, 140f, 0.2f, 0.25f);
        public static void Accept() => Play(ref _ok, "ui_aceptar", 140f, 0.35f, 9f, 0.05f, 0.6f);
        public static void Back() => Play(ref _back, "ui_atras", 900f, 0.06f, 60f, 0.3f, 0.3f);
    }
}
