using System;

namespace SevenDays.Weapons
{
    /// <summary>
    /// Datos de un arma. Es serializable a JSON (JsonUtility), así que en la Fase 5 las armas
    /// generadas por la IA del backend se cargan con esta misma clase.
    /// </summary>
    [Serializable]
    public class WeaponDefinition
    {
        public string id;
        public string displayName;
        public string rarity = "common";
        public float damage = 20f;
        public float roundsPerMinute = 600f;
        public float range = 100f;
        public float reloadTime = 2f;
        public int magazineSize = 30;
        public int reserveAmmo = 120;
        public bool automatic = true;
        public float spreadHip = 2f;   // grados de dispersión sin apuntar
        public float spreadAim = 0.4f; // grados apuntando
        public float recoil = 0.8f;    // grados de retroceso de cámara por disparo
        public float[] color = { 0.15f, 0.15f, 0.17f };
        public float length = 0.7f;    // largo visual del arma
    }

    public static class DefaultWeapons
    {
        public static WeaponDefinition Rifle() => new WeaponDefinition
        {
            id = "rifle_basic", displayName = "Fusil de asalto", damage = 18f, roundsPerMinute = 600f, range = 120f,
            reloadTime = 2f, magazineSize = 30, reserveAmmo = 120, automatic = true,
            spreadHip = 2.2f, spreadAim = 0.4f, recoil = 0.7f, length = 0.8f,
        };

        public static WeaponDefinition Pistol() => new WeaponDefinition
        {
            id = "pistol_basic", displayName = "Pistola", damage = 30f, roundsPerMinute = 330f, range = 70f,
            reloadTime = 1.3f, magazineSize = 12, reserveAmmo = 60, automatic = false,
            spreadHip = 1.4f, spreadAim = 0.25f, recoil = 1.2f, length = 0.35f,
            color = new[] { 0.3f, 0.3f, 0.32f },
        };
    }
}
