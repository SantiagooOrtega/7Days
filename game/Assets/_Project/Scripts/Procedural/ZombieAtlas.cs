namespace SevenDays.Procedural
{
    /// <summary>
    /// Disposición de los atlas de textura de los zombis (512 x 512 cada uno). La piel tiene un atlas por tipo (rasgos propios del tipo), la ropa y las heridas son
    /// comunes a todos. Las celdas pequeñas son de 128 px; el cuerpo y la cabeza ocupan 256 px.
    ///   Piel (u, v de 0 a 1; v=0 abajo):  cuerpo [0, .5]x[0, .5] | cabeza [.5, 1]x[0, .5] | 8 celdas de 128 px en las dos filas de arriba.
    ///   Ropa y heridas: rejilla de 4 x 4 celdas de 128 px.
    /// </summary>
    public static class ZombieAtlas
    {
        public const int Size = 512;

        // ---- piel (por tipo)
        public const int SkBody = 0, SkHead = 1, SkHand = 2, SkFoot = 3, SkBone = 4, SkEye = 5, SkHair = 6, SkTissue = 7, SkFeatureA = 8, SkFeatureB = 9;
        // SkFeatureA / SkFeatureB según el tipo: tanque = placas cicatrizadas y músculo hinchado; explosivo = pústulas y abdomen con venas;
        // lanzador = bolsas y pústulas verdosas; corredor = tendones y costillas; blindado/común = costado desgarrado y cuello.

        public static AtlasCell Skin(int id)
        {
            if (id == SkBody) return new AtlasCell(0f, 0f, 0.5f, 0.012f);
            if (id == SkHead) return new AtlasCell(0.5f, 0f, 0.5f, 0.012f);
            int i = id - SkHand;
            return new AtlasCell((i % 4) * 0.25f, 0.5f + (i / 4) * 0.25f, 0.25f, 0.01f);
        }

        // ---- ropa (común)
        public const int ClShirt = 0, ClTee = 1, ClMilitary = 2, ClLab = 3, ClSuit = 4, ClTrousers = 5, ClJeans = 6, ClOveralls = 7,
                         ClRags = 8, ClLeather = 9, ClMetal = 10, ClRubber = 11, ClWebbing = 12, ClDoor = 13, ClBandage = 14, ClSack = 15;

        public static AtlasCell Cloth(int id) => AtlasCell.Grid(id, 4, 0.04f);

        // ---- heridas (decals con recorte por alfa, común)
        public const int WdBite = 0, WdGash = 1, WdBullet = 2, WdStain = 3, WdBruise = 4, WdScab = 5, WdDrip = 6, WdTear = 7,
                         WdHead = 8, WdBurn = 9, WdBone = 10, WdSplat = 11, WdBiteB = 12, WdGashB = 13, WdStainB = 14, WdTearB = 15;

        public static AtlasCell Wound(int id) => AtlasCell.Grid(id, 4, 0.03f);

        /// <summary>Tela de la ropa de cada prenda (cuerpo) y de los pantalones.</summary>
        public static int TopCell(ZombieClothing c)
        {
            switch (c)
            {
                case ZombieClothing.Military: return ClMilitary;
                case ZombieClothing.LabCoat: return ClLab;
                case ZombieClothing.Suit: return ClSuit;
                case ZombieClothing.Workwear: return ClOveralls;
                case ZombieClothing.Rags: return ClRags;
                default: return ClShirt;
            }
        }

        public static int BottomCell(ZombieClothing c)
        {
            switch (c)
            {
                case ZombieClothing.Military: return ClMilitary;
                case ZombieClothing.Suit: return ClTrousers;
                case ZombieClothing.Workwear: return ClOveralls;
                case ZombieClothing.Rags: return ClRags;
                case ZombieClothing.LabCoat: return ClTrousers;
                default: return ClJeans;
            }
        }
    }
}
