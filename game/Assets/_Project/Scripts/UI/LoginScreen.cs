using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using SevenDays.Net;

namespace SevenDays.UI
{
    /// <summary>Pantalla de login/registro (IMGUI provisional). Consume el backend con UnityWebRequest.</summary>
    public class LoginScreen : MonoBehaviour
    {
        [SerializeField] private string gameSceneName = "Game";

        private bool _registerMode;
        private bool _busy;
        private string _email = "";
        private string _username = "";
        private string _password = "";
        private string _status = "";
        private bool _statusIsError;
        private GUIStyle _label, _title, _status_, _field;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 22, normal = { textColor = Color.white } };
                _title = new GUIStyle(_label) { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _status_ = new GUIStyle(_label) { fontSize = 20, wordWrap = true, alignment = TextAnchor.UpperCenter };
                _field = new GUIStyle(GUI.skin.textField) { fontSize = 24 };
            }

            float s = Screen.height / 900f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s;

            float bw = 560f, x = (w - bw) / 2f, y = 120f;
            GUI.color = new Color(0, 0, 0, 0.75f);
            GUI.DrawTexture(new Rect(x - 40, y - 40, bw + 80, 640), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(x, y, bw, 70), "7 DÍAS", _title);
            y += 90;
            GUI.Label(new Rect(x, y, bw, 34), _registerMode ? "Crear cuenta" : "Iniciar sesión", _label);
            y += 44;

            GUI.enabled = !_busy;
            GUI.Label(new Rect(x, y, bw, 30), "Email", _label);
            _email = GUI.TextField(new Rect(x, y + 32, bw, 40), _email, 254, _field);
            y += 84;
            if (_registerMode)
            {
                GUI.Label(new Rect(x, y, bw, 30), "Usuario (3-20: letras, números, _)", _label);
                _username = GUI.TextField(new Rect(x, y + 32, bw, 40), _username, 20, _field);
                y += 84;
            }
            GUI.Label(new Rect(x, y, bw, 30), "Contraseña (mínimo 8)", _label);
            _password = GUI.PasswordField(new Rect(x, y + 32, bw, 40), _password, '•', 128, _field);
            y += 92;

            if (GUI.Button(new Rect(x, y, bw, 48), _registerMode ? "Registrarme" : "Entrar")) _ = Submit();
            if (GUI.Button(new Rect(x, y + 56, bw, 36), _registerMode ? "Ya tengo cuenta" : "Crear una cuenta nueva"))
            {
                _registerMode = !_registerMode;
                _status = "";
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (GUI.Button(new Rect(x, y + 98, bw, 32), "[Debug] Jugar sin cuenta")) SceneManager.LoadScene(gameSceneName);
#endif
            GUI.enabled = true;

            _status_.normal.textColor = _statusIsError ? new Color(1f, 0.5f, 0.45f) : new Color(0.6f, 1f, 0.6f);
            GUI.Label(new Rect(x, y + 140, bw, 80), _busy ? "Conectando..." : _status, _status_);
        }

        private async Task Submit()
        {
            if (_busy) return;
            _busy = true;
            _status = "";
            try
            {
                var res = _registerMode
                    ? await AuthService.Register(_email.Trim(), _username.Trim(), _password)
                    : await AuthService.Login(_email.Trim(), _password);

                if (res.Ok)
                {
                    _password = "";
                    SceneManager.LoadScene(gameSceneName);
                    return;
                }
                _status = res.Error;
                _statusIsError = true;
            }
            finally
            {
                _busy = false;
            }
        }
    }
}
