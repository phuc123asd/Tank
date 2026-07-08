using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tanks.Complete
{
    /// <summary>
    /// Trình quản lý Main Menu của game, tự động sinh UI bằng code.
    /// Giữ nguyên phong cách (vibe) Arcade vui nhộn của màn hình StartScreen (Sa mạc, nút viền đen, đổ bóng).
    /// Bao gồm: Trang Chủ (Home), Chọn Chế Độ (ModeSelect), và Phòng Chờ 5v5 (Lobby5v5).
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        public enum MenuState { Home, ModeSelect, Lobby5v5 }

        private MenuState m_CurrentState = MenuState.Home;

        [SerializeField] private GameObject m_HomePanel;
        [SerializeField] private GameObject m_ModeSelectPanel;
        [SerializeField] private GameObject m_Lobby5v5Panel;

        // --- Bảng màu chuẩn Vibe "Tanks!" ---
        private readonly Color m_BgColor = new Color(0.85f, 0.51f, 0.16f);       // Cam nền
        private readonly Color m_DuneColor = new Color(0.75f, 0.42f, 0.12f);     // Cam đậm đụn cát
        private readonly Color m_OutlineColor = new Color(0.15f, 0.15f, 0.15f);  // Đen viền nút
        private readonly Color m_PlayButtonColor = new Color(0.95f, 0.85f, 0.35f); // Vàng nút Play
        private readonly Color m_CyanColor = new Color(0.25f, 0.70f, 0.85f);     // Xanh biển Cyan
        private readonly Color m_CardColor1 = new Color(0.70f, 0.30f, 0.30f);    // Đỏ nhạt
        private readonly Color m_CardColor2 = new Color(0.30f, 0.62f, 0.32f);    // Xanh lá
        private readonly Color m_TextDark = new Color(0.2f, 0.2f, 0.2f);         // Chữ đen xám

        [Header("Audio Settings")]
        [SerializeField] private AudioClip m_MusicHome;
        [SerializeField] private AudioClip m_MusicModeSelect;
        [SerializeField] private AudioClip m_MusicLobby;
        [SerializeField] private AudioClip m_ClickSound;

        private void Awake()
        {
            EnsureEventSystem();
            
            if (m_MusicHome == null)
            {
#if UNITY_EDITOR
                m_MusicHome = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Tanks/Audio/Music/Music_Western.ogg");
                m_MusicModeSelect = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Tanks/Audio/Music/Music_Funky.ogg");
                m_MusicLobby = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Tanks/Audio/Music/Music_Steady.ogg");
#endif
            }

            if (m_ClickSound == null)
            {
#if UNITY_EDITOR
                m_ClickSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Tanks/Audio/SFX/PickupPowerUp.wav");
#endif
            }

            var existingCanvas = transform.Find("MainMenuCanvas");
            if (existingCanvas != null)
            {
                // Nếu UI đã được Bake lên Scene, chúng ta chỉ cần móc nối lại các sự kiện Button
                // (do Unity không lưu delegate AddListener khi thoát Editor).
                m_HomePanel = existingCanvas.Find("HomePanel").gameObject;
                m_ModeSelectPanel = existingCanvas.Find("ModeSelectPanel").gameObject;
                m_Lobby5v5Panel = existingCanvas.Find("Lobby5v5Panel").gameObject;
                // Dùng GetComponentsInChildren để quét tìm tất cả các nút bấm tự động, tránh lỗi sai đường dẫn
                var allButtons = existingCanvas.GetComponentsInChildren<Button>(true);
                foreach (var btn in allButtons)
                {
                    // Tự động gắn hiệu ứng click và âm thanh vào nút nếu trước đó UI được Bake mà chưa có
                    var vibe = btn.GetComponent<UIButtonVibe>();
                    if (vibe == null) vibe = btn.gameObject.AddComponent<UIButtonVibe>();
                    vibe.clickSound = m_ClickSound;

                    if (btn.transform.parent == null) continue;
                    string parentName = btn.transform.parent.name;

                    // Chỉ móc nối sự kiện chuyển trang cho những nút có tên tương ứng
                    if (parentName == "PillButton_LEO RANK")
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => UpdateState(MenuState.ModeSelect));
                    }
                    else if (parentName == "PillButton_< TRỞ VỀ")
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => UpdateState(MenuState.Home));
                    }
                    else if (parentName == "Card_TEAM COMBAT\n(5v5)")
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => UpdateState(MenuState.Lobby5v5));
                    }
                    else if (parentName == "PillButton_< RỜI PHÒNG")
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => UpdateState(MenuState.ModeSelect));
                    }
                }

                UpdateState(MenuState.Home);
            }
            else
            {
                BuildUI();
                UpdateState(MenuState.Home);
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildUI()
        {
            // Root canvas
            var canvasGo = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Full-screen Background (Màu cam)
            CreateImage(canvasGo.transform, "Background", CreateVerticalGradientSprite(m_BgColor, m_BgColor), Image.Type.Simple, true);

            // Cồn cát lượn sóng (Dune) ở dưới đáy màn hình
            var duneGo = CreateImage(canvasGo.transform, "Dune", CreateDuneSilhouetteSprite(1024, 200, m_DuneColor), Image.Type.Simple, false);
            var duneRt = duneGo.rectTransform;
            duneRt.anchorMin = Vector2.zero;
            duneRt.anchorMax = new Vector2(1f, 0f);
            duneRt.offsetMin = Vector2.zero;
            duneRt.offsetMax = new Vector2(0f, 200f);

            m_HomePanel = CreateHomePanel(canvasGo.transform);
            m_ModeSelectPanel = CreateModeSelectPanel(canvasGo.transform);
            m_Lobby5v5Panel = CreateLobby5v5Panel(canvasGo.transform);
        }

        private void UpdateState(MenuState newState)
        {
            m_CurrentState = newState;
            if (m_HomePanel) m_HomePanel.SetActive(m_CurrentState == MenuState.Home);
            if (m_ModeSelectPanel) m_ModeSelectPanel.SetActive(m_CurrentState == MenuState.ModeSelect);
            if (m_Lobby5v5Panel) m_Lobby5v5Panel.SetActive(m_CurrentState == MenuState.Lobby5v5);

            // Đổi nhạc nền dựa trên trang hiện tại qua MusicManager để đảm bảo liền mạch
            AudioClip nextClip = null;
            if (m_CurrentState == MenuState.Home) nextClip = m_MusicHome;
            else if (m_CurrentState == MenuState.ModeSelect) nextClip = m_MusicModeSelect;
            else if (m_CurrentState == MenuState.Lobby5v5) nextClip = m_MusicLobby;

            if (nextClip != null)
            {
                MusicManager.PlayMusic(nextClip);
            }
        }

        #region Màn hình (Panels)

        private GameObject CreateHomePanel(Transform parent)
        {
            var panel = CreatePanel("HomePanel", parent);

            // 1. Thanh Top Bar (Profile & Tiền)
            var topBar = CreateElement(panel.transform, "TopBar", typeof(RectTransform)).GetComponent<RectTransform>();
            topBar.anchorMin = topBar.anchorMax = topBar.pivot = new Vector2(0.5f, 1);
            topBar.anchoredPosition = new Vector2(0, -40);
            topBar.sizeDelta = new Vector2(1840, 80);

            var profileBox = CreateElement(topBar.transform, "ProfileBox", typeof(RectTransform)).GetComponent<RectTransform>();
            profileBox.anchorMin = profileBox.anchorMax = profileBox.pivot = new Vector2(0, 0.5f);
            profileBox.anchoredPosition = Vector2.zero;
            CreateCallout(profileBox, "PLAYER: OMG19891019  |  RANK: BẠCH KIM", m_CyanColor, 600, 70);

            var currencyBox = CreateElement(topBar.transform, "CurrencyBox", typeof(RectTransform)).GetComponent<RectTransform>();
            currencyBox.anchorMin = currencyBox.anchorMax = currencyBox.pivot = new Vector2(1, 0.5f);
            currencyBox.anchoredPosition = Vector2.zero;
            CreateCallout(currencyBox, "VÀNG: 15,000  |  QUÂN HUY: 250", m_PlayButtonColor, 600, 70);


            // 3. Logo (Giữ nguyên vị trí ban đầu ở giữa trên cùng)
            var titleRt = CreateElement(panel.transform, "LogoContainer", typeof(RectTransform)).GetComponent<RectTransform>();
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.75f);
            titleRt.anchoredPosition = Vector2.zero;
            CreateShadowedTitle(titleRt.transform, "TANKS!", 160f);

            // 4. Menu Cạnh Trái (Left Nav) - Định vị tuyệt đối để mở khóa kéo thả
            var leftNavGroup = CreateElement(panel.transform, "LeftNavGroup", typeof(RectTransform)).GetComponent<RectTransform>();
            leftNavGroup.anchorMin = leftNavGroup.anchorMax = leftNavGroup.pivot = new Vector2(0, 0.5f);
            leftNavGroup.anchoredPosition = new Vector2(60, -50);
            leftNavGroup.sizeDelta = new Vector2(250, 400);

            string[] leftNames = { "TƯỚNG", "CỬA HÀNG", "TÚI ĐỒ", "SỰ KIỆN" };
            Color[] leftColors = { m_CyanColor, m_PlayButtonColor, m_CardColor1, m_CardColor2 };
            for(int i = 0; i < 4; i++) {
                var btn = CreatePillButton(leftNavGroup.transform, leftNames[i], leftColors[i], 250, 70, 32, () => { });
                btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(125, 150 - i * 100);
            }

            // 5. Menu Cạnh Phải (Right Nav) - Định vị tuyệt đối
            var rightNavGroup = CreateElement(panel.transform, "RightNavGroup", typeof(RectTransform)).GetComponent<RectTransform>();
            rightNavGroup.anchorMin = rightNavGroup.anchorMax = rightNavGroup.pivot = new Vector2(1, 0.5f);
            rightNavGroup.anchoredPosition = new Vector2(-60, -50);
            rightNavGroup.sizeDelta = new Vector2(250, 300);

            string[] rightNames = { "BẠN BÈ", "BANG HỘI", "BXH" };
            Color[] rightColors = { m_CyanColor, m_PlayButtonColor, m_CardColor1 };
            for(int i = 0; i < 3; i++) {
                var btn = CreatePillButton(rightNavGroup.transform, rightNames[i], rightColors[i], 250, 70, 32, () => { });
                btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-125, 100 - i * 100);
            }

            // 6. Box Chat góc dưới trái
            var chatBox = CreateElement(panel.transform, "ChatBox", typeof(RectTransform)).GetComponent<RectTransform>();
            chatBox.anchorMin = chatBox.anchorMax = chatBox.pivot = new Vector2(0, 0);
            chatBox.anchoredPosition = new Vector2(40, 40);
            CreateCallout(chatBox, "[Thế giới] Tìm pt leo rank 5v5...", new Color(0.15f, 0.15f, 0.15f, 0.8f), 550, 80);

            // 7. Nút LEO RANK (Khôi phục lại vị trí trung tâm màn hình như ban đầu)
            var startBtnRt = CreateElement(panel.transform, "StartBtnContainer", typeof(RectTransform)).GetComponent<RectTransform>();
            startBtnRt.anchorMin = startBtnRt.anchorMax = new Vector2(0.5f, 0.45f);
            startBtnRt.anchoredPosition = Vector2.zero;
            CreatePillButton(startBtnRt, "LEO RANK", m_PlayButtonColor, 400, 100, 60, () => UpdateState(MenuState.ModeSelect));

            return panel;
        }

        private GameObject CreateModeSelectPanel(Transform parent)
        {
            var panel = CreatePanel("ModeSelectPanel", parent);
            
            // Thêm hình nền để biến thành trang riêng biệt (che lấp hoàn toàn trang Home)
            var bg = CreateElement(panel.transform, "Background", typeof(RectTransform), typeof(Image));
            StretchFull(bg.GetComponent<RectTransform>());
            bg.GetComponent<Image>().color = new Color(0.85f, 0.51f, 0.16f); // Nền cam

            // Tiêu đề
            var titleRt = CreateElement(panel.transform, "TitleContainer", typeof(RectTransform)).GetComponent<RectTransform>();
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.85f);
            titleRt.anchoredPosition = Vector2.zero;
            CreateShadowedTitle(titleRt.transform, "CHỌN CHẾ ĐỘ", 90f);

            // Back button
            var backRt = CreateElement(panel.transform, "BackBtn", typeof(RectTransform)).GetComponent<RectTransform>();
            backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
            backRt.anchoredPosition = new Vector2(180, -80);
            CreatePillButton(backRt, "< TRỞ VỀ", m_CyanColor, 250, 70, 36, () => UpdateState(MenuState.Home));

            // Các thẻ chế độ
            var modesGroup = CreateElement(panel.transform, "ModesGroup", typeof(RectTransform)).GetComponent<RectTransform>();
            modesGroup.anchorMin = modesGroup.anchorMax = modesGroup.pivot = new Vector2(0.5f, 0.45f);
            modesGroup.sizeDelta = new Vector2(1400, 500);
            modesGroup.anchoredPosition = Vector2.zero;

            var cardOffline = CreateModeCard(modesGroup, "OFFLINE\nBOTS", "Luyện Tập", m_CyanColor, () => Debug.Log("Vào chế độ Offline..."));
            cardOffline.GetComponent<RectTransform>().anchoredPosition = new Vector2(-450, 0);

            var card1 = CreateModeCard(modesGroup, "SOLO ARENA\n(1v1)", "Đấu Trường Sa Mạc", m_CardColor1, () => Debug.Log("Tạo sảnh 1v1..."));
            card1.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);

            var card2 = CreateModeCard(modesGroup, "TEAM COMBAT\n(5v5)", "Bản Đồ Chiến Lược", m_CardColor2, () => UpdateState(MenuState.Lobby5v5));
            card2.GetComponent<RectTransform>().anchoredPosition = new Vector2(450, 0);

            return panel;
        }

        private GameObject CreateLobby5v5Panel(Transform parent)
        {
            var panel = CreatePanel("Lobby5v5Panel", parent);

            // Thêm hình nền để biến thành trang riêng biệt
            var bg = CreateElement(panel.transform, "Background", typeof(RectTransform), typeof(Image));
            StretchFull(bg.GetComponent<RectTransform>());
            bg.GetComponent<Image>().color = new Color(0.85f, 0.51f, 0.16f); // Nền cam

            var titleRt = CreateElement(panel.transform, "TitleContainer", typeof(RectTransform)).GetComponent<RectTransform>();
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.85f);
            titleRt.anchoredPosition = Vector2.zero;
            CreateShadowedTitle(titleRt.transform, "PHÒNG CHỜ 5v5", 90f);

            // Back button
            var backRt = CreateElement(panel.transform, "BackBtn", typeof(RectTransform)).GetComponent<RectTransform>();
            backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
            backRt.anchoredPosition = new Vector2(180, -80);
            CreatePillButton(backRt, "< RỜI PHÒNG", m_CardColor1, 280, 70, 36, () => UpdateState(MenuState.ModeSelect));

            // Khung người chơi (Định vị tuyệt đối)
            var playersGroup = CreateElement(panel.transform, "PlayersGroup", typeof(RectTransform)).GetComponent<RectTransform>();
            playersGroup.anchorMin = playersGroup.anchorMax = playersGroup.pivot = new Vector2(0.5f, 0.5f);
            playersGroup.sizeDelta = new Vector2(1600, 400);
            playersGroup.anchoredPosition = Vector2.zero;

            string[] pNames = { "Player 1", "Player 2", "Player 3", "Trống", "Trống" };
            string[] pRoles = { "Đường Giữa", "Đi Rừng", "Xạ Thủ", "+ Mời", "+ Mời" };
            bool[] pStates = { true, true, true, false, false };

            for(int i = 0; i < 5; i++) {
                var slot = CreatePlayerSlot(playersGroup, pNames[i], pRoles[i], pStates[i]);
                slot.GetComponent<RectTransform>().anchoredPosition = new Vector2(-580 + (i * 290), 0);
            }

            // Nút Bắt đầu
            var startBtnRt = CreateElement(panel.transform, "StartBtn", typeof(RectTransform)).GetComponent<RectTransform>();
            startBtnRt.anchorMin = startBtnRt.anchorMax = new Vector2(0.5f, 0.2f);
            startBtnRt.anchoredPosition = Vector2.zero;
            CreatePillButton(startBtnRt, "BẮT ĐẦU TÌM TRẬN", m_PlayButtonColor, 500, 100, 50, () => Debug.Log("Đang tìm trận..."));

            return panel;
        }

        #endregion

        #region UI Helpers (Kế thừa Vibe từ StartScreen)

        private static GameObject CreatePanel(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            StretchFull(rt);
            return go;
        }

        private static void CreateShadowedTitle(Transform parent, string text, float size)
        {

            var fore = CreateTMP(parent, "Fore", text, size, FontStyles.Bold | FontStyles.Italic, Color.white);
            fore.rectTransform.anchoredPosition = Vector2.zero;
        }

        private Button CreatePillButton(Transform parent, string label, Color color, float width, float height, float fontSize, UnityEngine.Events.UnityAction onClick)
        {
            var rt = CreateElement(parent, "PillButton_" + label, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = Vector2.zero;

            var fillGo = CreateElement(rt, "Fill", typeof(RectTransform), typeof(Image), typeof(Button));
            StretchFull(fillGo.GetComponent<RectTransform>());
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.sprite = CreateRoundedRectSprite(120, (int)height/2, color, m_OutlineColor, 5);
            fillImg.type = Image.Type.Sliced;

            var button = fillGo.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f); 
            colors.pressedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.colorMultiplier = 1;
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            var vibe = fillGo.AddComponent<UIButtonVibe>();
            vibe.clickSound = m_ClickSound;
            
            var labelTmp = CreateTMP(fillGo.transform, "Label", label, fontSize, FontStyles.Bold | FontStyles.Italic, m_TextDark);
            labelTmp.characterSpacing = 4f;

            button.onClick.AddListener(onClick);
            return button;
        }

        private void CreateCallout(Transform parent, string text, Color color, float width, float height)
        {
            var rt = parent.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);

            var fillImg = CreateImage(rt, "Fill", CreateRoundedRectSprite(64, (int)height/2, color, m_OutlineColor, 4), Image.Type.Sliced, true);

            var label = CreateTMP(fillImg.transform, "Label", text, height * 0.45f, FontStyles.Bold | FontStyles.Italic, Color.white);
            label.characterSpacing = 2f;
        }

        private GameObject CreateModeCard(Transform parent, string title, string desc, Color accentColor, Action onClick)
        {
            var cardGo = CreateElement(parent, $"Card_{title}", typeof(RectTransform));
            var rt = cardGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400, 450);

            var fillImg = CreateImage(cardGo.transform, "Fill", CreateRoundedRectSprite(120, 40, accentColor, m_OutlineColor, 6), Image.Type.Sliced, true);
            var button = fillImg.gameObject.AddComponent<Button>();
            
            var vibe = fillImg.gameObject.AddComponent<UIButtonVibe>();
            vibe.clickSound = m_ClickSound;
            
            button.onClick.AddListener(() => onClick());

            var titleTmp = CreateTMP(fillImg.transform, "Title", title, 50, FontStyles.Bold | FontStyles.Italic, Color.white, TextAlignmentOptions.Center);
            titleTmp.rectTransform.anchorMin = new Vector2(0, 0.5f);
            titleTmp.rectTransform.anchorMax = new Vector2(1, 1);
            titleTmp.rectTransform.offsetMin = new Vector2(20, 0);
            titleTmp.rectTransform.offsetMax = new Vector2(-20, -40);

            var descTmp = CreateTMP(fillImg.transform, "Desc", desc, 36, FontStyles.Bold, new Color(0.9f, 0.9f, 0.9f), TextAlignmentOptions.Center);
            descTmp.rectTransform.anchorMin = new Vector2(0, 0);
            descTmp.rectTransform.anchorMax = new Vector2(1, 0.5f);
            descTmp.rectTransform.offsetMin = new Vector2(20, 40);
            descTmp.rectTransform.offsetMax = new Vector2(-20, 0);

            return cardGo;
        }

        private GameObject CreatePlayerSlot(Transform parent, string playerName, string role, bool hasPlayer)
        {
            var slotGo = CreateElement(parent, $"Slot_{playerName}", typeof(RectTransform));
            var rt = slotGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(260, 360);

            Color bgColor = hasPlayer ? m_CyanColor : new Color(0.6f, 0.6f, 0.6f);
            
            var fillImg = CreateImage(slotGo.transform, "Fill", CreateRoundedRectSprite(100, 30, bgColor, m_OutlineColor, 5), Image.Type.Sliced, true);

            var titleTmp = CreateTMP(fillImg.transform, "Name", playerName, 36, FontStyles.Bold | FontStyles.Italic, Color.white, TextAlignmentOptions.Center);
            titleTmp.rectTransform.anchorMin = new Vector2(0, 0.5f);
            titleTmp.rectTransform.anchorMax = new Vector2(1, 1);
            
            var roleTmp = CreateTMP(fillImg.transform, "Role", role, 30, FontStyles.Bold, m_TextDark, TextAlignmentOptions.Center);
            roleTmp.rectTransform.anchorMin = new Vector2(0, 0);
            roleTmp.rectTransform.anchorMax = new Vector2(1, 0.5f);

            return slotGo;
        }

        #endregion

        #region Core UI Generators

        private static GameObject CreateElement(Transform parent, string name, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static Image CreateImage(Transform parent, string name, Sprite sprite, Image.Type type = Image.Type.Simple, bool stretch = false)
        {
            var img = CreateElement(parent, name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.sprite = sprite;
            img.type = type;
            if (stretch) StretchFull(img.rectTransform);
            return img;
        }

        private static Image CreateShadow(Transform parent, int size, int radius, Vector2 offsetMin, Vector2 offsetMax, float alpha = 0.35f)
        {
            var img = CreateImage(parent, "Shadow", CreateRoundedRectSprite(size, radius, new Color(0f, 0f, 0f, alpha), Color.clear, 0), Image.Type.Sliced, true);
            var rt = img.rectTransform;
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
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, b);
        }

        private static Sprite CreateDuneSilhouetteSprite(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var clear = new Color(0f, 0f, 0f, 0f);

            for (int x = 0; x < width; x++)
            {
                float t = x / (float)width;
                float wave = Mathf.Sin(t * Mathf.PI * 2f) * 0.18f + Mathf.Sin(t * Mathf.PI * 5f + 1.3f) * 0.09f + Mathf.Sin(t * Mathf.PI * 11f + 2.7f) * 0.04f;
                int topY = Mathf.Clamp(Mathf.RoundToInt((0.55f + wave) * height), 1, height - 1);
                for (int y = 0; y < height; y++) tex.SetPixel(x, y, y <= topY ? color : clear);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0f));
        }
        #endregion

#if UNITY_EDITOR
        [ContextMenu("Bake UI To Hierarchy")]
        public void BakeUIToHierarchy()
        {
            var old = transform.Find("MainMenuCanvas");
            if (old) DestroyImmediate(old.gameObject);

            BuildUI();

            if (!AssetDatabase.IsValidFolder("Assets/_Tanks/Sprites"))
                AssetDatabase.CreateFolder("Assets/_Tanks", "Sprites");
            if (!AssetDatabase.IsValidFolder("Assets/_Tanks/Sprites/GeneratedUI"))
                AssetDatabase.CreateFolder("Assets/_Tanks/Sprites", "GeneratedUI");

            var images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.sprite != null && img.sprite.texture != null && !AssetDatabase.Contains(img.sprite.texture))
                {
                    string path = $"Assets/_Tanks/Sprites/GeneratedUI/{img.sprite.texture.name}_{img.sprite.texture.GetHashCode()}.asset";
                    var tex = img.sprite.texture;
                    var spr = img.sprite;
                    AssetDatabase.CreateAsset(tex, path);
                    AssetDatabase.AddObjectToAsset(spr, tex);
                    AssetDatabase.SaveAssets();
                }
            }

            m_HomePanel = transform.Find("MainMenuCanvas/HomePanel").gameObject;
            m_ModeSelectPanel = transform.Find("MainMenuCanvas/ModeSelectPanel").gameObject;
            m_Lobby5v5Panel = transform.Find("MainMenuCanvas/Lobby5v5Panel").gameObject;
            
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log("Đã nướng (Bake) UI thành công lên Hierarchy và lưu Sprite!");
        }
#endif
    }
}
