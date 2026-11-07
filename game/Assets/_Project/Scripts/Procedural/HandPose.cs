using System;

namespace SevenDays.Procedural
{
    /// <summary>Flexión de un dedo (grados en cada una de sus dos falanges).</summary>
    public struct FingerCurl
    {
        public float Base, Tip;
        public FingerCurl(float b, float t) { Base = b; Tip = t; }
    }

    /// <summary>Pose de una mano: pulgar e índice, medio, anular y meñique (dos falanges cada uno) y oposición del pulgar.</summary>
    public struct HandPose
    {
        public FingerCurl Thumb, Index, Middle, Ring, Pinky;
        public float ThumbOpposition;   // grados: el pulgar cruza hacia la palma

        public FingerCurl this[int i] => i == 0 ? Thumb : i == 1 ? Index : i == 2 ? Middle : i == 3 ? Ring : Pinky;

        public static HandPose Lerp(HandPose a, HandPose b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            FingerCurl L(FingerCurl x, FingerCurl y) => new FingerCurl(x.Base + (y.Base - x.Base) * t, x.Tip + (y.Tip - x.Tip) * t);
            return new HandPose { Thumb = L(a.Thumb, b.Thumb), Index = L(a.Index, b.Index), Middle = L(a.Middle, b.Middle), Ring = L(a.Ring, b.Ring), Pinky = L(a.Pinky, b.Pinky),
                ThumbOpposition = a.ThumbOpposition + (b.ThumbOpposition - a.ThumbOpposition) * t };
        }
    }

    public enum HandGrip { Relaxed, Grip, Support, MagGrip, Pump, Fist, Open }

    /// <summary>Poses de mano procedurales (puras). Límites: cada falange entre 0 y 100°.</summary>
    public static class HandPoses
    {
        public const float MaxCurl = 100f;

        public static HandPose For(HandGrip g, bool triggerOn = false)
        {
            switch (g)
            {
                case HandGrip.Grip:     // empuñadura: dedos cerrados; índice estirado sobre el guardamonte hasta que dispara
                    return new HandPose { Thumb = new FingerCurl(25f, 30f), ThumbOpposition = 55f, Index = triggerOn ? new FingerCurl(40f, 45f) : new FingerCurl(8f, 4f),
                        Middle = new FingerCurl(80f, 75f), Ring = new FingerCurl(85f, 75f), Pinky = new FingerCurl(88f, 70f) };
                case HandGrip.Support:  // agarre en "C" bajo el cañón
                    return new HandPose { Thumb = new FingerCurl(15f, 20f), ThumbOpposition = 35f, Index = new FingerCurl(45f, 40f), Middle = new FingerCurl(50f, 45f), Ring = new FingerCurl(55f, 45f), Pinky = new FingerCurl(60f, 45f) };
                case HandGrip.MagGrip:  // sobre el cargador (subfusil)
                    return new HandPose { Thumb = new FingerCurl(30f, 25f), ThumbOpposition = 50f, Index = new FingerCurl(60f, 50f), Middle = new FingerCurl(65f, 55f), Ring = new FingerCurl(68f, 55f), Pinky = new FingerCurl(70f, 55f) };
                case HandGrip.Pump:     // bomba de la escopeta: mano cerrada alrededor
                    return new HandPose { Thumb = new FingerCurl(30f, 35f), ThumbOpposition = 55f, Index = new FingerCurl(70f, 60f), Middle = new FingerCurl(75f, 65f), Ring = new FingerCurl(78f, 65f), Pinky = new FingerCurl(80f, 65f) };
                case HandGrip.Fist:     // barra: puño cerrado
                    return new HandPose { Thumb = new FingerCurl(40f, 50f), ThumbOpposition = 65f, Index = new FingerCurl(90f, 85f), Middle = new FingerCurl(92f, 88f), Ring = new FingerCurl(94f, 88f), Pinky = new FingerCurl(95f, 85f) };
                case HandGrip.Open:     // mano abierta (recarga: coger el cargador)
                    return new HandPose { Thumb = new FingerCurl(5f, 5f), ThumbOpposition = 20f, Index = new FingerCurl(10f, 8f), Middle = new FingerCurl(10f, 8f), Ring = new FingerCurl(12f, 8f), Pinky = new FingerCurl(14f, 10f) };
                default:                // relajada
                    return new HandPose { Thumb = new FingerCurl(10f, 15f), ThumbOpposition = 25f, Index = new FingerCurl(18f, 15f), Middle = new FingerCurl(25f, 20f), Ring = new FingerCurl(30f, 22f), Pinky = new FingerCurl(35f, 25f) };
            }
        }

        public static bool WithinLimits(HandPose p)
        {
            for (int i = 0; i < 5; i++) { var f = p[i]; if (f.Base < 0f || f.Base > MaxCurl || f.Tip < 0f || f.Tip > MaxCurl) return false; }
            return true;
        }
    }

    /// <summary>
    /// Puntos de agarre de un arma (en el espacio del arma: x derecha, y arriba, z hacia la boca): empuñadura (mano derecha), guardamano/bomba/cargador (mano
    /// izquierda) y culata (al hombro). Rotaciones en grados (pitch, yaw, roll) de la mano.
    /// </summary>
    [Serializable]
    public class WeaponGrip
    {
        public float[] grip = { 0f, -0.1f, 0.045f, 15f, 0f, 0f };
        public float[] foregrip = { 0f, -0.07f, 0.4f, 0f, 0f, 0f };
        public float[] stock = { 0f, -0.02f, -0.2f };
        public string support = "cgrip";     // cgrip | mag | pump | pistol | none
        public bool shouldered = true;         // culata al hombro (rifles, escopeta, francotirador)

        public V3 GripPos => new V3(grip[0], grip[1], grip[2]);
        public V3 ForegripPos => new V3(foregrip[0], foregrip[1], foregrip[2]);
        public V3 StockPos => new V3(stock[0], stock[1], stock[2]);

        public HandGrip SupportGrip => support == "mag" ? HandGrip.MagGrip : support == "pump" ? HandGrip.Pump : HandGrip.Support;

        /// <summary>Agarre por defecto según el tipo de arma y su geometría (coincide con GunParts).</summary>
        public static WeaponGrip For(WeaponKind kind, float length, float thickness)
        {
            switch (kind)
            {
                case WeaponKind.Pistol:
                case WeaponKind.Revolver:
                    return new WeaponGrip { grip = new[] { 0f, -0.1f, 0.045f, 15f, 0f, 0f }, foregrip = new[] { -0.03f, -0.1f, 0.05f, 15f, 0f, -30f }, stock = new[] { 0f, 0f, 0f }, support = "pistol", shouldered = false };
                case WeaponKind.Smg:
                    return new WeaponGrip { grip = new[] { 0f, -0.1f, 0.045f, 15f, 0f, 0f }, foregrip = new[] { 0f, -0.15f, length * 0.42f, -12f, 0f, 0f }, stock = new[] { 0f, -0.01f, -0.06f }, support = "mag", shouldered = true };
                case WeaponKind.Shotgun:
                    return new WeaponGrip { grip = new[] { 0f, -0.1f, 0.045f, 15f, 0f, 0f }, foregrip = new[] { 0f, -0.08f, length * 0.42f, 0f, 0f, 0f }, stock = new[] { 0f, -0.02f, -0.21f }, support = "pump", shouldered = true };
                case WeaponKind.Melee:
                    return new WeaponGrip { grip = new[] { 0f, 0f, 0.08f, 0f, 0f, 0f }, foregrip = new[] { 0f, 0f, 0.2f, 0f, 0f, 0f }, stock = new[] { 0f, 0f, 0f }, support = "none", shouldered = false };
                case WeaponKind.SniperRifle:
                    return new WeaponGrip { grip = new[] { 0f, -0.1f, 0.045f, 15f, 0f, 0f }, foregrip = new[] { 0f, -0.07f, length * 0.22f, 0f, 0f, 0f }, stock = new[] { 0f, -0.02f, -0.24f }, support = "cgrip", shouldered = true };
                default:   // fusil de asalto
                    return new WeaponGrip { grip = new[] { 0f, -0.1f, 0.045f, 15f, 0f, 0f }, foregrip = new[] { 0f, -0.07f, length * 0.4f, 0f, 0f, 0f }, stock = new[] { 0f, -0.02f, -0.21f }, support = "cgrip", shouldered = true };
            }
        }
    }

    /// <summary>Colocación del arma apuntando con culata (pura): la culata toca el hombro y el arma mira hacia delante.</summary>
    public static class GripLayout
    {
        /// <summary>Al encarar un arma con culata el torso gira (postura de perfil): el hombro izquierdo se adelanta y llega al guardamano.</summary>
        public const float ShoulderedTwist = 25f;

        /// <summary>Gira un punto del cuerpo alrededor del eje vertical (grados, positivo = el lado derecho atrás, el izquierdo adelante).</summary>
        public static V3 Twist(V3 p, float yawDeg)
        {
            double a = yawDeg * Math.PI / 180.0;
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return new V3(p.X * c + p.Z * s, p.Y, -p.X * s + p.Z * c);
        }

        /// <summary>Origen del arma (espacio del cuerpo) para que su culata quede en "shoulder" con el arma recta hacia delante.</summary>
        public static V3 ShoulderedOrigin(V3 shoulder, WeaponGrip g) => shoulder - g.StockPos;

        /// <summary>Hueco del hombro donde apoya la culata: un poco hacia dentro, abajo y delante de la articulación del hombro derecho.</summary>
        public static V3 StockPocket(V3 rightShoulder) => rightShoulder + new V3(-0.1f, -0.03f, 0.08f);
    }
}
