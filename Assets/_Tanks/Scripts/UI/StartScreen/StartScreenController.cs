using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Tanks.Backend;

namespace Tanks.Complete
{
    /// <summary>
    /// Giao diện màn hình khởi đầu game (Start Screen) - Build Index 0.
    /// Thiết kế theo phong cách hoạt hình (cartoon-styled), isometric low-poly:
    /// Nền chuyển màu cam ấm, logo "TANKS!" màu trắng bóng đổ đen nổi bật,
    /// các trường nhập thông tin bo tròn dạng viên thuốc (pill-shaped), nút PLAY màu vàng lớn.
    /// Toàn bộ UI được sinh tự động bằng mã nguồn (code-only UI generation).
    /// </summary>
    [ExecuteAlways]
    public class StartScreenController : MonoBehaviour
    {
        [Tooltip("Tiêu đề logo game.")]
        public string m_Title = "TANKS!";
        [Tooltip("Tên Scene tiếp theo sẽ tải sau khi đăng nhập thành công.")]
        public string m_NextScene = "MainMenu";

        [Header("UI Text Labels")]
        [Tooltip("Gợi ý trong ô nhập tài khoản.")]
        public string m_UsernamePlaceholder = "USERNAME";
        [Tooltip("Gợi ý trong ô nhập mật khẩu.")]
        public string m_PasswordPlaceholder = "PASSWORD";
        [Tooltip("Nội dung hiển thị trên nút chơi.")]
        public string m_PlayButtonText = "PLAY!";
        [Tooltip("Tiêu đề của hộp thoại đăng nhập.")]
        public string m_CalloutText = "LOGIN";
        [Tooltip("Liên kết ở dưới cùng giao diện.")]
        public string m_BottomLinksText = "<u>Sign up</u>   ·   <u>Forgot password?</u>";

        [Header("UI Layout Settings")]
        [Tooltip("Kích thước khung bảng chính.")]
        public Vector2 m_PanelSize = new Vector2(620, 840);
        [Tooltip("Khoảng cách giữa các phần tử UI.")]
        public float m_Spacing = 20f;
        [Tooltip("Chiều cao phần tiêu đề.")]
        public float m_TitleHeight = 200f;
        [Tooltip("Cỡ chữ tiêu đề.")]
        public float m_TitleFontSize = 180f;
        [Tooltip("Chiều cao của nhãn callout đăng nhập.")]
        public float m_CalloutHeight = 80f;
        [Tooltip("Chiều rộng của nhãn callout đăng nhập.")]
        public float m_CalloutWidth = 300f;
        [Tooltip("Cỡ chữ callout đăng nhập.")]
        public float m_CalloutFontSize = 40f;
        [Tooltip("Chiều cao của ô nhập tài khoản/mật khẩu.")]
        public float m_InputHeight = 95f;
        [Tooltip("Cỡ chữ của ô nhập tài khoản/mật khẩu.")]
        public float m_InputFontSize = 30f;
        [Tooltip("Chiều cao nút Play.")]
        public float m_PlayButtonHeight = 120f;
        [Tooltip("Cỡ chữ nút Play.")]
        public float m_PlayButtonFontSize = 56f;
        [Tooltip("Chiều cao của dòng liên kết dưới cùng.")]
        public float m_BottomLinksHeight = 40f;
        [Tooltip("Cỡ chữ dòng liên kết dưới cùng.")]
        public float m_BottomLinksFontSize = 22f;

        [Header("UI Color Palette")]
        public Color m_BgTop = new Color(0.914f, 0.576f, 0.176f); // #E9932D
        public Color m_BgBottom = new Color(0.851f, 0.498f, 0.122f); // #D97F1F
        public Color m_HorizonCol = new Color(0.286f, 0.157f, 0.078f, 0.28f); // Màu bóng cồn cát phía dưới
        public Color m_TitleFill = Color.white;
        public Color m_TitleShadow = new Color(0.173f, 0.094f, 0.063f); // #2C1810
        public Color m_CyanCallout = new Color(0.227f, 0.706f, 0.851f); // #3AB4D9
        public Color m_PlayButtonColor = new Color(0.961f, 0.820f, 0.251f); // #F5D140
        public Color m_OutlineColor = new Color(0.173f, 0.094f, 0.063f); // #2C1810
        public Color m_InputFill = Color.white;
        public Color m_TextDark = new Color(0.173f, 0.094f, 0.063f);
        public Color m_HintGray = new Color(0.6f, 0.6f, 0.6f);

        [Header("Audio Settings")]
        [SerializeField] private AudioClip m_BackgroundMusic;
        [SerializeField] private AudioClip m_ClickSound;

        public enum UIState
        {
            Login,
            Register
        }
        private UIState m_CurrentState = UIState.Login;

        private TMP_InputField m_UsernameField;
        private TMP_InputField m_PasswordField;
        private TMP_InputField m_ConfirmPasswordField;
        private Button m_PlayButton;
        private bool m_Loading;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                EnsureEventSystem();
                EnsureUGSManager();

                if (m_BackgroundMusic == null)
                {
#if UNITY_EDITOR
                    m_BackgroundMusic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Tanks/Audio/Music/Music_Mysterious.ogg");
                    m_ClickSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Tanks/Audio/SFX/PickupPowerUp.wav");
#endif
                }

                if (m_BackgroundMusic != null)
                {
                    MusicManager.PlayMusic(m_BackgroundMusic);
                }
            }
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;

                var ugs = UGSManager.Instance;
                if (ugs != null)
                {
                    ugs.OnPlayerSignedIn += HandleAutoLogin;
                    if (ugs.IsSignedIn)
                    {
                        HandleAutoLogin(ugs.PlayerId);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            var ugs = UGSManager.Instance;
            if (ugs != null)
            {
                ugs.OnPlayerSignedIn -= HandleAutoLogin;
            }
        }

        private void HandleAutoLogin(string playerId)
        {
            Debug.Log($"[StartScreen] Tự động đăng nhập thành công. Chuyển tới MainMenu cho Player ID: {playerId}");
            if (Application.CanStreamedLevelBeLoaded(m_NextScene))
            {
                SceneManager.LoadScene(m_NextScene);
            }
        }

        private void EnsureUGSManager()
        {
            if (FindObject<UGSManager>() == null)
                new GameObject("UGSManager", typeof(UGSManager));
        }

        private void OnEnable() => BuildUI();
        private void OnDisable() => CleanUI();

        private void CleanUI()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall -= RebuildUIInEditor;
            UnityEditor.EditorApplication.delayCall += RebuildUIInEditor;
        }

        private void RebuildUIInEditor()
        {
            if (this == null) return;
            BuildUI();
        }
#endif

        /// <summary>
        /// Xử lý sự kiện đăng nhập hoặc đăng ký thông qua UGS Authentication.
        /// </summary>
        private async void OnPlayClicked()
        {
            if (m_Loading) return;

            string username = m_UsernameField.text.Trim();
            string password = m_PasswordField.text.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                Debug.LogWarning("[StartScreenController] Tên đăng nhập và mật khẩu không được để trống.");
                return;
            }

            if (m_CurrentState == UIState.Register)
            {
                string confirmPassword = m_ConfirmPasswordField.text.Trim();
                if (password != confirmPassword)
                {
                    Debug.LogError("[Auth] Lỗi: Mật khẩu xác nhận không trùng khớp.");
                    return;
                }
            }

            m_Loading = true;

            try
            {
                var authService = Unity.Services.Authentication.AuthenticationService.Instance;

                Debug.Log("[Auth] Bước 1/4: Kiểm tra trạng thái phiên đăng nhập cũ...");
                if (authService.IsSignedIn)
                {
                    Debug.Log($"[Auth] Phát hiện người chơi đã đăng nhập trước đó (Player ID: {authService.PlayerId}). Đang đăng xuất...");
                    authService.SignOut();
                }

                if (m_CurrentState == UIState.Login)
                {
                    Debug.Log($"[Auth] Bước 2/4: Đăng nhập tài khoản: '{username}'...");
                    await authService.SignInWithUsernamePasswordAsync(username, password);
                }
                else
                {
                    Debug.Log($"[Auth] Bước 2/4: Đăng ký tài khoản: '{username}'...");
                    await authService.SignUpWithUsernamePasswordAsync(username, password);
                }

                Debug.Log($"[Auth] Bước 3/4: Xác thực thành công! Player ID: {authService.PlayerId}");
                Debug.Log($"[Auth] Bước 4/4: Tải màn chơi tiếp theo: '{m_NextScene}'...");

                if (Application.CanStreamedLevelBeLoaded(m_NextScene))
                {
                    SceneManager.LoadScene(m_NextScene);
                }
                else
                {
                    Debug.LogError($"[Auth] Cảnh '{m_NextScene}' chưa được thêm vào Build Settings.");
                    m_Loading = false;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Auth] Giao dịch xác thực thất bại: {e.Message}");
                m_Loading = false;
            }
        }

        private void OnLinkClicked(string linkId)
        {
            m_CurrentState = linkId == "signup" ? UIState.Register : UIState.Login;
            BuildUI();
        }

        /// <summary>
        /// Dựng toàn bộ cấu trúc UI của màn hình Start.
        /// </summary>
        private void BuildUI()
        {
            CleanUI();

            // Khởi tạo Camera tạm thời nếu không có Camera nào rendering trong Scene
            Camera existingCamera = Camera.main ?? FindObject<Camera>();
            if (existingCamera == null)
            {
                var camGo = CreateElement(transform, "StartScreenCamera", typeof(Camera));
                var cam = camGo.GetComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = m_BgBottom;
                cam.orthographic = true;
                cam.depth = -1;
            }

            // Khởi tạo Canvas và các scaler đi kèm
            var canvasGo = CreateElement(transform, "StartCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            BuildBackground(canvasGo.transform);
            BuildFormPanel(canvasGo.transform);
        }

        /// <summary>
        /// Tạo lớp nền chuyển màu và cồn cát lượn sóng phía dưới.
        /// </summary>
        private void BuildBackground(Transform canvasRoot)
        {
            var bgImg = CreateImage(canvasRoot, "Background", CreateVerticalGradientSprite(m_BgTop, m_BgBottom));
            StretchFull(bgImg.rectTransform);

            var duneImg = CreateImage(canvasRoot, "Dunes", CreateDuneSilhouetteSprite(1024, 128, m_HorizonCol));
            duneImg.preserveAspect = false;
            var duneRt = duneImg.rectTransform;
            duneRt.anchorMin = new Vector2(0f, 0f);
            duneRt.anchorMax = new Vector2(1f, 0f);
            duneRt.pivot = new Vector2(0.5f, 0f);
            duneRt.offsetMin = Vector2.zero;
            duneRt.offsetMax = new Vector2(0f, 220f);
        }

        /// <summary>
        /// Tạo khung bảng điền thông tin (Form Panel) chứa logo, ô nhập liệu và nút bấm.
        /// </summary>
        private void BuildFormPanel(Transform canvasRoot)
        {
            var panel = CreateElement(canvasRoot, "FormPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = m_PanelSize;

            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = m_Spacing;
            vlg.childControlWidth = vlg.childControlHeight = vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            CreateShadowedTitle(panel.transform, m_Title);

            string calloutText = m_CurrentState == UIState.Login ? m_CalloutText : "REGISTER";
            CreateCyanCallout(panel.transform, calloutText);

            CreateSpacer(panel.transform, 12f);

            string userPlaceholder = m_CurrentState == UIState.Login ? m_UsernamePlaceholder : "NEW USERNAME";
            string passPlaceholder = m_CurrentState == UIState.Login ? m_PasswordPlaceholder : "NEW PASSWORD";
            m_UsernameField = CreatePillInput(panel.transform, userPlaceholder, "ti-user", false);
            m_PasswordField = CreatePillInput(panel.transform, passPlaceholder, "ti-lock", true);

            if (m_CurrentState == UIState.Register)
            {
                m_ConfirmPasswordField = CreatePillInput(panel.transform, "CONFIRM PASSWORD", "ti-lock", true);
            }
            else
            {
                m_ConfirmPasswordField = null;
            }

            CreateSpacer(panel.transform, 8f);

            string actionText = m_CurrentState == UIState.Login ? m_PlayButtonText : "SIGN UP!";
            m_PlayButton = CreatePlayButton(panel.transform, actionText, OnPlayClicked);

            CreateBottomLinks(panel.transform);

            // Bắt sự kiện ấn Enter để tiến hành Đăng nhập/Đăng ký nhanh
            m_UsernameField.onSubmit.AddListener(_ => OnPlayClicked());
            m_PasswordField.onSubmit.AddListener(_ => OnPlayClicked());
            m_ConfirmPasswordField?.onSubmit.AddListener(_ => OnPlayClicked());
        }

        private void CreateShadowedTitle(Transform parent, string text)
        {
            var wrapper = CreateLayoutGroupItem(parent, "Title", m_TitleHeight);

            // Chữ đổ bóng phía sau, lệch (10, -10)
            var shadow = CreateTMP(wrapper, text + "_Shadow", text, m_TitleFontSize, FontStyles.Bold | FontStyles.Italic, m_TitleShadow);
            shadow.rectTransform.anchoredPosition = new Vector2(10f, -10f);

            // Chữ màu trắng phía trước
            var fore = CreateTMP(wrapper, "Title_Fore", text, m_TitleFontSize, FontStyles.Bold | FontStyles.Italic, m_TitleFill);
            fore.rectTransform.anchoredPosition = Vector2.zero;
        }

        private void CreateCyanCallout(Transform parent, string text)
        {
            var rt = CreateLayoutGroupItem(parent, "Callout", m_CalloutHeight);
            rt.sizeDelta = new Vector2(m_CalloutWidth, m_CalloutHeight);

            CreateShadow(rt, 64, 30, new Vector2(6f, -8f), new Vector2(6f, -2f));

            var fillImg = CreateImage(rt, "Fill", CreateRoundedRectSprite(64, 30, m_CyanCallout, m_OutlineColor, 4), Image.Type.Sliced);
            StretchFull(fillImg.rectTransform);

            var label = CreateTMP(fillImg.transform, "Label", text, m_CalloutFontSize, FontStyles.Bold | FontStyles.Italic, Color.white);
            label.characterSpacing = 6f;
        }

        private TMP_InputField CreatePillInput(Transform parent, string placeholder, string leadingIconName, bool isPassword)
        {
            var rt = CreateLayoutGroupItem(parent, $"Input_{placeholder}", m_InputHeight);

            CreateShadow(rt, 96, 46, new Vector2(6f, -8f), new Vector2(6f, -2f));

            var fillGo = CreateElement(rt, "Fill", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            StretchFull(fillGo.GetComponent<RectTransform>());
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.sprite = CreateRoundedRectSprite(96, 46, m_InputFill, m_OutlineColor, 4);
            fillImg.type = Image.Type.Sliced;

            // Icon biểu tượng phía trước (Dùng ký tự ASCII tiêu chuẩn để tránh lỗi thiếu Font character)
            var iconTmp = CreateElement(fillGo.transform, "Icon", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            var iconRt = iconTmp.rectTransform;
            iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(30f, 0f);
            iconRt.sizeDelta = new Vector2(50f, 50f);
            iconTmp.text = leadingIconName == "ti-lock" ? "*" : "@";
            iconTmp.fontSize = 36f;
            iconTmp.color = m_HintGray;
            iconTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // Vùng hiển thị Text (TextArea) lệch về bên phải để chừa chỗ cho Icon
            var textArea = CreateElement(fillGo.transform, "TextArea", typeof(RectTransform), typeof(RectMask2D));
            var textAreaRt = textArea.GetComponent<RectTransform>();
            StretchFull(textAreaRt);
            textAreaRt.offsetMin = new Vector2(90f, 12f);
            textAreaRt.offsetMax = new Vector2(-40f, -12f);

            var placeholderTmp = CreateTMP(textArea.transform, "Placeholder", placeholder, m_InputFontSize, FontStyles.Bold, new Color(0.55f, 0.55f, 0.55f), TextAlignmentOptions.MidlineLeft);
            var textTmp = CreateTMP(textArea.transform, "Text", "", m_InputFontSize, FontStyles.Bold, m_TextDark, TextAlignmentOptions.MidlineLeft);

            var input = fillGo.GetComponent<TMP_InputField>();
            input.textViewport = textAreaRt;
            input.textComponent = textTmp;
            input.placeholder = placeholderTmp;
            input.characterLimit = 30;
            input.contentType = isPassword ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;

            return input;
        }

        private Button CreatePlayButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var rt = CreateLayoutGroupItem(parent, "PlayButton", m_PlayButtonHeight);

            CreateShadow(rt, 120, 58, new Vector2(7f, -10f), new Vector2(7f, -3f), 0.4f);

            var fillGo = CreateElement(rt, "Fill", typeof(RectTransform), typeof(Image), typeof(Button));
            StretchFull(fillGo.GetComponent<RectTransform>());
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.sprite = CreateRoundedRectSprite(120, 58, m_PlayButtonColor, m_OutlineColor, 5);
            fillImg.type = Image.Type.Sliced;

            var button = fillGo.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
            colors.pressedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            
            var vibe = fillGo.AddComponent<UIButtonVibe>();
            vibe.clickSound = m_ClickSound;
            
            button.onClick.AddListener(onClick);

            var labelTmp = CreateTMP(fillGo.transform, "Label", label, m_PlayButtonFontSize, FontStyles.Bold | FontStyles.Italic, m_TextDark);
            labelTmp.characterSpacing = 8f;

            return button;
        }

        private void CreateBottomLinks(Transform parent)
        {
            var go = CreateElement(parent, "Links", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().preferredHeight = m_BottomLinksHeight;
            
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = m_CurrentState == UIState.Login 
                ? "<link=\"signup\"><u>Sign up</u></link>   ·   <u>Forgot password?</u>" 
                : "<link=\"login\"><u>Back to Login</u></link>";
            tmp.fontSize = m_BottomLinksFontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(1f, 1f, 1f, 0.92f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.richText = true;

            go.AddComponent<TMPLinkHandler>().Init(tmp, OnLinkClicked);
        }

        public class TMPLinkHandler : MonoBehaviour, IPointerClickHandler
        {
            private TextMeshProUGUI m_Text;
            private System.Action<string> m_OnLinkClicked;

            public void Init(TextMeshProUGUI text, System.Action<string> onLinkClicked)
            {
                m_Text = text;
                m_OnLinkClicked = onLinkClicked;
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                int linkIndex = TMP_TextUtilities.FindIntersectingLink(m_Text, eventData.position, eventData.pressEventCamera);
                if (linkIndex != -1)
                {
                    m_OnLinkClicked?.Invoke(m_Text.textInfo.linkInfo[linkIndex].GetLinkID());
                }
            }
        }

        private static void CreateSpacer(Transform parent, float height)
        {
            CreateLayoutGroupItem(parent, "Spacer", height);
        }

        #region UI Helper Methods (Các phương thức bổ trợ tạo UI)

        private static T FindObject<T>() where T : Object
        {
#if UNITY_2022_1_OR_NEWER
            return FindAnyObjectByType<T>();
#else
            return FindObjectOfType<T>();
#endif
        }

        private static GameObject CreateElement(Transform parent, string name, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static RectTransform CreateLayoutGroupItem(Transform parent, string name, float height)
        {
            var go = CreateElement(parent, name, typeof(RectTransform), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().preferredHeight = height;
            return (RectTransform)go.transform;
        }

        private static Image CreateImage(Transform parent, string name, Sprite sprite, Image.Type type = Image.Type.Simple)
        {
            var img = CreateElement(parent, name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.sprite = sprite;
            img.type = type;
            return img;
        }

        private static Image CreateShadow(Transform parent, int size, int radius, Vector2 offsetMin, Vector2 offsetMax, float alpha = 0.35f)
        {
            var img = CreateImage(parent, "Shadow", CreateRoundedRectSprite(size, radius, new Color(0f, 0f, 0f, alpha), Color.clear, 0), Image.Type.Sliced);
            var rt = img.rectTransform;
            StretchFull(rt);
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return img;
        }

        private static TextMeshProUGUI CreateTMP(Transform parent, string name, string text, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var tmp = CreateElement(parent, name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            StretchFull(tmp.rectTransform);
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        #endregion

        #region Sprite Generator Methods (Các phương thức sinh Texture/Sprite bằng CPU)

        private static Sprite CreateVerticalGradientSprite(Color top, Color bottom)
        {
            const int h = 256;
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            {
                var c = Color.Lerp(bottom, top, y / (float)(h - 1));
                tex.SetPixel(0, y, c);
                tex.SetPixel(1, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateRoundedRectSprite(int size, int radius, Color fill, Color border, int borderWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = 0f, dy = 0f;
                    if (x < radius) dx = radius - x;
                    else if (x >= size - radius) dx = x - (size - radius - 1);
                    if (y < radius) dy = radius - y;
                    else if (y >= size - radius) dy = y - (size - radius - 1);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    Color c;
                    if (dist > radius) c = Color.clear;
                    else if (borderWidth > 0 && dist > radius - borderWidth) c = border;
                    else c = fill;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            var b = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, b);
        }

        private static Sprite CreateDuneSilhouetteSprite(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var clear = new Color(0f, 0f, 0f, 0f);

            for (int x = 0; x < width; x++)
            {
                float t = x / (float)width;
                float wave = Mathf.Sin(t * Mathf.PI * 2f) * 0.18f
                             + Mathf.Sin(t * Mathf.PI * 5f + 1.3f) * 0.09f
                             + Mathf.Sin(t * Mathf.PI * 11f + 2.7f) * 0.04f;
                int topY = Mathf.Clamp(Mathf.RoundToInt((0.55f + wave) * height), 1, height - 1);
                for (int y = 0; y < height; y++)
                    tex.SetPixel(x, y, y <= topY ? color : clear);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0f));
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        #endregion
    }
}
