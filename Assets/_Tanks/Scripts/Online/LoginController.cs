using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tanks.Complete
{
    /// <summary>
    /// Self-contained login/register screen (Slice 5) built entirely in code, matching
    /// <see cref="OnlineMenuController"/>. Uses UGS username/password auth via <see cref="UGSBootstrap"/>.
    /// On a successful sign-in (register, login, or guest) it loads the online lobby menu.
    /// </summary>
    public class LoginController : MonoBehaviour
    {
        [Tooltip("Scene loaded after a successful sign-in.")]
        public string m_OnlineScene = "Online";
        [Tooltip("Scene loaded when pressing BACK.")]
        public string m_MainMenuScene = "MainMenu";

        private UGSBootstrap m_UGS;
        private TMP_InputField m_UsernameInput;
        private TMP_InputField m_PasswordInput;
        private TextMeshProUGUI m_StatusText;
        private Button m_LoginButton;
        private Button m_RegisterButton;
        private Button m_GuestButton;
        private bool m_Navigating;
        private string m_LastStatus;

        private void Awake()
        {
            EnsureEventSystem();
            m_UGS = UGSBootstrap.Ensure();
            BuildUI();
        }

        private void Update()
        {
            if (m_StatusText != null && m_UGS != null)
            {
                string label = m_UGS.StatusLabel();
                if (label != m_LastStatus)
                {
                    m_StatusText.text = label;
                    m_LastStatus = label;
                }
            }

            bool busy = m_UGS != null && m_UGS.CurrentStatus == UGSBootstrap.Status.SigningIn;
            bool interactable = !busy && !m_Navigating;
            if (m_LoginButton != null) m_LoginButton.interactable = interactable;
            if (m_RegisterButton != null) m_RegisterButton.interactable = interactable;
            if (m_GuestButton != null) m_GuestButton.interactable = interactable;

            // If we became signed in (e.g. session restore), move on.
            if (!m_Navigating && m_UGS != null && m_UGS.IsSignedIn)
                GoOnline();
        }

        // ---- Auth actions ----

        private async void OnLoginClicked()
        {
            if (!ValidateInputs(out string user, out string pass))
                return;
            if (await m_UGS.LoginAsync(user, pass))
                GoOnline();
        }

        private async void OnRegisterClicked()
        {
            if (!ValidateInputs(out string user, out string pass))
                return;
            if (await m_UGS.RegisterAsync(user, pass))
                GoOnline();
        }

        private async void OnGuestClicked()
        {
            if (await m_UGS.SignInAsGuestAsync())
                GoOnline();
        }

        private void OnBackClicked()
        {
            LoadScene(m_MainMenuScene);
        }

        private bool ValidateInputs(out string user, out string pass)
        {
            user = m_UsernameInput != null ? m_UsernameInput.text.Trim() : string.Empty;
            pass = m_PasswordInput != null ? m_PasswordInput.text : string.Empty;

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(pass))
            {
                if (m_StatusText != null)
                    m_StatusText.text = "Nhập tên đăng nhập và mật khẩu.";
                m_LastStatus = m_StatusText != null ? m_StatusText.text : null;
                return false;
            }

            return true;
        }

        private void GoOnline()
        {
            if (m_Navigating)
                return;
            m_Navigating = true;
            LoadScene(m_OnlineScene);
        }

        private void LoadScene(string scene)
        {
            if (Application.CanStreamedLevelBeLoaded(scene))
                SceneManager.LoadScene(scene);
            else
                Debug.LogError($"LoginController: scene '{scene}' is not in Build Settings.");
        }

        // ---- UI construction ----

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("LoginCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var bg = CreateStretchImage("Background", canvasGo.transform, new Color(0.08f, 0.11f, 0.14f, 1f));

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(bg.transform, false);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(820, 900);
            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 18;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            CreateText("Title", panel.transform, "TÀI KHOẢN", 80, FontStyles.Bold, new Color(0.95f, 0.95f, 0.98f), 110);

            m_UsernameInput = CreateInputField(panel.transform, "Tên đăng nhập", 78, TMP_InputField.ContentType.Standard, 20);
            m_PasswordInput = CreateInputField(panel.transform, "Mật khẩu", 78, TMP_InputField.ContentType.Password, 30);

            m_StatusText = CreateText("Status", panel.transform, "Chưa đăng nhập", 28, FontStyles.Normal, new Color(0.62f, 0.80f, 0.95f), 90);

            m_LoginButton = CreateButton(panel.transform, "ĐĂNG NHẬP", new Color(0.26f, 0.44f, 0.62f), 88, OnLoginClicked);
            m_RegisterButton = CreateButton(panel.transform, "ĐĂNG KÝ", new Color(0.24f, 0.55f, 0.35f), 88, OnRegisterClicked);
            m_GuestButton = CreateButton(panel.transform, "CHƠI KHÁCH", new Color(0.42f, 0.42f, 0.46f), 74, OnGuestClicked);

            CreateSpacer(panel.transform, 10);
            CreateButton(panel.transform, "QUAY LẠI", new Color(0.42f, 0.42f, 0.46f), 66, OnBackClicked);
        }

        // ---- UI helpers (self-contained, mirroring OnlineMenuController) ----

        private static Image CreateStretchImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            return go.GetComponent<Image>();
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size,
            FontStyles style, Color color, float preferredHeight)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            go.GetComponent<LayoutElement>().preferredHeight = preferredHeight;
            return tmp;
        }

        private static void CreateSpacer(Transform parent, float height)
        {
            var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
        }

        private static Button CreateButton(Transform parent, string label, Color accent, float height, Action onClick)
        {
            var go = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            go.GetComponent<Image>().color = accent;

            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 40;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            return button;
        }

        private static TMP_InputField CreateInputField(Transform parent, string placeholder, float height,
            TMP_InputField.ContentType contentType, int charLimit)
        {
            var go = new GameObject($"Input_{placeholder}", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            go.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.22f, 1f);

            var input = go.GetComponent<TMP_InputField>();

            var textArea = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(go.transform, false);
            var taRt = textArea.GetComponent<RectTransform>();
            taRt.anchorMin = Vector2.zero;
            taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(20, 8);
            taRt.offsetMax = new Vector2(-20, -8);

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            placeholderGo.transform.SetParent(textArea.transform, false);
            StretchToParent(placeholderGo.GetComponent<RectTransform>());
            var phTmp = placeholderGo.GetComponent<TextMeshProUGUI>();
            phTmp.text = placeholder;
            phTmp.fontSize = 34;
            phTmp.fontStyle = FontStyles.Italic;
            phTmp.color = new Color(0.6f, 0.6f, 0.65f, 0.8f);
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(textArea.transform, false);
            StretchToParent(textGo.GetComponent<RectTransform>());
            var txtTmp = textGo.GetComponent<TextMeshProUGUI>();
            txtTmp.fontSize = 34;
            txtTmp.color = Color.white;
            txtTmp.alignment = TextAlignmentOptions.MidlineLeft;

            input.textViewport = taRt;
            input.textComponent = txtTmp;
            input.placeholder = phTmp;
            input.characterLimit = charLimit;
            input.contentType = contentType;
            input.text = string.Empty;

            return input;
        }

        private static void StretchToParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
