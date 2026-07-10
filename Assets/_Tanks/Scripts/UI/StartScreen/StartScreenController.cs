using System.Collections.Generic;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
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
        [Tooltip("Chiều cao của dòng thông báo lỗi/trạng thái.")]
        public float m_StatusHeight = 56f;
        [Tooltip("Cỡ chữ của dòng thông báo lỗi/trạng thái.")]
        public float m_StatusFontSize = 22f;
        
        [Header("Background Video (Optional)")]
        [Tooltip("Video nền của màn hình Start. Kéo thả VideoClip vào đây. Nếu để trống sẽ dùng ảnh mặc định.")]
        public VideoClip m_BackgroundVideo;

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
        public Color m_ErrorColor = new Color(0.451f, 0.043f, 0.043f);   // #730B0B
        public Color m_SuccessColor = new Color(0.075f, 0.290f, 0.129f); // #134A21
        public Color m_InfoColor = new Color(0.173f, 0.094f, 0.063f);

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

        private TextMeshProUGUI m_StatusLabel;

        // Mọi ô mật khẩu đang hiển thị, để nút con mắt bật/tắt tất cả cùng lúc.
        private readonly List<TMP_InputField> m_PasswordFields = new List<TMP_InputField>();
        private readonly List<TextMeshProUGUI> m_RevealLabels = new List<TextMeshProUGUI>();
        private bool m_PasswordVisible;

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
            var ugs = FindObject<UGSManager>();
            if (ugs == null)
            {
                var go = new GameObject("UGSManager", typeof(UGSManager));
                go.AddComponent<ProfileManager>();
            }
            else
            {
                if (ugs.GetComponent<ProfileManager>() == null)
                {
                    ugs.gameObject.AddComponent<ProfileManager>();
                }
            }
        }

        private void OnEnable() => BuildUI();
        private void OnDisable() => CleanUI();

        private void CleanUI()
        {
            m_PasswordFields.Clear();
            m_RevealLabels.Clear();
            m_StatusLabel = null;

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

            string username = StripInvisible(m_UsernameField.text).Trim();
            string password = StripInvisible(m_PasswordField.text);

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                SetStatus("Vui lòng nhập cả tên đăng nhập và mật khẩu.", m_ErrorColor);
                return;
            }

            // Không tự cắt dấu cách của mật khẩu: một tài khoản có thể đã được đăng ký kèm dấu cách,
            // cắt đi sẽ khiến nó vĩnh viễn không đăng nhập lại được. Báo cho người dùng thay vì sửa ngầm.
            if (password != password.Trim())
            {
                SetStatus("Mật khẩu có dấu cách ở đầu hoặc cuối. Bấm HIỆN để kiểm tra lại.", m_ErrorColor);
                return;
            }

            if (m_CurrentState == UIState.Register)
            {
                string confirmPassword = StripInvisible(m_ConfirmPasswordField.text);
                if (password != confirmPassword)
                {
                    SetStatus("Mật khẩu xác nhận không trùng khớp.", m_ErrorColor);
                    return;
                }
            }

            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                SetStatus("Đang kết nối tới máy chủ, vui lòng thử lại sau vài giây.", m_InfoColor);
                return;
            }

            m_Loading = true;
            SetStatus(m_CurrentState == UIState.Login ? "Đang đăng nhập..." : "Đang tạo tài khoản...", m_InfoColor);

            try
            {
                var authService = AuthenticationService.Instance;

                // Ghi lại đặc điểm chuỗi (không ghi chính mật khẩu) để lỗi sai mật khẩu lộ ra ngay ở log.
                Debug.Log($"[Auth] Bước 1/4: username='{username}' ({username.Length} ký tự), " +
                          $"mật khẩu {password.Length} ký tự.");

                if (authService.IsSignedIn)
                {
                    Debug.Log($"[Auth] Phát hiện phiên cũ của Player ID {authService.PlayerId}. Đang xoá hoàn toàn...");
                    authService.SignOut(true);
                }

                if (authService.SessionTokenExists)
                    authService.ClearSessionToken();

                if (m_CurrentState == UIState.Login)
                {
                    Debug.Log($"[Auth] Bước 2/4: Đăng nhập tài khoản: '{username}'...");
                    await authService.SignInWithUsernamePasswordAsync(username, password);
                }
                else
                {
                    Debug.Log($"[Auth] Bước 2/4: Đăng ký tài khoản: '{username}'...");
                    await authService.SignUpWithUsernamePasswordAsync(username, password);

                    string registeredPlayerId = authService.PlayerId;
                    Debug.Log($"[Auth] Đăng ký thành công Player ID: {registeredPlayerId}. Đang xác minh đăng nhập lại...");

                    // Do not trust only the cached session created during sign-up.
                    // Verify that the new credentials can really authenticate again.
                    authService.SignOut(true);
                    await authService.SignInWithUsernamePasswordAsync(username, password);

                    if (authService.PlayerId != registeredPlayerId)
                        throw new System.InvalidOperationException(
                            "Tài khoản xác minh trả về Player ID khác với tài khoản vừa đăng ký.");

                    Debug.Log($"[Auth] Xác minh tài khoản '{username}' thành công.");
                }

                Debug.Log($"[Auth] Bước 3/4: Xác thực thành công! Player ID: {authService.PlayerId}");
                SetStatus("Thành công! Đang vào game...", m_SuccessColor);

                if (ProfileManager.Instance != null)
                {
                    Debug.Log("[Auth] Đang tải Profile từ Cloud Save...");
                    await ProfileManager.Instance.LoadProfileAsync();
                }

                Debug.Log($"[Auth] Bước 4/4: Tải màn chơi tiếp theo: '{m_NextScene}'...");

                if (Application.CanStreamedLevelBeLoaded(m_NextScene))
                {
                    SceneManager.LoadScene(m_NextScene);
                }
                else
                {
                    Debug.LogError($"[Auth] Cảnh '{m_NextScene}' chưa được thêm vào Build Settings.");
                    SetStatus($"Lỗi cấu hình: cảnh '{m_NextScene}' chưa có trong Build Settings.", m_ErrorColor);
                    m_Loading = false;
                }
            }
            catch (System.Exception e)
            {
                string friendly = DescribeAuthError(e, m_CurrentState);
                Debug.LogError($"[Auth] Xác thực thất bại: {friendly} (gốc: {e.GetType().Name} - {e.Message})");
                SetStatus(friendly, m_ErrorColor);
                m_Loading = false;
            }
        }

        /// <summary>
        /// Bỏ các ký tự vô hình mà TMP hoặc thao tác copy/paste chèn vào, nhưng GIỮ NGUYÊN dấu cách thật.
        /// </summary>
        private static string StripInvisible(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            return raw
                .Replace("\u200B", "")  // zero-width space
                .Replace("\u200E", "")  // left-to-right mark
                .Replace("\u200F", "")  // right-to-left mark
                .Replace("\uFEFF", "")  // byte-order mark
                .Trim('\r', '\n');
        }

        /// <summary>
        /// Dịch lỗi xác thực của UGS sang thông báo tiếng Việt.
        ///
        /// Unity CỐ Ý không phân biệt "sai tên đăng nhập" với "sai mật khẩu": cả hai đều trả về
        /// ErrorCode 10002 (InvalidParameters) kèm cùng câu "Invalid username or password", nhằm chống
        /// dò tài khoản. Các trường hợp còn lại chỉ tách được bằng cách so chuỗi Detail của server,
        /// nên phần so chuỗi dưới đây sẽ vỡ nếu Unity đổi câu chữ - luôn giữ nhánh mặc định trả message gốc.
        /// </summary>
        private static string DescribeAuthError(System.Exception exception, UIState state)
        {
            if (exception is AuthenticationException authException)
            {
                int code = authException.ErrorCode;

                if (code == AuthenticationErrorCodes.BannedUser)
                    return "Tài khoản này đã bị khoá.";

                if (code == AuthenticationErrorCodes.ClientInvalidUserState)
                    return "Vẫn còn một phiên đăng nhập cũ. Hãy thử lại.";

                if (code == AuthenticationErrorCodes.InvalidSessionToken)
                    return "Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại.";

                if (code == AuthenticationErrorCodes.EnvironmentMismatch)
                    return "Sai môi trường UGS (Environment). Kiểm tra Project Settings > Services.";

                if (code == AuthenticationErrorCodes.InvalidParameters)
                {
                    string detail = authException.Message ?? string.Empty;

                    if (detail.Contains("already exists"))
                        return "Tên đăng nhập này đã có người dùng. Hãy chọn tên khác.";

                    if (detail.Contains("Invalid username or password"))
                        return state == UIState.Login
                            ? "Sai tên đăng nhập hoặc mật khẩu. (Unity không cho biết sai cái nào.)"
                            : "Tên đăng nhập hoặc mật khẩu không hợp lệ.";

                    if (detail.Contains("format"))
                        return "Sai định dạng. Tên đăng nhập: 3-20 ký tự (chữ, số, . - @ _). " +
                               "Mật khẩu: 8-30 ký tự, đủ chữ hoa, chữ thường, số và ký tự đặc biệt.";

                    return $"Thông tin không hợp lệ: {detail}";
                }

                return $"Lỗi xác thực ({code}): {authException.Message}";
            }

            if (exception is RequestFailedException requestException)
            {
                int code = requestException.ErrorCode;

                if (code == CommonErrorCodes.TransportError)
                    return "Không kết nối được máy chủ. Kiểm tra mạng hoặc tắt VPN.";

                if (code == CommonErrorCodes.Timeout)
                    return "Máy chủ phản hồi quá chậm. Thử lại sau.";

                if (code == CommonErrorCodes.ServiceUnavailable)
                    return "Dịch vụ Unity đang bảo trì. Thử lại sau.";

                if (code == CommonErrorCodes.TooManyRequests)
                    return "Bạn thử quá nhiều lần. Chờ một lát rồi thử lại.";

                return $"Yêu cầu thất bại ({code}): {requestException.Message}";
            }

            if (exception is ServicesInitializationException)
                return "Dịch vụ Unity chưa khởi tạo xong. Thử lại sau vài giây.";

            return exception.Message;
        }

        private void SetStatus(string message, Color color)
        {
            if (m_StatusLabel == null) return;
            m_StatusLabel.text = message;
            m_StatusLabel.color = color;
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
            if (m_BackgroundVideo != null)
            {
                var videoGo = CreateElement(canvasRoot, "VideoBackground", typeof(VideoPlayer));
                var vp = videoGo.GetComponent<VideoPlayer>();
                vp.playOnAwake = true;
                vp.clip = m_BackgroundVideo;
                vp.renderMode = VideoRenderMode.CameraFarPlane;
                vp.targetCamera = Camera.main;
                if (vp.targetCamera == null)
                {
#if UNITY_2023_1_OR_NEWER
                    vp.targetCamera = FindFirstObjectByType<Camera>();
#else
                    vp.targetCamera = FindObjectOfType<Camera>();
#endif
                }
                vp.isLooping = false; // Tắt lặp lại để giữ nguyên cảnh kính vỡ cuối cùng
                
                // Mẹo tắt tiếng 100%: Chuyển âm thanh sang AudioSource nhưng không cung cấp AudioSource nào cả
                vp.audioOutputMode = VideoAudioOutputMode.AudioSource; 
                vp.SetTargetAudioSource(0, null);
                
                // Trả về luôn để không vẽ đè ảnh nền che mất video
                return;
            }
            
            // Nếu Inspector chưa gán Video, thì thử tải ảnh mặc định từ Resources
            Sprite desertBg = Resources.Load<Sprite>("background_desert");
            if (desertBg == null)
            {
                var sprites = Resources.LoadAll<Sprite>("background_desert");
                if (sprites != null && sprites.Length > 0)
                {
                    desertBg = sprites[0];
                }
            }

            if (desertBg != null)
            {
                var bgImg = CreateImage(canvasRoot, "Background", desertBg);
                bgImg.preserveAspect = false; // Tràn viền
                StretchFull(bgImg.rectTransform);
            }
            else
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
        }

        /// <summary>
        /// Tạo khung bảng điền thông tin (Form Panel) chứa logo, ô nhập liệu và nút bấm.
        /// </summary>
        private void BuildFormPanel(Transform canvasRoot)
        {
            var panel = CreateElement(canvasRoot, "FormPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
            var panelRt = panel.GetComponent<RectTransform>();
            
            // Dời toàn bộ khung nhập liệu sang bên phải màn hình
            panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(1f, 0.5f);
            panelRt.anchoredPosition = new Vector2(-150, 0); // Cách lề phải 150px
            panelRt.localScale = new Vector3(0.85f, 0.85f, 1f); // Thu nhỏ lại một chút cho gọn
            panelRt.sizeDelta = m_PanelSize;

            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = m_Spacing;
            vlg.childControlWidth = vlg.childControlHeight = vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            CreateShadowedTitle(panel.transform, m_Title);

            // Đã bỏ khối CreateCyanCallout ("LOGIN") theo yêu cầu

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

            ApplyPasswordVisibility();

            m_StatusLabel = CreateStatusLabel(panel.transform);

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

            // Vùng hiển thị Text (TextArea) lệch về bên phải để chừa chỗ cho Icon.
            // Ô mật khẩu chừa thêm chỗ bên phải cho nút HIỆN/ẨN.
            var textArea = CreateElement(fillGo.transform, "TextArea", typeof(RectTransform), typeof(RectMask2D));
            var textAreaRt = textArea.GetComponent<RectTransform>();
            StretchFull(textAreaRt);
            textAreaRt.offsetMin = new Vector2(90f, 12f);
            textAreaRt.offsetMax = new Vector2(isPassword ? -135f : -40f, -12f);

            var placeholderTmp = CreateTMP(textArea.transform, "Placeholder", placeholder, m_InputFontSize, FontStyles.Bold, new Color(0.55f, 0.55f, 0.55f), TextAlignmentOptions.MidlineLeft);
            var textTmp = CreateTMP(textArea.transform, "Text", "", m_InputFontSize, FontStyles.Bold, m_TextDark, TextAlignmentOptions.MidlineLeft);

            var input = fillGo.GetComponent<TMP_InputField>();
            input.textViewport = textAreaRt;
            input.textComponent = textTmp;
            input.placeholder = placeholderTmp;
            input.characterLimit = 30;

            // contentType phải đặt trước, vì setter của nó ghi đè lineType/inputType/characterValidation.
            input.contentType = isPassword ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;

            // Trên macOS, Esc mặc định khôi phục chuỗi cũ và click vào ô sẽ bôi đen toàn bộ;
            // cả hai đều gây mất chữ ngoài ý muốn khi người dùng đang sửa mật khẩu.
            input.restoreOriginalTextOnEscape = false;
            input.onFocusSelectAll = false;
            input.caretWidth = 2;
            input.customCaretColor = true;
            input.caretColor = m_TextDark;
            input.selectionColor = new Color(0.227f, 0.706f, 0.851f, 0.45f);

            if (isPassword)
            {
                m_PasswordFields.Add(input);
                CreateRevealButton(fillGo.transform);
            }

            return input;
        }

        /// <summary>
        /// Nút HIỆN/ẨN nằm bên phải ô mật khẩu. Một nút bất kỳ sẽ bật/tắt mọi ô mật khẩu cùng lúc,
        /// để ô PASSWORD và CONFIRM PASSWORD luôn ở cùng trạng thái.
        /// </summary>
        private void CreateRevealButton(Transform parent)
        {
            var go = CreateElement(parent, "RevealButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-24f, 0f);
            rt.sizeDelta = new Vector2(96f, 52f);

            var img = go.GetComponent<Image>();
            img.sprite = CreateRoundedRectSprite(48, 22, new Color(0.93f, 0.93f, 0.93f), m_HintGray, 2);
            img.type = Image.Type.Sliced;

            var label = CreateTMP(go.transform, "Label", "", 20f, FontStyles.Bold, m_TextDark);
            m_RevealLabels.Add(label);

            var button = go.GetComponent<Button>();
            button.targetGraphic = img; // Button tạo bằng code không tự gán, thiếu nó thì bấm không đổi màu.
            button.onClick.AddListener(TogglePasswordVisibility);
        }

        private void TogglePasswordVisibility()
        {
            m_PasswordVisible = !m_PasswordVisible;
            ApplyPasswordVisibility();
        }

        private void ApplyPasswordVisibility()
        {
            foreach (var field in m_PasswordFields)
            {
                if (field == null) continue;

                field.contentType = m_PasswordVisible
                    ? TMP_InputField.ContentType.Standard
                    : TMP_InputField.ContentType.Password;

                // Bắt buộc: đổi contentType không tự vẽ lại chuỗi đang hiển thị.
                field.ForceLabelUpdate();
            }

            foreach (var label in m_RevealLabels)
            {
                if (label != null) label.text = m_PasswordVisible ? "ẨN" : "HIỆN";
            }
        }

        private TextMeshProUGUI CreateStatusLabel(Transform parent)
        {
            var rt = CreateLayoutGroupItem(parent, "StatusLabel", m_StatusHeight);
            var tmp = CreateTMP(rt, "Text", "", m_StatusFontSize, FontStyles.Bold, m_ErrorColor);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
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

            go.AddComponent<StartScreenLinkHandler>().Init(tmp, OnLinkClicked);
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
