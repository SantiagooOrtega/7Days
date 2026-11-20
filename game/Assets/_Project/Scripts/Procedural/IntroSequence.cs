using System;
using System.Collections.Generic;
using System.Globalization;

namespace SevenDays.Procedural
{
    /// <summary>Quién habla en una viñeta de la intro. Cambia el estilo del recuadro y el sonido (pitido de radio para VIGÍA, estática para Ibarra).</summary>
    public enum IntroSpeaker { Narrator, Vigia, Ibarra }

    /// <summary>Dónde está el texto: ya dibujado en la imagen (Unity no lo escribe encima, salvo que falte la imagen) o escrito por Unity con efecto de máquina de escribir.</summary>
    public enum IntroTextSource { Image, Unity }

    /// <summary>Una viñeta de la intro: una línea de intro.txt con el formato "número | QUIÉN | DÓNDE | texto".</summary>
    public sealed class IntroPanel
    {
        public int Number;
        public IntroSpeaker Speaker;
        public IntroTextSource Source;
        public string Text = "";

        /// <summary>Nombre de su imagen en Resources/Intro: intro_01 ... intro_07 (PNG o JPG).</summary>
        public string ImageName => "intro_" + Number.ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>Textos fijos de la historia (ver docs/HISTORIA.md).</summary>
    public static class Story
    {
        public const string GameTitle = "7 DÍAS";
        public static readonly string[] DayTitles = { "La ceniza", "Los recientes", "El capataz", "Los trajes", "Lluvia de concreto", "Silencio", "La floración" };

        /// <summary>"Día 1: La ceniza".</summary>
        public static string DayHeading(int day)
        {
            day = Math.Max(1, Math.Min(DayTitles.Length, day));
            return "Día " + day + ": " + DayTitles[day - 1];
        }
    }

    /// <summary>Lee intro.txt. Las líneas vacías o que empiezan por # se ignoran; las mal formadas se saltan y se explican en errors.</summary>
    public static class IntroScript
    {
        public static List<IntroPanel> Parse(string text, List<string> errors = null)
        {
            var list = new List<IntroPanel>();
            if (string.IsNullOrEmpty(text)) return list;
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimStart('﻿').Trim();   // el BOM de UTF-8 no cuenta como espacio para Trim
                if (line.Length == 0 || line[0] == '#') continue;
                var parts = line.Split(new[] { '|' }, 4);           // el texto puede llevar '|': solo se cortan los 3 primeros
                string where = "línea " + (i + 1) + ": ";
                if (parts.Length < 4) { errors?.Add(where + "faltan campos (formato: número | QUIÉN | DÓNDE | texto)"); continue; }
                if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || number < 1)
                { errors?.Add(where + "número de viñeta no válido '" + parts[0].Trim() + "'"); continue; }
                if (!TryParseSpeaker(parts[1], out var speaker)) { errors?.Add(where + "QUIÉN debe ser NARRADOR, VIGÍA o IBARRA, no '" + parts[1].Trim() + "'"); continue; }
                if (!TryParseSource(parts[2], out var source)) { errors?.Add(where + "DÓNDE debe ser IMAGEN o UNITY, no '" + parts[2].Trim() + "'"); continue; }
                string body = parts[3].Trim();
                if (body.Length == 0) { errors?.Add(where + "sin texto"); continue; }
                list.Add(new IntroPanel { Number = number, Speaker = speaker, Source = source, Text = body });
            }
            return list;
        }

        /// <summary>NARRADOR, VIGÍA (o VIGIA) e IBARRA (o CORONEL IBARRA), sin importar mayúsculas ni tildes.</summary>
        public static bool TryParseSpeaker(string s, out IntroSpeaker speaker)
        {
            switch (Simplify(s))
            {
                case "NARRADOR": speaker = IntroSpeaker.Narrator; return true;
                case "VIGIA": speaker = IntroSpeaker.Vigia; return true;
                case "IBARRA":
                case "CORONEL IBARRA": speaker = IntroSpeaker.Ibarra; return true;
                default: speaker = IntroSpeaker.Narrator; return false;
            }
        }

        public static bool TryParseSource(string s, out IntroTextSource source)
        {
            switch (Simplify(s))
            {
                case "IMAGEN": source = IntroTextSource.Image; return true;
                case "UNITY": source = IntroTextSource.Unity; return true;
                default: source = IntroTextSource.Image; return false;
            }
        }

        /// <summary>Etiqueta del recuadro: null para el narrador.</summary>
        public static string Label(IntroSpeaker s) => s == IntroSpeaker.Vigia ? "VIGÍA" : s == IntroSpeaker.Ibarra ? "CORONEL IBARRA" : null;

        private static string Simplify(string s)
        {
            if (s == null) return "";
            return s.Trim().ToUpperInvariant().Replace('Á', 'A').Replace('É', 'E').Replace('Í', 'I').Replace('Ó', 'O').Replace('Ú', 'U');
        }
    }

    /// <summary>Etapas de cada viñeta: entra desde negro, Unity escribe el texto (si le toca), espera, y sale a negro.</summary>
    public enum IntroStage { FadeIn, Typing, Hold, FadeOut, Done }

    /// <summary>
    /// Estado de la intro, sin Unity (se prueba en EditMode). Viñetas 0..Count-1 y, al final, el título ("7 DÍAS" / "Día 1: La ceniza").
    /// Avanzar: si el texto se está escribiendo lo completa; si no, pasa a la siguiente. Las viñetas de IMAGEN esperan al jugador;
    /// las de UNITY pasan solas tras un tiempo de lectura; el título se cierra solo.
    /// </summary>
    public sealed class IntroSequence
    {
        public const float FadeInSeconds = 0.8f;
        public const float FadeOutSeconds = 0.5f;
        public const float CharsPerSecond = 40f;
        /// <summary>Tiempo de lectura de una viñeta UNITY ya escrita antes de pasar sola: base + por letra.</summary>
        public const float ReadBaseSeconds = 3f, ReadPerCharSeconds = 0.05f;
        public const float TitleHoldSeconds = 3.5f;

        private readonly List<IntroPanel> _panels;
        private readonly bool[] _hasImage;

        public IntroSequence(IList<IntroPanel> panels, Func<IntroPanel, bool> hasImage = null)
        {
            _panels = panels != null ? new List<IntroPanel>(panels) : new List<IntroPanel>();
            _hasImage = new bool[_panels.Count];
            for (int i = 0; i < _panels.Count; i++) _hasImage[i] = hasImage != null && hasImage(_panels[i]);
            Enter(0);
        }

        /// <summary>Número de viñetas (sin contar el título final).</summary>
        public int Count => _panels.Count;
        /// <summary>0..Count-1 = viñeta; Count = título; Count+1 = terminado.</summary>
        public int Index { get; private set; }
        public IntroStage Stage { get; private set; }
        /// <summary>Segundos en la etapa actual.</summary>
        public float StageTime { get; private set; }
        /// <summary>Segundos desde que entró la viñeta (para el zoom y el paneo, que siguen durante todas sus etapas).</summary>
        public float PanelTime { get; private set; }
        public int VisibleChars { get; private set; }
        public bool WasSkipped { get; private set; }

        public bool IsDone => Stage == IntroStage.Done;
        public bool OnTitle => !IsDone && Index == Count;
        public IntroPanel Current => Index < Count ? _panels[Index] : null;
        public bool HasImage => Index < Count && _hasImage[Index];
        /// <summary>Unity dibuja el texto: siempre en las viñetas UNITY y, en las IMAGEN, solo si falta la imagen.</summary>
        public bool ShowsText => Current != null && (Current.Source == IntroTextSource.Unity || !HasImage);
        public int TextLength => ShowsText ? Current.Text.Length : 0;
        public bool TextComplete => VisibleChars >= TextLength;
        /// <summary>Las viñetas IMAGEN (con imagen o sin ella) no avanzan solas.</summary>
        public bool WaitsForPlayer => Current != null && Current.Source == IntroTextSource.Image;
        public float HoldSeconds => OnTitle ? TitleHoldSeconds : WaitsForPlayer ? float.PositiveInfinity : ReadBaseSeconds + TextLength * ReadPerCharSeconds;

        /// <summary>Opacidad de la viñeta (0 = negro).</summary>
        public float Alpha
        {
            get
            {
                switch (Stage)
                {
                    case IntroStage.FadeIn: return Clamp01(StageTime / FadeInSeconds);
                    case IntroStage.FadeOut: return Clamp01(1f - StageTime / FadeOutSeconds);
                    case IntroStage.Done: return 0f;
                    default: return 1f;
                }
            }
        }

        /// <summary>Progreso de la barra: sube un paso por viñeta y llega a 1 en el título.</summary>
        public float Progress => IsDone ? 1f : (Index + 1f) / (Count + 1f);

        public void Tick(float dt)
        {
            if (IsDone || dt <= 0f) return;
            StageTime += dt;
            PanelTime += dt;
            for (int guard = 0; guard < 8 && !IsDone; guard++)   // un dt grande puede encadenar varias etapas
            {
                switch (Stage)
                {
                    case IntroStage.FadeIn:
                        if (StageTime < FadeInSeconds) return;
                        SetStage(ShowsText && !TextComplete ? IntroStage.Typing : IntroStage.Hold, StageTime - FadeInSeconds);
                        break;
                    case IntroStage.Typing:
                        VisibleChars = Math.Max(VisibleChars, Math.Min(TextLength, (int)(StageTime * CharsPerSecond)));
                        if (!TextComplete) return;
                        SetStage(IntroStage.Hold, Math.Max(0f, StageTime - TextLength / CharsPerSecond));
                        break;
                    case IntroStage.Hold:
                        if (StageTime < HoldSeconds) return;
                        SetStage(IntroStage.FadeOut, StageTime - HoldSeconds);
                        break;
                    case IntroStage.FadeOut:
                        if (StageTime < FadeOutSeconds) return;
                        float carry = StageTime - FadeOutSeconds;
                        Enter(Index + 1);
                        StageTime = carry;
                        PanelTime = carry;
                        break;
                }
            }
        }

        /// <summary>Clic, Espacio o flecha derecha. Si el texto aún no está completo, lo completa; si ya lo está, pasa a la siguiente viñeta.</summary>
        public void Advance()
        {
            switch (Stage)
            {
                case IntroStage.FadeIn:
                    if (!TextComplete) VisibleChars = TextLength;   // sigue el fundido, pero con el texto entero
                    else SetStage(IntroStage.Hold, 0f);
                    break;
                case IntroStage.Typing:
                    VisibleChars = TextLength;
                    SetStage(IntroStage.Hold, 0f);
                    break;
                case IntroStage.Hold:
                    SetStage(IntroStage.FadeOut, 0f);
                    break;
                case IntroStage.FadeOut:
                    Enter(Index + 1);   // con prisa: no espera a que termine el fundido
                    break;
            }
        }

        /// <summary>Flecha izquierda: vuelve a la viñeta anterior, con su texto ya escrito. En la primera no hace nada.</summary>
        public void Back()
        {
            if (IsDone) return;
            if (Index == 0 && Stage != IntroStage.FadeOut) return;
            Enter(Math.Max(0, Index - 1));
            VisibleChars = TextLength;
        }

        /// <summary>Esc o botón "Saltar": termina ya.</summary>
        public void Skip()
        {
            if (IsDone) return;
            WasSkipped = true;
            Index = Count + 1;
            Stage = IntroStage.Done;
        }

        private void Enter(int index)
        {
            Index = index;
            StageTime = 0f;
            PanelTime = 0f;
            VisibleChars = 0;
            Stage = index > Count ? IntroStage.Done : IntroStage.FadeIn;
        }

        private void SetStage(IntroStage stage, float time)
        {
            Stage = stage;
            StageTime = time;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    /// <summary>Rectángulo en coordenadas de pantalla (x, y arriba a la izquierda).</summary>
    public struct IntroRect
    {
        public float X, Y, W, H;
        public IntroRect(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
    }

    /// <summary>
    /// Dónde se dibuja la imagen de una viñeta. IMAGEN: letterbox (se ve entera) con un zoom de 5 % como máximo que nunca recorta el texto dibujado.
    /// UNITY: llena la pantalla con un zoom y un paneo más notorios, porque el texto lo pone Unity encima.
    /// </summary>
    public static class IntroLayout
    {
        public const float ImageZoom = 0.05f;
        public const float UnityZoom = 0.12f;
        /// <summary>Constante de tiempo del zoom (empieza rápido y se va frenando).</summary>
        public const float ZoomSeconds = 8f;

        /// <summary>Avance del zoom 0..1 según el tiempo en la viñeta.</summary>
        public static float Ease(float panelTime) => 1f - (float)Math.Exp(-Math.Max(0f, panelTime) / ZoomSeconds);

        /// <summary>Letterbox: la escala va de 1/(1+zoom) del ajuste a pantalla hasta el ajuste exacto, así que la imagen entera siempre queda dentro.</summary>
        public static IntroRect Fit(float screenW, float screenH, float imageW, float imageH, float t, float zoom = ImageZoom)
        {
            float fit = Math.Min(screenW / imageW, screenH / imageH);
            float s = fit * Lerp(1f / (1f + zoom), 1f, t);
            float w = imageW * s, h = imageH * s;
            return new IntroRect((screenW - w) * 0.5f, (screenH - h) * 0.5f, w, h);
        }

        /// <summary>Llena la pantalla (recorta bordes) y se acerca hasta 1+zoom. pan (-1..1) elige hacia qué borde se va el encuadre; nunca deja ver el fondo.</summary>
        public static IntroRect Cover(float screenW, float screenH, float imageW, float imageH, float t, float zoom, float panX, float panY)
        {
            float cover = Math.Max(screenW / imageW, screenH / imageH);
            float s = cover * (1f + zoom * Clamp01(t));
            float w = imageW * s, h = imageH * s;
            float spareX = (w - screenW) * 0.5f, spareY = (h - screenH) * 0.5f;   // lo que sobra por cada lado
            return new IntroRect(-spareX * (1f + Clamp(panX)), -spareY * (1f + Clamp(panY)), w, h);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        private static float Clamp(float v) => v < -1f ? -1f : (v > 1f ? 1f : v);
    }
}
