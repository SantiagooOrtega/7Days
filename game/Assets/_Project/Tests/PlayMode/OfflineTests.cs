using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SevenDays.Procedural;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Backend apagado: el login falla rápido con un mensaje claro (sin excepciones ni bloqueos), sin sesión no se intenta subir nada y la partida terminada
    /// se guarda en el perfil local. (Restaura perfil.json al terminar: la carpeta de datos es la misma que la del editor.)
    /// </summary>
    public class OfflineTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);

        [UnityTest, Timeout(60000)]
        public IEnumerator BackendOff_LoginFailsCleanly_LocalSaveWorks()
        {
            var api = T("SevenDays.Net.ApiClient"); var urlField = api.GetField("BaseUrl");
            string url = (string)urlField.GetValue(null);
            var fed = T("SevenDays.UI.FrontEndData");
            string file = (string)fed.GetProperty("PathFile").GetValue(null);
            string backup = File.Exists(file) ? File.ReadAllText(file) : null;
            urlField.SetValue(null, "http://127.0.0.1:9");   // puerto cerrado: como el backend apagado
            try
            {
                float t0 = Time.realtimeSinceStartup;
                var task = (Task)T("SevenDays.Net.AuthService").GetMethod("Login").Invoke(null, new object[] { "nadie@ejemplo.test", "clave-segura-1" });
                while (!task.IsCompleted && Time.realtimeSinceStartup - t0 < 15f) yield return null;
                Assert.IsTrue(task.IsCompleted, "el login no se queda colgado");
                Assert.IsFalse(task.IsFaulted, "sin excepciones: " + task.Exception);
                var res = task.GetType().GetProperty("Result").GetValue(task);
                Assert.IsFalse((bool)res.GetType().GetField("Ok").GetValue(res));
                StringAssert.Contains("conectar", (string)res.GetType().GetField("Error").GetValue(res), "mensaje claro de servidor apagado");
                Assert.IsFalse((bool)T("SevenDays.Net.Session").GetProperty("IsLoggedIn").GetValue(null), "modo prueba: sin sesión");

                // sin sesión no se intenta subir nada
                var save = (Task<bool>)T("SevenDays.Net.ProgressService").GetMethod("SaveStats").Invoke(null, new object[] { 2, new CombatStatsRecord() });
                while (!save.IsCompleted) yield return null;
                Assert.IsFalse(save.Result, "sin sesión no sube estadísticas");

                // la partida se guarda en local
                var profile = (LocalProfile)fed.GetProperty("Profile").GetValue(null);
                int before = profile.matches.Count;
                var m = new MatchStats { mapId = "calle_en_ruinas", victory = false }; m.days.Add(new DayStats { day = 1, kills = 9 });
                fed.GetMethod("Record").Invoke(null, new object[] { m, 1 });
                Assert.AreEqual(Math.Min(before + 1, LocalProfile.MaxRecords), profile.matches.Count, "guardada en el perfil local");
                Assert.IsTrue(File.Exists(file), "perfil.json escrito");
                StringAssert.Contains("calle_en_ruinas", File.ReadAllText(file));
            }
            finally
            {
                urlField.SetValue(null, url);
                if (backup != null) File.WriteAllText(file, backup); else if (File.Exists(file)) File.Delete(file);
                fed.GetField("_profile", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
            }
        }

        /// <summary>Con el backend encendido (npm run dev, puerto 3001): el cliente del juego se registra, guarda las estadísticas del día y sube la partida.
        /// Comprueba que el JSON de Unity pasa la validación del servidor. Si el backend está apagado, se omite.</summary>
        [UnityTest, Timeout(60000)]
        public IEnumerator BackendOn_GameClient_Register_SaveDay_SubmitMatch()
        {
            var auth = T("SevenDays.Net.AuthService"); var session = T("SevenDays.Net.Session");
            string u = "prueba" + (Environment.TickCount & 0xFFFFF);
            var reg = (Task)auth.GetMethod("Register").Invoke(null, new object[] { u + "@ejemplo.test", u, "clave-segura-1" });
            while (!reg.IsCompleted) yield return null;
            var rr = reg.GetType().GetProperty("Result").GetValue(reg);
            if (!(bool)rr.GetType().GetField("Ok").GetValue(rr) && ((string)rr.GetType().GetField("Error").GetValue(rr)).Contains("conectar")) { Assert.Ignore("backend apagado: npm run dev para esta prueba"); yield break; }
            try
            {
                Assert.IsTrue((bool)rr.GetType().GetField("Ok").GetValue(rr), "registro: " + rr.GetType().GetField("Error").GetValue(rr));
                Assert.IsTrue((bool)session.GetProperty("IsLoggedIn").GetValue(null), "sesión iniciada");
                var stats = new CombatStatsRecord();
                var save = (Task<bool>)T("SevenDays.Net.ProgressService").GetMethod("SaveStats").Invoke(null, new object[] { 2, stats });
                while (!save.IsCompleted) yield return null;
                Assert.IsTrue(save.Result, "estadísticas del día guardadas en el servidor");
                var load = (Task<CombatStatsRecord>)T("SevenDays.Net.ProgressService").GetMethod("LoadStats").Invoke(null, null);
                while (!load.IsCompleted) yield return null;
                Assert.IsNotNull(load.Result, "y se cargan");
                var fed = T("SevenDays.UI.FrontEndData");
                var post = (Task)T("SevenDays.Net.ApiClient").GetMethod("Post").MakeGenericMethod(fed.GetNestedType("RankingSubmitResponse")).Invoke(null, new object[] {
                    "/api/rankings", Activator.CreateInstance(fed.GetNestedType("RankingSubmit")), true });
                while (!post.IsCompleted) yield return null;
                var pr = post.GetType().GetProperty("Result").GetValue(post);
                Assert.IsTrue((bool)pr.GetType().GetField("Ok").GetValue(pr), "partida subida al ranking: " + pr.GetType().GetField("Error").GetValue(pr));
            }
            finally { session.GetMethod("Clear").Invoke(null, null); }
        }
    }
}
