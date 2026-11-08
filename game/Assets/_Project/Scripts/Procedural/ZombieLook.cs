using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    public enum ZombieHeadStyle { Intact, Sunken, JawBroken, JawExposed }
    public enum ZombieHairStyle { Bald, Tufts, Matted }
    public enum ZombieClothing { Shirt, Military, LabCoat, Suit, Workwear, Rags }
    public enum ZombieFootwear { Bare, BrokenBoots }
    /// <summary>Protección improvisada del blindado: chaleco con placas, puerta atada, neumáticos o chatarra mezclada.</summary>
    public enum ZombieArmor { None, VestPlates, DoorShield, TireRings, MixedScrap }

    /// <summary>
    /// Una variante visual de un tipo de zombi (pura y serializable con JsonUtility; la valida <see cref="ZombieLookValidator"/> y el esquema zod de docs/ZOMBIES.md,
    /// así el backend o el contenido offline pueden definir variantes nuevas). Colores en sRGB 0..1 como [r, g, b]. Los enums se guardan como enteros.
    /// </summary>
    [Serializable]
    public class ZombieVariantData
    {
        public string id = "common_shirt";
        public string typeId = ZombieIds.Common;
        public string displayName = "Zombi con camisa";
        public ZombieHeadStyle head = ZombieHeadStyle.Intact;
        public ZombieHairStyle hair = ZombieHairStyle.Tufts;
        public ZombieClothing clothing = ZombieClothing.Shirt;
        public ZombieFootwear footwear = ZombieFootwear.Bare;
        public ZombieArmor armor = ZombieArmor.None;
        public int seed = 1;
        public float[] skinTone = { 1f, 1f, 1f };          // multiplica el color de piel de la textura del tipo
        public float[] clothTone = { 1f, 1f, 1f };         // multiplica el color de la ropa
        public float[] hairColor = { 0.12f, 0.1f, 0.08f };
        public float[] eyeTint = { 0.75f, 0.8f, 0.6f };    // brillo tenue del ojo con velo lechoso
        public float eyeGlow = 0.18f;                      // 0..1
        public float grime = 0.5f;                         // 0..1 suciedad general (oscurece la ropa)
        public float bloodiness = 0.4f;                    // 0..1 cuántas manchas de sangre del repertorio se ven desde el principio

        public ZombieVariantData Clone() => (ZombieVariantData)MemberwiseClone();
    }

    [Serializable]
    public class ZombieVariantFile
    {
        public List<ZombieVariantData> variants = new List<ZombieVariantData>();
    }

    /// <summary>
    /// Anatomía de cada tipo: proporciones del esqueleto (el mismo ProceduralRig del soldado, con otras medidas) y rasgos del modelo. Las medidas en metros ya incluyen
    /// la escala del tipo (el tanque mide ~2.6 m), así el rig no necesita escala: sus zancadas, rodillas y pies se calculan con las piernas reales.
    /// </summary>
    public class ZombieAnatomy
    {
        public string TypeId;
        public RigProportions Proportions;
        public float Emaciation;          // 0..1: delgadez; con > 0.45 se marcan costillas, clavículas y columna
        public bool VisibleRibs => Emaciation > 0.45f;
        public float BigArmScale = 1f;    // 1 = simétrico; > 1 el brazo derecho (BigArmSide) es mucho más grande
        public int BigArmSide = 1;        // +1 derecho, -1 izquierdo
        public float LongArmExtra;        // metros de antebrazo y mano de más (lanzador)
        public bool ScarPlates;           // placas de tejido cicatrizado (tanque)
        public bool Bags;                 // bultos y bolsas en espalda y hombros (lanzador)
        public bool Belly;                // abdomen hinchado con pústulas y venas (explosivo)
        public float SkinTension = 0.5f;  // 0 piel floja, 1 tensa (corredor)
        public float[] SkinBase = { 0.62f, 0.66f, 0.6f };   // color base de la piel del tipo (sRGB)

        /// <summary>Altura del soldado de referencia: todas las medidas absolutas del modelo se escalan por height / ReferenceHeight.</summary>
        public const float ReferenceHeight = 1.82f;
        public float Size => Proportions.height / ReferenceHeight;
    }

    public static class ZombieAnatomies
    {
        /// <summary>Tres cubos de altura (-6 %, 0, +6 %): cada cuerpo del pool nace con uno al azar, así los zombis no miden todos igual.</summary>
        public static readonly float[] ScaleBuckets = { 0.94f, 1f, 1.06f };

        private static RigProportions P(float h, float shoulder, float hip, float bulk, float legRatio, float arm, float neck, float head, float hunch, float depth, float limb)
            => new RigProportions { height = h, shoulderWidth = shoulder, hipWidth = hip, bulk = bulk, legRatio = legRatio, armRatio = arm, neckRatio = neck, headSize = head, hunch = hunch, torsoDepth = depth, limbThickness = limb };

        public static ZombieAnatomy For(string typeId, int bucket = 1)
        {
            var a = Base(typeId);
            float b = ScaleBuckets[Math.Max(0, Math.Min(ScaleBuckets.Length - 1, bucket))];
            a.Proportions.height *= b; a.Proportions.shoulderWidth *= b; a.Proportions.hipWidth *= b;
            a.Proportions.Validate();
            return a;
        }

        private static ZombieAnatomy Base(string id)
        {
            switch (id)
            {
                case ZombieIds.Runner:
                    return new ZombieAnatomy { TypeId = id, Proportions = P(1.74f, 0.40f, 0.245f, 0.80f, 0.52f, 1.14f, 1.05f, 0.96f, 0.6f, 0.85f, 0.78f), Emaciation = 0.8f, SkinTension = 0.9f, SkinBase = new[] { 0.66f, 0.64f, 0.55f } };
                case ZombieIds.Tank:
                    return new ZombieAnatomy { TypeId = id, Proportions = P(2.64f, 0.80f, 0.42f, 1.3f, 0.46f, 1.1f, 0.6f, 0.78f, 0.4f, 1.1f, 1.25f), Emaciation = 0f, BigArmScale = 1.55f, BigArmSide = 1, ScarPlates = true, SkinTension = 0.7f, SkinBase = new[] { 0.5f, 0.55f, 0.48f } };
                case ZombieIds.Armored:
                    return new ZombieAnatomy { TypeId = id, Proportions = P(1.82f, 0.50f, 0.29f, 1.1f, 0.5f, 1f, 1f, 1f, 0.12f, 1f, 1f), Emaciation = 0.1f, SkinBase = new[] { 0.62f, 0.64f, 0.6f } };
                case ZombieIds.Spitter:
                    return new ZombieAnatomy { TypeId = id, Proportions = P(1.76f, 0.46f, 0.27f, 0.95f, 0.5f, 1.06f, 1f, 1f, 0.34f, 0.95f, 0.9f), Emaciation = 0.35f, LongArmExtra = 0.12f, Bags = true, SkinBase = new[] { 0.58f, 0.66f, 0.46f } };
                case ZombieIds.Explosive:
                    return new ZombieAnatomy { TypeId = id, Proportions = P(1.72f, 0.46f, 0.30f, 1.0f, 0.5f, 1f, 0.9f, 0.95f, 0.22f, 1.05f, 0.95f), Emaciation = 0.15f, Belly = true, SkinBase = new[] { 0.72f, 0.58f, 0.46f } };
                default:
                    return new ZombieAnatomy { TypeId = ZombieIds.Common, Proportions = P(1.78f, 0.44f, 0.27f, 0.98f, 0.5f, 1f, 1f, 1f, 0.28f, 0.9f, 0.92f), Emaciation = 0.3f, SkinTension = 0.35f, SkinBase = new[] { 0.64f, 0.67f, 0.6f } };
            }
        }

        /// <summary>Los seis tipos que usan el cuerpo procedural (los jefes del bloque D tendrán el suyo).</summary>
        public static readonly string[] Types = { ZombieIds.Common, ZombieIds.Runner, ZombieIds.Tank, ZombieIds.Armored, ZombieIds.Spitter, ZombieIds.Explosive };

        public static bool Has(string typeId) => Array.IndexOf(Types, typeId) >= 0;
    }

    public static class ZombieVariants
    {
        private static ZombieVariantData V(string id, string type, string name, ZombieHeadStyle head, ZombieHairStyle hair, ZombieClothing cloth, ZombieFootwear feet, int seed,
            float[] skin = null, float[] clothTone = null, float[] hairColor = null, ZombieArmor armor = ZombieArmor.None, float grime = 0.5f, float blood = 0.4f, float eyeGlow = 0.18f, float[] eye = null)
        {
            var v = new ZombieVariantData { id = id, typeId = type, displayName = name, head = head, hair = hair, clothing = cloth, footwear = feet, seed = seed, armor = armor, grime = grime, bloodiness = blood, eyeGlow = eyeGlow };
            if (skin != null) v.skinTone = skin;
            if (clothTone != null) v.clothTone = clothTone;
            if (hairColor != null) v.hairColor = hairColor;
            if (eye != null) v.eyeTint = eye;
            return v;
        }

        /// <summary>3 a 4 variantes por tipo (cabeza, pelo, mandíbula, ropa y paleta distintos). Los ScriptableObject o el JSON de Resources/Zombies/zombie_variants.json las pueden sustituir.</summary>
        public static List<ZombieVariantData> Defaults()
        {
            var H = ZombieHeadStyle.Intact; var S = ZombieHeadStyle.Sunken; var JB = ZombieHeadStyle.JawBroken; var JE = ZombieHeadStyle.JawExposed;
            var Bald = ZombieHairStyle.Bald; var Tufts = ZombieHairStyle.Tufts; var Matted = ZombieHairStyle.Matted;
            var bare = ZombieFootwear.Bare; var boots = ZombieFootwear.BrokenBoots;
            return new List<ZombieVariantData>
            {
                V("common_shirt", ZombieIds.Common, "Zombi con camisa", H, Tufts, ZombieClothing.Shirt, bare, 11, clothTone: new[] { 0.95f, 0.9f, 0.85f }, hairColor: new[] { 0.14f, 0.11f, 0.08f }),
                V("common_lab", ZombieIds.Common, "Zombi con bata", JB, Bald, ZombieClothing.LabCoat, bare, 12, skin: new[] { 0.95f, 1f, 0.95f }, blood: 0.7f, eyeGlow: 0.22f),
                V("common_suit", ZombieIds.Common, "Zombi con traje", S, Matted, ZombieClothing.Suit, boots, 13, hairColor: new[] { 0.2f, 0.19f, 0.17f }, grime: 0.7f),
                V("common_work", ZombieIds.Common, "Zombi de obra", JE, Tufts, ZombieClothing.Workwear, boots, 14, skin: new[] { 1f, 0.95f, 0.92f }, blood: 0.6f, grime: 0.8f),

                V("runner_rags", ZombieIds.Runner, "Corredor en harapos", S, Matted, ZombieClothing.Rags, bare, 21, skin: new[] { 1.02f, 0.98f, 0.9f }, hairColor: new[] { 0.08f, 0.07f, 0.06f }, eyeGlow: 0.3f, eye: new[] { 0.9f, 0.55f, 0.3f }),
                V("runner_shirt", ZombieIds.Runner, "Corredor sin mangas", JB, Bald, ZombieClothing.Shirt, bare, 22, blood: 0.7f, eyeGlow: 0.28f, eye: new[] { 0.9f, 0.6f, 0.35f }),
                V("runner_military", ZombieIds.Runner, "Corredor con restos de uniforme", H, Tufts, ZombieClothing.Military, boots, 23, hairColor: new[] { 0.16f, 0.12f, 0.08f }, eyeGlow: 0.3f),

                V("tank_rags", ZombieIds.Tank, "Tanque en harapos", S, Bald, ZombieClothing.Rags, bare, 31, skin: new[] { 0.95f, 1f, 0.92f }, blood: 0.6f, grime: 0.8f, eyeGlow: 0.25f),
                V("tank_work", ZombieIds.Tank, "Tanque con mono de obra", JB, Tufts, ZombieClothing.Workwear, bare, 32, hairColor: new[] { 0.1f, 0.09f, 0.08f }, grime: 0.9f),
                V("tank_scarred", ZombieIds.Tank, "Tanque cicatrizado", JE, Matted, ZombieClothing.Rags, bare, 33, skin: new[] { 1f, 0.94f, 0.9f }, blood: 0.8f, eyeGlow: 0.3f),

                V("armored_vest", ZombieIds.Armored, "Blindado con chaleco", H, Bald, ZombieClothing.Military, boots, 41, armor: ZombieArmor.VestPlates),
                V("armored_door", ZombieIds.Armored, "Blindado con puerta", JB, Tufts, ZombieClothing.Workwear, boots, 42, armor: ZombieArmor.DoorShield, grime: 0.8f),
                V("armored_tires", ZombieIds.Armored, "Blindado con neumáticos", S, Matted, ZombieClothing.Shirt, bare, 43, armor: ZombieArmor.TireRings, blood: 0.6f),
                V("armored_scrap", ZombieIds.Armored, "Blindado de chatarra", JE, Bald, ZombieClothing.Rags, boots, 44, armor: ZombieArmor.MixedScrap, grime: 0.9f),

                V("spitter_work", ZombieIds.Spitter, "Lanzador de obra", JB, Tufts, ZombieClothing.Workwear, bare, 51, skin: new[] { 0.92f, 1f, 0.85f }, eye: new[] { 0.6f, 0.9f, 0.3f }, eyeGlow: 0.3f),
                V("spitter_lab", ZombieIds.Spitter, "Lanzador con bata", S, Matted, ZombieClothing.LabCoat, bare, 52, skin: new[] { 0.9f, 1f, 0.82f }, eye: new[] { 0.55f, 0.95f, 0.35f }, eyeGlow: 0.34f, blood: 0.5f),
                V("spitter_rags", ZombieIds.Spitter, "Lanzador en harapos", JE, Bald, ZombieClothing.Rags, boots, 53, skin: new[] { 0.95f, 1f, 0.8f }, eye: new[] { 0.7f, 0.9f, 0.3f }, eyeGlow: 0.3f),

                V("explosive_shirt", ZombieIds.Explosive, "Explosivo con camisa rota", H, Tufts, ZombieClothing.Shirt, bare, 61, skin: new[] { 1f, 0.96f, 0.9f }, eye: new[] { 1f, 0.55f, 0.2f }, eyeGlow: 0.35f),
                V("explosive_work", ZombieIds.Explosive, "Explosivo de obra", JB, Bald, ZombieClothing.Workwear, boots, 62, skin: new[] { 1f, 0.94f, 0.88f }, eye: new[] { 1f, 0.5f, 0.15f }, eyeGlow: 0.4f, grime: 0.7f),
                V("explosive_rags", ZombieIds.Explosive, "Explosivo en harapos", S, Matted, ZombieClothing.Rags, bare, 63, skin: new[] { 1.02f, 0.92f, 0.86f }, eye: new[] { 1f, 0.6f, 0.2f }, eyeGlow: 0.4f),
            };
        }

        public static List<ZombieVariantData> ForType(IEnumerable<ZombieVariantData> all, string typeId)
        {
            var r = new List<ZombieVariantData>();
            foreach (var v in all) if (v.typeId == typeId) r.Add(v);
            return r;
        }
    }

    /// <summary>Valida variantes que vienen de fuera (JSON del backend o de Gemini offline): tipo conocido, enums en rango, colores 0..1 y valores acotados.</summary>
    public static class ZombieLookValidator
    {
        public static List<string> Validate(IList<ZombieVariantData> list)
        {
            var errors = new List<string>();
            if (list == null || list.Count == 0) { errors.Add("no hay variantes de zombi"); return errors; }
            var ids = new HashSet<string>();
            var perType = new Dictionary<string, int>();
            foreach (var v in list)
            {
                if (v == null || string.IsNullOrEmpty(v.id) || !ids.Add(v.id)) { errors.Add("variante sin id o repetida"); continue; }
                string n = v.id + ": ";
                if (!ZombieAnatomies.Has(v.typeId)) errors.Add(n + "typeId desconocido '" + v.typeId + "'");
                else perType[v.typeId] = (perType.TryGetValue(v.typeId, out int c) ? c : 0) + 1;
                if (!Enum.IsDefined(typeof(ZombieHeadStyle), v.head)) errors.Add(n + "head fuera de rango");
                if (!Enum.IsDefined(typeof(ZombieHairStyle), v.hair)) errors.Add(n + "hair fuera de rango");
                if (!Enum.IsDefined(typeof(ZombieClothing), v.clothing)) errors.Add(n + "clothing fuera de rango");
                if (!Enum.IsDefined(typeof(ZombieFootwear), v.footwear)) errors.Add(n + "footwear fuera de rango");
                if (!Enum.IsDefined(typeof(ZombieArmor), v.armor)) errors.Add(n + "armor fuera de rango");
                if (v.armor != ZombieArmor.None && v.typeId != ZombieIds.Armored) errors.Add(n + "solo el blindado lleva armor");
                if (v.typeId == ZombieIds.Armored && v.armor == ZombieArmor.None) errors.Add(n + "el blindado necesita armor");
                CheckColor(errors, n + "skinTone", v.skinTone, 0.5f, 1.5f); CheckColor(errors, n + "clothTone", v.clothTone, 0.3f, 1.6f);
                CheckColor(errors, n + "hairColor", v.hairColor, 0f, 1f); CheckColor(errors, n + "eyeTint", v.eyeTint, 0f, 1f);
                if (!(v.eyeGlow >= 0f && v.eyeGlow <= 1f)) errors.Add(n + "eyeGlow fuera de 0..1");
                if (!(v.grime >= 0f && v.grime <= 1f)) errors.Add(n + "grime fuera de 0..1");
                if (!(v.bloodiness >= 0f && v.bloodiness <= 1f)) errors.Add(n + "bloodiness fuera de 0..1");
            }
            foreach (string t in ZombieAnatomies.Types) if (!perType.ContainsKey(t)) errors.Add("el tipo '" + t + "' no tiene ninguna variante");
            return errors;
        }

        private static void CheckColor(List<string> errors, string name, float[] c, float lo, float hi)
        {
            if (c == null || c.Length != 3) { errors.Add(name + " debe tener 3 valores"); return; }
            foreach (float x in c) if (float.IsNaN(x) || x < lo || x > hi) { errors.Add(name + " fuera de " + lo + ".." + hi); return; }
        }
    }

    /// <summary>
    /// Los rasgos de UN zombi concreto (se tiran al aparecer, deterministas desde una semilla): qué variante, cuánto cojea, la fase y la velocidad de su paso, tono de
    /// piel, suciedad, brillo de ojos y qué manchas del repertorio lleva. Con esto 20 zombis del mismo tipo no son clones.
    /// </summary>
    public struct ZombieIndividual
    {
        public int Seed, VariantIndex;
        public float Limp;            // 0..1 cuánto arrastra un pie
        public int LimpSide;          // -1 pie izquierdo, +1 derecho
        public float SpeedJitter;     // 0.9..1.1 multiplica la cadencia de la animación
        public float Phase;           // 0..1 desfase de balanceos
        public float HeadTilt;        // grados, cabeza ladeada de reposo (signo = lado)
        public float ShoulderDrop;    // 0..1 hombros caídos
        public float Aggression;      // 0..1 cuánto agita los brazos al correr
        public float SkinShift;       // -0.08..0.08 oscurece o aclara la piel
        public float Grime, Blood;    // 0..1 suciedad y sangre de este individuo
        public float EyeGlow;         // 0..1
        public int WoundMask;         // bits de las marcas del repertorio que lleva desde el principio
        public int MoanSeed;

        public const int WoundSlots = 8;

        public static ZombieIndividual Roll(int seed, int variantCount, ZombieVariantData v)
        {
            var r = new Random(seed * 7919 + 17);
            float F() => (float)r.NextDouble();
            var z = new ZombieIndividual
            {
                Seed = seed, VariantIndex = variantCount > 0 ? r.Next(variantCount) : 0,
                Limp = F() < 0.45f ? 0.35f + 0.65f * F() : 0.05f * F(), LimpSide = F() < 0.5f ? -1 : 1,
                SpeedJitter = 0.9f + 0.2f * F(), Phase = F(), HeadTilt = (F() - 0.5f) * 2f * 14f, ShoulderDrop = F(), Aggression = F(),
                SkinShift = (F() - 0.5f) * 0.16f, Grime = F(), Blood = F(), EyeGlow = F(), MoanSeed = r.Next(),
            };
            if (v != null) { z.Grime = Clamp01(v.grime * 0.6f + z.Grime * 0.4f); z.Blood = Clamp01(v.bloodiness * 0.6f + z.Blood * 0.4f); z.EyeGlow = Clamp01(v.eyeGlow * 0.7f + z.EyeGlow * 0.3f); }
            // 8 marcas posibles (mordida en cuello, hombro, antebrazo, costado, muslo, cara, mano, vientre): cada una aparece con probabilidad según la sangre
            for (int i = 0; i < WoundSlots; i++) if (F() < 0.25f + 0.55f * z.Blood) z.WoundMask |= 1 << i;
            return z;
        }

        public bool HasWound(int slot) => (WoundMask & (1 << slot)) != 0;
        private static float Clamp01(float x) => x < 0f ? 0f : (x > 1f ? 1f : x);
    }
}
