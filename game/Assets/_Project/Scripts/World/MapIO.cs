using System.Collections.Generic;
using UnityEngine;
using SevenDays.Look;
using SevenDays.Procedural;
using SevenDays.World.Loot;

namespace SevenDays.World
{
    /// <summary>
    /// Mapas como datos: se leen de Resources/Maps/{id}.json (los mismos JSON que guarda el backend en la tabla maps; respaldo local para jugar sin servidor).
    /// Si el JSON falta o no es válido, el mapa 1 se genera con los valores por defecto (MapDefaults), idéntico al de antes. La ambientación del mapa se aplica
    /// sobre LookSettings antes de construir nada.
    /// </summary>
    public static class MapIO
    {
        public const string ResourceFolder = "Maps/";
        public const string DefaultId = "calle_en_ruinas";

        /// <summary>Mapa que se cargará al dar Play (lo cambian el menú o el director; por defecto el mapa 1).</summary>
        public static string SelectedId = DefaultId;

        /// <summary>Los tres mapas del juego, en orden.</summary>
        public static readonly string[] KnownIds = { DefaultId, MapLibrary.NeighborhoodId, MapLibrary.ForestId };

        public static string ToJson(MapData m) => JsonUtility.ToJson(m, true);

        public static MapData FromJson(string json, out List<string> errors)
        {
            errors = new List<string>();
            MapData m = null;
            try { m = JsonUtility.FromJson<MapData>(json); } catch (System.Exception e) { errors.Add("JSON ilegible: " + e.Message); return null; }
            errors = MapKit.Validate(m);
            return errors.Count == 0 ? m : null;
        }

        /// <summary>Carga un mapa por id desde Resources/Maps; null si no existe o no es válido (se avisa en la consola).</summary>
        public static MapData LoadLocal(string id)
        {
            var ta = Resources.Load<TextAsset>(ResourceFolder + id);
            if (ta == null) return null;
            var m = FromJson(ta.text, out var errors);
            if (m == null) Debug.LogWarning("[7Days] Mapa '" + id + "' inválido, se ignora: " + string.Join("; ", errors));
            return m;
        }

        /// <summary>El mapa a jugar: el pedido (o el mapa 1); si no hay JSON válido, el mapa 1 generado con los valores por defecto.</summary>
        public static MapData Load(string id, LookSettings look)
        {
            var m = LoadLocal(string.IsNullOrEmpty(id) ? DefaultId : id);
            if (m != null) { Debug.Log("[7Days] Mapa '" + m.name + "' (" + m.id + ") cargado de Resources/" + ResourceFolder + m.id + ".json"); return m; }
            if (id != DefaultId) { var d = LoadLocal(DefaultId); if (d != null) { Debug.LogWarning("[7Days] No hay mapa '" + id + "': se usa " + d.name); return d; } }
            Debug.Log("[7Days] Sin JSON del mapa: se genera '" + DefaultId + "' con los valores por defecto");
            return MapDefaults.StreetRuins(look);
        }

        /// <summary>Pone la ambientación del mapa (niebla, luz, sol, efectos) en los ajustes de aspecto.</summary>
        public static void ApplyAmbience(MapAmbience a, LookSettings look)
        {
            if (a == null) return;
            Color C(float[] c) => new Color(c[0], c[1], c[2]);
            look.FogColor = C(a.fogColor); look.FogDensity = a.fogDensity;
            look.AmbientSky = C(a.ambientSky); look.AmbientEquator = C(a.ambientEquator); look.AmbientGround = C(a.ambientGround);
            look.SunPitch = a.sunPitch; look.SunYaw = a.sunYaw; look.SunColor = C(a.sunColor); look.SunIntensity = a.sunIntensity; look.ShadowStrength = a.shadowStrength;
            look.GlobalBrightness = a.brightness;
            var at = look.Atmosphere;
            at.Density = a.density; at.GroundFogLayers = a.groundFogLayers; at.GroundFogDensity = a.groundFogDensity; at.GroundFogAlpha = a.groundFogAlpha; at.HazeBanks = a.hazeBanks; at.SkyDim = a.skyDim;
            at.Ash = a.ash; at.SmokeColumns = a.smokeColumns; at.Smoke = a.smoke; at.Sparks = a.sparks; at.GodRays = a.godRays; at.GodRayIntensity = a.godRayIntensity;
            at.Fireflies = a.fireflies;
            at.RedFlickerLamps = a.redFlickerLamps; at.Cracks = a.cracks; at.WeedTufts = a.weedTufts; at.Puddles = a.puddles; at.BloodStains = a.bloodStains; at.SkylineBuildings = a.skylineBuildings;
        }

        /// <summary>La ambientación que corresponde a unos ajustes de aspecto (para exportar el mapa 1 con los valores actuales).</summary>
        public static MapAmbience AmbienceFrom(LookSettings look)
        {
            float[] F(Color c) => new[] { c.r, c.g, c.b };
            var at = look.Atmosphere;
            return new MapAmbience
            {
                hour = 17f, fogColor = F(look.FogColor), fogDensity = look.FogDensity, ambientSky = F(look.AmbientSky), ambientEquator = F(look.AmbientEquator), ambientGround = F(look.AmbientGround),
                sunPitch = look.SunPitch, sunYaw = look.SunYaw, sunColor = F(look.SunColor), sunIntensity = look.SunIntensity, shadowStrength = look.ShadowStrength, brightness = look.GlobalBrightness,
                density = at.Density, groundFogLayers = at.GroundFogLayers, groundFogDensity = at.GroundFogDensity, groundFogAlpha = at.GroundFogAlpha, hazeBanks = at.HazeBanks, skyDim = at.SkyDim,
                ash = at.Ash, smokeColumns = at.SmokeColumns, smoke = at.Smoke, sparks = at.Sparks, godRays = at.GodRays, godRayIntensity = at.GodRayIntensity,
                redFlickerLamps = at.RedFlickerLamps, cracks = at.Cracks, weedTufts = at.WeedTufts, puddles = at.Puddles, bloodStains = at.BloodStains, skylineBuildings = at.SkylineBuildings,
                dust = true, skyline = true, roadMarkings = true, fireflies = 0f, soundscape = "none",
            };
        }
    }

    /// <summary>Mapa 1 "Calle en ruinas" con los valores por defecto (el mismo contenido que generaba el código antes de pasar a JSON).</summary>
    public static class MapDefaults
    {
        public static MapData StreetRuins(LookSettings look)
        {
            var house = HouseIO.Load();
            var vegetation = Vegetation.BuildLayout(look);   // árboles, arbustos, coches, señales... con las semillas de LookSettings
            var exclusions = IndustrialRuins.Exclusions();
            var m = new MapData
            {
                id = MapIO.DefaultId, name = "Calle en ruinas", baseDifficulty = 1, halfSize = 30f, seed = 42, playerStart = new[] { 0f, 0.1f, -14f, 0f },
                props = MapKit.StreetRuinsProps(), houses = new[] { house }, vegetation = vegetation,
                loot = LootIO.Load(vegetation.items, house),
                ambience = MapIO.AmbienceFrom(look),
            };
            // spawns: 3 por lado a 2 m del muro, apartados de lo que bloquee el paso (contenedores, nave)
            m.spawns = MapKit.EdgeSpawns(30f, 3, 2f, (x, z) => { foreach (var zn in exclusions) if (zn.Contains(x, z, 1.5f)) return true; return false; });
            return m;
        }
    }
}
