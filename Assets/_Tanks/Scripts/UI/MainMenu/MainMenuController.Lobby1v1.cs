using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using Unity.Services.Multiplayer;
using Tanks.Backend;

namespace Tanks.Complete
{
    /// <summary>
    /// Sảnh chờ 1v1 Online: Tạo phòng (Host) -> nhận Join Code -> bạn bè nhập mã để vào (Client).
    /// Khi đủ 2 người, Chủ phòng chọn bản đồ và bấm "BẮT ĐẦU" để tải scene chiến đấu qua Netcode.
    /// </summary>
    public partial class MainMenuController
    {
        // Các bản đồ có thể chọn cho trận 1v1 online (Chủ phòng chọn).
        private static readonly string[] k_Lobby1v1Maps = { "Desert", "Jungle", "Moon" };
        private string m_SelectedMap = "Desert";

        // View con: chưa vào phòng (tạo/nhập mã) vs đã ở trong phòng.
        private GameObject m_EntryView;
        private GameObject m_RoomView;

        // Widget động cần cập nhật theo trạng thái session.
        private TMP_InputField m_JoinCodeInput;
        private TextMeshProUGUI m_CodeLabel;
        private TextMeshProUGUI m_Lobby1v1Status;
        private const int k_MaxOnlinePlayers = 4;
        private readonly TextMeshProUGUI[] m_SlotNames = new TextMeshProUGUI[k_MaxOnlinePlayers];
        private readonly TextMeshProUGUI[] m_SlotStateLabels = new TextMeshProUGUI[k_MaxOnlinePlayers];
        private readonly Image[] m_SlotFills = new Image[k_MaxOnlinePlayers];
        private readonly Image[] m_SlotThumbs = new Image[k_MaxOnlinePlayers];
        private readonly Image[] m_SlotTeamBars = new Image[k_MaxOnlinePlayers];
        private readonly GameObject[] m_RoomSlots = new GameObject[k_MaxOnlinePlayers];
        private TextMeshProUGUI m_OnlineLobbyTitle;
        private TextMeshProUGUI m_VsLabel;
        private GameObject m_BlueTeamHeaderGo;
        private GameObject m_RedTeamHeaderGo;
        private bool m_IsTeam2v2;
        private readonly Image[] m_MapPickFills = new Image[3];
        private readonly Button[] m_MapPickButtons = new Button[3];
        private readonly UIMapSelectionVisual[] m_MapPickVisuals = new UIMapSelectionVisual[3];
        private TextMeshProUGUI m_SelectedMapLabel;
        private bool m_MapSelectionInitialized;
        private RectTransform m_WaitingRadarRt;
        private GameObject m_HostControls;   // Khu map luôn hiện; chỉ Chủ phòng được tương tác.
        private GameObject m_WaitingLabelGo;  // Nhãn "Chờ chủ phòng..." (chỉ Khách thấy)
        private Button m_StartMatchButton;
        private GameObject m_StartMatchButtonGo;
        private GameObject m_ChangeTeamButtonGo;

        private Button m_CreateRoomButton;
        private Button m_JoinRoomButton;
        private GameObject m_RetryButtonGo;

        private bool m_LobbyEventsHooked;
        private bool m_UgsEventsHooked;
        private float m_StatusOverrideUntil;  // Giữ thông báo tạm thời không bị RefreshLobby ghi đè.

        // Custom Messaging Profile Sync
        private struct ProfileInfoMessage
        {
            public ulong ClientId;
            public string DisplayName;
            public string AvatarId;
            public int Team;
        }
        private System.Collections.Generic.Dictionary<ulong, ProfileInfoMessage> m_LobbyProfiles = new System.Collections.Generic.Dictionary<ulong, ProfileInfoMessage>();
        private bool m_NetcodeEventsHooked = false;

        // Quản lý trạng thái (chặn thao tác khi đang gọi UGS)
        private enum UgsConnState { Unknown, Connecting, Connected, Failed }
        private UgsConnState m_ConnState = UgsConnState.Connecting;
        private string m_ConnError;

        // =====================================================================
        //  BUILD UI
        // =====================================================================
        private GameObject CreateLobby1v1Panel(Transform parent)
        {
            var panel = CreatePanel("Lobby1v1Panel", parent);

            var bg = CreateElement(panel.transform, "Background", typeof(RectTransform), typeof(Image));
            StretchFull(bg.GetComponent<RectTransform>());
            var bgImg = bg.GetComponent<Image>();

            // Tự động load ảnh nền desert, nếu không có thì fallback sang màu cam
            Sprite bgSprite = LoadSpriteWithFallback("background_desert");
            if (bgSprite != null)
            {
                bgImg.sprite = bgSprite;
                bgImg.color = Color.white;
            }
            else
            {
                bgImg.color = m_BgColor;
            }

            // Làm dịu ảnh nền để các khối thông tin dễ đọc ở mọi bản đồ.
            var dimmer = CreateElement(panel.transform, "Dimmer", typeof(RectTransform), typeof(Image));
            StretchFull(dimmer.GetComponent<RectTransform>());
            dimmer.GetComponent<Image>().color = new Color(0, 0, 0, 0.46f);

            // Tiêu đề
            var titleRt = CreateElement(panel.transform, "TitleContainer", typeof(RectTransform)).GetComponent<RectTransform>();
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.91f);
            titleRt.anchoredPosition = Vector2.zero;
            CreateShadowedTitle(titleRt.transform, "ĐẤU TRƯỜNG 1v1", 62f);
            m_OnlineLobbyTitle = titleRt.GetComponentInChildren<TextMeshProUGUI>();

            // Nút quay lại / rời phòng
            var backRt = CreateElement(panel.transform, "BackBtn", typeof(RectTransform)).GetComponent<RectTransform>();
            backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
            backRt.anchoredPosition = new Vector2(135, -58);
            CreatePillButton(backRt, "< TRỞ VỀ", m_CardColor1, 210, 58, 27, OnLobby1v1Back);

            // Thanh trạng thái dưới cùng
            var statusBg = CreateImage(panel.transform, "Status",
                CreateRoundedRectSprite(64, 28, new Color(0.04f, 0.05f, 0.06f, 0.78f), new Color(1f, 1f, 1f, 0.16f), 2), Image.Type.Sliced, true);
            var statusRt = statusBg.rectTransform;
            statusRt.anchorMin = statusRt.anchorMax = new Vector2(0.5f, 0.075f);
            statusRt.sizeDelta = new Vector2(900, 56);
            statusRt.anchoredPosition = Vector2.zero;
            m_Lobby1v1Status = CreateTMP(statusRt, "Label", "", 26, FontStyles.Bold | FontStyles.Italic, Color.white, TextAlignmentOptions.Center);
            StretchFull(m_Lobby1v1Status.rectTransform);
            m_Lobby1v1Status.rectTransform.offsetMin = new Vector2(24, 0);
            m_Lobby1v1Status.rectTransform.offsetMax = new Vector2(-24, 0);

            BuildEntryView(panel.transform);
            BuildRoomView(panel.transform);

            // Nút "Thử lại" (chỉ hiện khi kết nối UGS thất bại)
            // Đặt tạo cuối cùng để luôn render đè lên trên board
            var retryRt = CreateElement(panel.transform, "RetryBtn", typeof(RectTransform)).GetComponent<RectTransform>();
            retryRt.anchorMin = retryRt.anchorMax = new Vector2(0.5f, 0f); // Neo dưới cùng
            retryRt.anchoredPosition = new Vector2(0, 140); // Nằm trên Status label
            CreatePillButton(retryRt, "THỬ LẠI", m_PlayButtonColor, 280, 74, 34, OnRetryClicked);
            m_RetryButtonGo = retryRt.gameObject;
            m_RetryButtonGo.SetActive(false);

            return panel;
        }

        // ------- View 1: chưa vào phòng -------
        private void BuildEntryView(Transform panel)
        {
            var view = CreateElement(panel, "EntryView", typeof(RectTransform)).GetComponent<RectTransform>();
            view.anchorMin = view.anchorMax = view.pivot = new Vector2(0.5f, 0.5f);
            view.sizeDelta = new Vector2(640, 580);
            view.anchoredPosition = Vector2.zero;
            m_EntryView = view.gameObject;

            // Khung Bảng Arcade trung tâm
            var board = CreateImage(view.transform, "Board", 
                CreateRoundedRectSprite(120, 40, new Color(0.7f, 0.4f, 0.2f), m_OutlineColor, 6), Image.Type.Sliced, true);
            var boardRt = board.rectTransform;
            StretchFull(boardRt);

            var createHolder = CreateElement(view, "CreateHolder", typeof(RectTransform)).GetComponent<RectTransform>();
            createHolder.anchoredPosition = new Vector2(0, 160);
            m_CreateRoomButton = CreatePillButton(createHolder, "TẠO PHÒNG MỚI", m_PlayButtonColor, 520, 110, 42, OnCreateRoomClicked);

            var orLabel = CreateTMP(view, "OrLabel", "— HOẶC NHẬP MÃ PHÒNG —", 28,
                FontStyles.Bold | FontStyles.Italic, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center);
            orLabel.rectTransform.anchorMin = orLabel.rectTransform.anchorMax = orLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            orLabel.rectTransform.sizeDelta = new Vector2(500, 40);
            orLabel.rectTransform.anchoredPosition = new Vector2(0, 30);

            m_JoinCodeInput = CreateInputField(view, "NHẬP MÃ...", 420, 90);
            m_JoinCodeInput.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -60);

            var joinHolder = CreateElement(view, "JoinHolder", typeof(RectTransform)).GetComponent<RectTransform>();
            joinHolder.anchoredPosition = new Vector2(0, -180);
            m_JoinRoomButton = CreatePillButton(joinHolder, "VÀO PHÒNG", m_CyanColor, 340, 90, 36, OnJoinRoomClicked);
        }

        // ------- View 2: đã ở trong phòng -------
        private void BuildRoomView(Transform panel)
        {
            var view = CreateElement(panel, "RoomView", typeof(RectTransform)).GetComponent<RectTransform>();
            view.anchorMin = view.anchorMax = view.pivot = new Vector2(0.5f, 0.5f);
            view.sizeDelta = new Vector2(1400, 720);
            view.anchoredPosition = new Vector2(0, -8);
            m_RoomView = view.gameObject;
            m_RoomView.SetActive(false);

            // Mã phòng nằm gọn ở góc phải, tách khỏi tiêu đề và nội dung thi đấu.
            var codeGroup = CreateElement(view, "CodeGroup", typeof(RectTransform)).GetComponent<RectTransform>();
            codeGroup.anchorMin = codeGroup.anchorMax = codeGroup.pivot = new Vector2(1f, 1f);
            codeGroup.anchoredPosition = new Vector2(-18, -18);
            codeGroup.sizeDelta = new Vector2(450, 58);

            var codeTitle = CreateTMP(codeGroup, "CodeTitle", "MÃ PHÒNG", 19, FontStyles.Bold,
                new Color(1f, 1f, 1f, 0.78f), TextAlignmentOptions.Left);
            codeTitle.rectTransform.anchorMin = codeTitle.rectTransform.anchorMax = codeTitle.rectTransform.pivot = new Vector2(0f, 0.5f);
            codeTitle.rectTransform.sizeDelta = new Vector2(108, 40);
            codeTitle.rectTransform.anchoredPosition = new Vector2(0, 0);

            var codeBox = CreateImage(codeGroup, "CodeBox",
                CreateRoundedRectSprite(64, 26, Color.white, m_OutlineColor, 3), Image.Type.Sliced, true);
            var codeBoxRt = codeBox.rectTransform;
            codeBoxRt.anchorMin = codeBoxRt.anchorMax = codeBoxRt.pivot = new Vector2(0f, 0.5f);
            codeBoxRt.sizeDelta = new Vector2(190, 52);
            codeBoxRt.anchoredPosition = new Vector2(112, 0);

            m_CodeLabel = CreateTMP(codeBoxRt, "CodeLabel", "------", 30, FontStyles.Bold, m_TextDark, TextAlignmentOptions.Center);
            m_CodeLabel.rectTransform.anchorMin = m_CodeLabel.rectTransform.anchorMax = m_CodeLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            m_CodeLabel.rectTransform.sizeDelta = new Vector2(176, 44);
            m_CodeLabel.rectTransform.anchoredPosition = Vector2.zero;
            m_CodeLabel.characterSpacing = 4f;

            var copyHolder = CreateElement(codeGroup, "CopyHolder", typeof(RectTransform)).GetComponent<RectTransform>();
            copyHolder.anchorMin = copyHolder.anchorMax = copyHolder.pivot = new Vector2(0f, 0.5f);
            copyHolder.anchoredPosition = new Vector2(370, 0);
            CreatePillButton(copyHolder, "SAO CHÉP", m_CardColor2, 136, 52, 18, OnCopyCodeClicked);

            // Tiêu đề đội giúp đọc bố cục ngay lập tức nhưng không che nhân vật hay bản đồ.
            var blueHeader = CreateImage(view, "BlueTeamHeader",
                CreateRoundedRectSprite(64, 24, new Color(0.03f, 0.08f, 0.10f, 0.88f), m_CyanColor, 3), Image.Type.Sliced, true);
            blueHeader.rectTransform.anchorMin = blueHeader.rectTransform.anchorMax = blueHeader.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            blueHeader.rectTransform.sizeDelta = new Vector2(360, 52);
            blueHeader.rectTransform.anchoredPosition = new Vector2(-475, 230);
            m_BlueTeamHeaderGo = blueHeader.gameObject;
            var blueHeaderLabel = CreateTMP(blueHeader.transform, "Label", "ĐỘI XANH", 25, FontStyles.Bold, m_CyanColor, TextAlignmentOptions.Center);
            StretchFull(blueHeaderLabel.rectTransform);

            var redHeader = CreateImage(view, "RedTeamHeader",
                CreateRoundedRectSprite(64, 24, new Color(0.11f, 0.04f, 0.03f, 0.88f), m_CardColor1, 3), Image.Type.Sliced, true);
            redHeader.rectTransform.anchorMin = redHeader.rectTransform.anchorMax = redHeader.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            redHeader.rectTransform.sizeDelta = new Vector2(360, 52);
            redHeader.rectTransform.anchoredPosition = new Vector2(475, 230);
            m_RedTeamHeaderGo = redHeader.gameObject;
            var redHeaderLabel = CreateTMP(redHeader.transform, "Label", "ĐỘI ĐỎ", 25, FontStyles.Bold, m_CardColor1, TextAlignmentOptions.Center);
            StretchFull(redHeaderLabel.rectTransform);

            for (int i = 0; i < k_MaxOnlinePlayers; i++)
                m_RoomSlots[i] = CreateRoomSlot(view, i);

            // Khu thiết lập trận luôn ở giữa, tách biệt rõ hai đội.
            var setup = CreateElement(view, "MatchSetup", typeof(RectTransform)).GetComponent<RectTransform>();
            setup.anchorMin = setup.anchorMax = setup.pivot = new Vector2(0.5f, 0.5f);
            setup.sizeDelta = new Vector2(410, 410);
            setup.anchoredPosition = new Vector2(0, 0);
            CreateShadow(setup, 96, 32, new Vector2(10, -12), new Vector2(10, -12), 0.32f);
            var setupBg = CreateImage(setup, "Fill",
                CreateRoundedRectSprite(96, 28, new Color(0.035f, 0.045f, 0.05f, 0.94f), new Color(1f, 0.82f, 0.25f, 0.75f), 3), Image.Type.Sliced, true);
            StretchFull(setupBg.rectTransform);

            var setupTitle = CreateTMP(setupBg.transform, "Title", "THIẾT LẬP TRẬN", 23, FontStyles.Bold,
                new Color(1f, 1f, 1f, 0.78f), TextAlignmentOptions.Center);
            setupTitle.rectTransform.anchorMin = setupTitle.rectTransform.anchorMax = setupTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            setupTitle.rectTransform.sizeDelta = new Vector2(330, 34);
            setupTitle.rectTransform.anchoredPosition = new Vector2(0, -26);

            var vsBadge = CreateImage(setupBg.transform, "VsBadge",
                CreateRoundedRectSprite(64, 28, new Color(0.12f, 0.10f, 0.04f, 0.95f), m_PlayButtonColor, 3), Image.Type.Sliced, true);
            var vsRt = vsBadge.rectTransform;
            vsRt.anchorMin = vsRt.anchorMax = vsRt.pivot = new Vector2(0.5f, 0.5f);
            vsRt.sizeDelta = new Vector2(104, 58);
            vsRt.anchoredPosition = new Vector2(0, 102);
            m_VsLabel = CreateTMP(vsRt, "VS", "VS", 40, FontStyles.Bold | FontStyles.Italic, m_PlayButtonColor, TextAlignmentOptions.Center);
            StretchFull(m_VsLabel.rectTransform);

            // Chủ phòng chọn bản đồ và bắt đầu trong cùng một cột trung tâm.
            var host = CreateElement(setupBg.transform, "HostControls", typeof(RectTransform)).GetComponent<RectTransform>();
            host.anchorMin = host.anchorMax = host.pivot = new Vector2(0.5f, 0.5f);
            host.sizeDelta = new Vector2(390, 250);
            host.anchoredPosition = new Vector2(0, -58);
            m_HostControls = host.gameObject;

            var mapTitle = CreateTMP(host, "MapTitle", "CHỌN CHIẾN TRƯỜNG", 18, FontStyles.Bold,
                new Color(1f, 1f, 1f, 0.65f), TextAlignmentOptions.Center);
            mapTitle.rectTransform.anchorMin = mapTitle.rectTransform.anchorMax = mapTitle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            mapTitle.rectTransform.sizeDelta = new Vector2(320, 28);
            mapTitle.rectTransform.anchoredPosition = new Vector2(0, 78);

            float[] xs = { -124, 0, 124 };
            m_MapSelectionInitialized = false;
            for (int i = 0; i < k_Lobby1v1Maps.Length; i++)
            {
                string map = k_Lobby1v1Maps[i];
                CreateMapCard(host, map, xs[i], i);
            }

            m_SelectedMapLabel = CreateTMP(host, "SelectedMapLabel", "", 15,
                FontStyles.Bold | FontStyles.Italic, m_PlayButtonColor, TextAlignmentOptions.Center);
            m_SelectedMapLabel.rectTransform.anchorMin = m_SelectedMapLabel.rectTransform.anchorMax =
                m_SelectedMapLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            m_SelectedMapLabel.rectTransform.sizeDelta = new Vector2(330, 24);
            m_SelectedMapLabel.rectTransform.anchoredPosition = new Vector2(0, -38);
            m_SelectedMapLabel.characterSpacing = 1.5f;

            var startHolder = CreateElement(host, "StartHolder", typeof(RectTransform)).GetComponent<RectTransform>();
            startHolder.anchorMin = startHolder.anchorMax = startHolder.pivot = new Vector2(0.5f, 0.5f);
            startHolder.anchoredPosition = new Vector2(0, -92);
            m_StartMatchButton = CreatePillButton(startHolder, "BẮT ĐẦU TRẬN ĐẤU", m_PlayButtonColor, 342, 62, 24, OnStartMatchClicked);
            m_StartMatchButtonGo = startHolder.gameObject;

            // Khách vẫn thấy map host đã chọn; banner này thay vị trí nút Bắt đầu.
            var waitRt = CreateElement(setupBg.transform, "WaitingLabel", typeof(RectTransform)).GetComponent<RectTransform>();
            waitRt.anchorMin = waitRt.anchorMax = waitRt.pivot = new Vector2(0.5f, 0.5f);
            waitRt.sizeDelta = new Vector2(342, 62);
            waitRt.anchoredPosition = new Vector2(0, -150);
            var waitBg = CreateImage(waitRt, "Fill",
                CreateRoundedRectSprite(64, 32, new Color(0.05f, 0.07f, 0.08f, 0.84f), m_PlayButtonColor, 3), Image.Type.Sliced, true);
            StretchFull(waitBg.rectTransform);
            var radar = CreateImage(waitBg.transform, "Radar",
                CreateRadarSprite(96, m_CyanColor, new Color(1f, 1f, 1f, 0.28f)), Image.Type.Simple, false);
            m_WaitingRadarRt = radar.rectTransform;
            m_WaitingRadarRt.anchorMin = m_WaitingRadarRt.anchorMax = m_WaitingRadarRt.pivot = new Vector2(0f, 0.5f);
            m_WaitingRadarRt.sizeDelta = new Vector2(40, 40);
            m_WaitingRadarRt.anchoredPosition = new Vector2(22, 0);
            var waitLabel = CreateTMP(waitBg.transform, "Label", "ĐANG CHỜ CHỦ PHÒNG...", 18,
                FontStyles.Bold | FontStyles.Italic, Color.white, TextAlignmentOptions.Center);
            waitLabel.rectTransform.offsetMin = new Vector2(58, 4);
            waitLabel.rectTransform.offsetMax = new Vector2(-14, -4);
            m_WaitingLabelGo = waitRt.gameObject;

            // Đổi đội là hành động phụ, đặt dưới bảng thiết lập để không cạnh tranh với nút Bắt đầu.
            var changeTeamRt = CreateElement(view, "ChangeTeamButton", typeof(RectTransform)).GetComponent<RectTransform>();
            changeTeamRt.anchorMin = changeTeamRt.anchorMax = changeTeamRt.pivot = new Vector2(0.5f, 0.5f);
            changeTeamRt.anchoredPosition = new Vector2(0, -244);
            CreatePillButton(changeTeamRt, "ĐỔI ĐỘI", m_CyanColor, 190, 50, 21, OnChangeTeamClicked);
            m_ChangeTeamButtonGo = changeTeamRt.gameObject;

            ApplySelectedMap(m_SelectedMap);
            ApplyOnlineModeLayout();
        }

        private GameObject CreateRoomSlot(Transform parent, int index)
        {
            var slotGo = CreateElement(parent, $"Slot_{index}", typeof(RectTransform));
            var rt = slotGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(360, 160);

            CreateShadow(slotGo.transform, 96, 28, new Vector2(9, -10), new Vector2(9, -10), 0.28f);

            // Card trung tính; màu đội chỉ dùng làm đường nhấn để avatar luôn dễ nhìn.
            var fill = CreateImage(slotGo.transform, "Fill",
                CreateRoundedRectSprite(96, 22, new Color(0.035f, 0.045f, 0.05f, 0.94f), new Color(1f, 1f, 1f, 0.18f), 2), Image.Type.Sliced, true);
            m_SlotFills[index] = fill;
            StretchFull(fill.rectTransform);

            var teamBar = CreateImage(fill.transform, "TeamBar",
                CreateRoundedRectSprite(24, 4, Color.white, Color.clear, 0), Image.Type.Sliced, false);
            teamBar.rectTransform.anchorMin = new Vector2(0f, 0.14f);
            teamBar.rectTransform.anchorMax = new Vector2(0f, 0.86f);
            teamBar.rectTransform.pivot = new Vector2(0f, 0.5f);
            teamBar.rectTransform.sizeDelta = new Vector2(7, 0);
            teamBar.rectTransform.anchoredPosition = new Vector2(8, 0);
            m_SlotTeamBars[index] = teamBar;

            var avatarFrame = CreateImage(fill.transform, "AvatarFrame",
                CreateRoundedRectSprite(64, 18, new Color(0.08f, 0.10f, 0.11f, 1f), new Color(1f, 1f, 1f, 0.12f), 2), Image.Type.Sliced, true);
            var avatarFrameRt = avatarFrame.rectTransform;
            avatarFrameRt.anchorMin = avatarFrameRt.anchorMax = avatarFrameRt.pivot = new Vector2(0f, 0.5f);
            avatarFrameRt.sizeDelta = new Vector2(116, 124);
            avatarFrameRt.anchoredPosition = new Vector2(24, 0);

            // Vùng avatar/silhouette nằm riêng, không bị chữ hoặc màu nền phủ lên.
            var thumbGo = CreateElement(avatarFrame.transform, "Thumb", typeof(RectTransform), typeof(Image));
            var thumbRt = thumbGo.GetComponent<RectTransform>();
            StretchFull(thumbRt);
            thumbRt.offsetMin = new Vector2(7, 7);
            thumbRt.offsetMax = new Vector2(-7, -7);

            var thumbImg = thumbGo.GetComponent<Image>();
            m_SlotThumbs[index] = thumbImg;
            thumbImg.preserveAspect = true;
            thumbImg.sprite = CreateTankSilhouetteSprite(256, 150, new Color(0.18f, 0.22f, 0.24f, 1f));
            thumbImg.color = new Color(0.16f, 0.22f, 0.25f, 0.72f);

            var divider = CreateImage(fill.transform, "Divider",
                CreateRoundedRectSprite(8, 0, new Color(1f, 1f, 1f, 0.12f), Color.clear, 0), Image.Type.Sliced, false);
            divider.rectTransform.anchorMin = divider.rectTransform.anchorMax = divider.rectTransform.pivot = new Vector2(0f, 0.5f);
            divider.rectTransform.sizeDelta = new Vector2(2, 104);
            divider.rectTransform.anchoredPosition = new Vector2(154, 0);

            var name = CreateTMP(fill.transform, "Name", "", 27, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
            name.rectTransform.anchorMin = new Vector2(0f, 0.52f);
            name.rectTransform.anchorMax = new Vector2(1f, 0.82f);
            name.rectTransform.offsetMin = new Vector2(172, 0);
            name.rectTransform.offsetMax = new Vector2(-18, 0);

            var state = CreateTMP(fill.transform, "State", "ĐANG CHỜ...", 16,
                FontStyles.Bold | FontStyles.Italic, m_CyanColor, TextAlignmentOptions.Left);
            state.rectTransform.anchorMin = new Vector2(0f, 0.24f);
            state.rectTransform.anchorMax = new Vector2(1f, 0.48f);
            state.rectTransform.offsetMin = new Vector2(172, 0);
            state.rectTransform.offsetMax = new Vector2(-18, 0);
            m_SlotStateLabels[index] = state;

            m_SlotNames[index] = name;

            return slotGo;
        }

        private static Sprite CreateRadarSprite(int size, Color sweep, Color line)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var clear = new Color(0f, 0f, 0f, 0f);
            float center = (size - 1) * 0.5f;
            float outer = center - 2f;
            float inner = outer * 0.58f;
            float sweepWidth = Mathf.PI * 0.18f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r > outer)
                    {
                        tex.SetPixel(x, y, clear);
                        continue;
                    }

                    float alpha = 0f;
                    if (Mathf.Abs(r - outer) < 1.8f || Mathf.Abs(r - inner) < 1.4f) alpha = 0.45f;
                    if (Mathf.Abs(dx) < 1.2f || Mathf.Abs(dy) < 1.2f) alpha = Mathf.Max(alpha, 0.22f);

                    float angle = Mathf.Atan2(dy, dx);
                    if (angle < 0f) angle += Mathf.PI * 2f;
                    float sweepAlpha = Mathf.Clamp01(1f - Mathf.Abs(angle) / sweepWidth) * Mathf.Clamp01(1f - r / outer * 0.15f);
                    if (sweepAlpha > alpha)
                    {
                        var c = sweep;
                        c.a *= sweepAlpha;
                        tex.SetPixel(x, y, c);
                    }
                    else
                    {
                        var c = line;
                        c.a *= alpha;
                        tex.SetPixel(x, y, c);
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateTankSilhouetteSprite(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool body = x >= width * 0.18f && x <= width * 0.78f && y >= height * 0.28f && y <= height * 0.58f;
                    bool turret = x >= width * 0.38f && x <= width * 0.62f && y >= height * 0.52f && y <= height * 0.73f;
                    bool barrel = x >= width * 0.58f && x <= width * 0.93f && y >= height * 0.61f && y <= height * 0.68f;
                    bool tread = x >= width * 0.12f && x <= width * 0.82f && y >= height * 0.16f && y <= height * 0.32f;
                    bool wheelA = (new Vector2(x - width * 0.28f, y - height * 0.22f)).sqrMagnitude < Mathf.Pow(height * 0.08f, 2f);
                    bool wheelB = (new Vector2(x - width * 0.48f, y - height * 0.22f)).sqrMagnitude < Mathf.Pow(height * 0.08f, 2f);
                    bool wheelC = (new Vector2(x - width * 0.68f, y - height * 0.22f)).sqrMagnitude < Mathf.Pow(height * 0.08f, 2f);

                    tex.SetPixel(x, y, body || turret || barrel || tread || wheelA || wheelB || wheelC ? color : clear);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }

        private GameObject CreateMapCard(Transform parent, string mapName, float posX, int index)
        {
            var cardGo = CreateElement(parent, "MapPick_" + mapName, typeof(RectTransform));
            var rt = cardGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(112, 82);
            rt.anchoredPosition = new Vector2(posX, 18);

            var glow = CreateImage(cardGo.transform, "SelectedGlow",
                CreateRoundedRectSprite(36, 16, Color.white, Color.clear, 0), Image.Type.Sliced, true);
            StretchFull(glow.rectTransform);
            glow.rectTransform.offsetMin = new Vector2(-7, -7);
            glow.rectTransform.offsetMax = new Vector2(7, 7);
            glow.raycastTarget = false;

            var fill = CreateImage(cardGo.transform, "Fill",
                CreateRoundedRectSprite(32, 14, new Color(0.055f, 0.065f, 0.075f), new Color(1f, 1f, 1f, 0.20f), 2), Image.Type.Sliced, true);
            m_MapPickFills[index] = fill;
            StretchFull(fill.rectTransform);

            var thumbGo = CreateElement(fill.transform, "Thumb", typeof(RectTransform), typeof(Image));
            var thumbRt = thumbGo.GetComponent<RectTransform>();
            StretchFull(thumbRt);
            thumbRt.offsetMin = new Vector2(3, 3);
            thumbRt.offsetMax = new Vector2(-3, -3);
            
            var thumbImg = thumbGo.GetComponent<Image>();
            thumbImg.color = Color.white; 
            
            Sprite thumbSprite = LoadSpriteWithFallback("map_thumb_" + mapName.ToLower());
            if (thumbSprite != null) {
                thumbImg.sprite = thumbSprite;
            } else {
                thumbImg.color = m_CardColor1;
            }

            var shade = CreateImage(thumbGo.transform, "UnselectedShade",
                CreateRoundedRectSprite(16, 0, Color.white, Color.clear, 0), Image.Type.Sliced, true);
            StretchFull(shade.rectTransform);
            shade.raycastTarget = false;

            var nameBgGo = CreateElement(thumbGo.transform, "NameBg", typeof(RectTransform), typeof(Image));
            var nameBgRt = nameBgGo.GetComponent<RectTransform>();
            nameBgRt.anchorMin = new Vector2(0, 0);
            nameBgRt.anchorMax = new Vector2(1, 0.34f);
            nameBgRt.offsetMin = Vector2.zero;
            nameBgRt.offsetMax = Vector2.zero;
            
            var nameBgImg = nameBgGo.GetComponent<Image>();
            nameBgImg.sprite = CreateRoundedRectSprite(16, 0, new Color(0, 0, 0, 0.7f), Color.clear, 0);
            nameBgImg.type = Image.Type.Sliced;

            var nameTxt = CreateTMP(nameBgGo.transform, "Name", mapName.ToUpper(), 14, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            StretchFull(nameTxt.rectTransform);
            nameTxt.raycastTarget = false;

            var frame = CreateImage(cardGo.transform, "SelectionFrame",
                CreateRoundedRectSprite(36, 16, Color.clear, Color.white, 4), Image.Type.Sliced, true);
            StretchFull(frame.rectTransform);
            frame.raycastTarget = false;

            var badge = CreateImage(cardGo.transform, "SelectedBadge",
                CreateRoundedRectSprite(40, 20, m_PlayButtonColor, new Color(0.10f, 0.08f, 0.02f, 0.95f), 3), Image.Type.Sliced, false);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = badge.rectTransform.pivot = new Vector2(1f, 1f);
            badge.rectTransform.sizeDelta = new Vector2(30, 30);
            badge.rectTransform.anchoredPosition = new Vector2(6, 6);
            badge.raycastTarget = false;
            var badgeLabel = CreateTMP(badge.transform, "Label", "✓", 19, FontStyles.Bold, m_TextDark, TextAlignmentOptions.Center);
            StretchFull(badgeLabel.rectTransform);
            badgeLabel.raycastTarget = false;

            // Thêm nút bấm bao toàn bộ thẻ
            var btn = cardGo.AddComponent<Button>();
            btn.targetGraphic = fill;
            var colors = btn.colors;
            colors.disabledColor = Color.white;
            btn.colors = colors;
            btn.onClick.AddListener(() => SelectMap(mapName));
            m_MapPickButtons[index] = btn;

            var vibe = cardGo.AddComponent<UIButtonVibe>();
            vibe.clickSound = m_ClickSound;

            var selectionVisual = cardGo.AddComponent<UIMapSelectionVisual>();
            selectionVisual.Configure(m_PlayButtonColor, glow, frame, shade, badge.gameObject, nameTxt, vibe);
            m_MapPickVisuals[index] = selectionVisual;

            return cardGo;
        }

        // Ô nhập text theo phong cách bo tròn của menu.
        private TMP_InputField CreateInputField(Transform parent, string placeholder, float width, float height)
        {
            var img = CreateImage(parent, "JoinCodeInput",
                CreateRoundedRectSprite(64, (int)(height / 2), Color.white, m_OutlineColor, 4), Image.Type.Sliced, false);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);

            var input = img.gameObject.AddComponent<TMP_InputField>();

            var area = CreateElement(img.transform, "TextArea", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            StretchFull(area);
            area.offsetMin = new Vector2(28, 6);
            area.offsetMax = new Vector2(-28, -6);

            var ph = CreateTMP(area, "Placeholder", placeholder, height * 0.42f, FontStyles.Italic, new Color(0.45f, 0.45f, 0.45f), TextAlignmentOptions.Left);
            var txt = CreateTMP(area, "Text", "", height * 0.42f, FontStyles.Bold, m_TextDark, TextAlignmentOptions.Left);

            input.textViewport = area;
            input.textComponent = txt;
            input.placeholder = ph;
            input.characterLimit = 10;
            input.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
            input.text = "";
            return input;
        }

        // =====================================================================
        //  HÀNH ĐỘNG
        // =====================================================================
        private async void OnCreateRoomClicked()
        {
            var mgr = NetworkLobbyManager.Instance;
            Debug.Log($"[Lobby1v1] OnCreateRoomClicked: mgr={(mgr != null)}, connState={m_ConnState}, ugsSignedIn={(UGSManager.Instance != null && UGSManager.Instance.IsSignedIn)}");
            if (mgr == null) { SetLobbyStatus("Lỗi: Không tìm thấy NetworkLobbyManager (dịch vụ mạng chưa nạp)."); return; }
            SetLobbyStatus("Đang tạo phòng...");
            try
            {
                int playerCount = RequiredOnlinePlayers;
                await mgr.CreateLobbyAsync(m_IsTeam2v2 ? "Phòng 2v2" : "Phòng 1v1", playerCount);
                Debug.Log("[Lobby1v1] CreateLobbyAsync hoàn tất.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Lobby1v1] CreateLobbyAsync lỗi: {e}");
                SetLobbyStatus("Lỗi tạo phòng: " + e.Message);
            }
        }

        private async void OnJoinRoomClicked()
        {
            var mgr = NetworkLobbyManager.Instance;
            if (mgr == null) { SetLobbyStatus("Lỗi: Không tìm thấy NetworkLobbyManager (dịch vụ mạng chưa nạp)."); return; }

            string code = m_JoinCodeInput != null ? m_JoinCodeInput.text.Trim().ToUpper() : "";
            if (string.IsNullOrEmpty(code)) { SetLobbyStatus("Vui lòng nhập mã phòng!"); return; }

            SetLobbyStatus($"Đang vào phòng {code}...");
            await mgr.JoinLobbyByCodeAsync(code);
        }

        private void OnCopyCodeClicked()
        {
            var mgr = NetworkLobbyManager.Instance;
            if (mgr != null && mgr.IsInSession && mgr.CurrentSession != null)
            {
                GUIUtility.systemCopyBuffer = mgr.CurrentSession.Code;
                SetLobbyStatus("Đã sao chép mã phòng!");
            }
        }

        private async void OnStartMatchClicked()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
            {
                SetLobbyStatus("Chỉ chủ phòng mới bắt đầu được trận đấu.");
                return;
            }
            int requiredPlayers = RequiredOnlinePlayers;
            if (NetworkManager.Singleton.ConnectedClientsList.Count < requiredPlayers)
            {
                SetLobbyStatus($"Cần đủ {requiredPlayers} người chơi mới bắt đầu được.");
                return;
            }

            EnsureHostConnectedProfiles();
            var roster = new System.Collections.Generic.Dictionary<ulong, int>();
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (!m_LobbyProfiles.TryGetValue(client.ClientId, out var profile))
                {
                    SetLobbyStatus("Chưa đồng bộ đủ danh sách người chơi. Vui lòng thử lại.");
                    return;
                }
                roster[client.ClientId] = profile.Team;
            }

            if (m_IsTeam2v2)
            {
                int blue = 0;
                int red = 0;
                foreach (int team in roster.Values)
                {
                    if (team == 0) blue++;
                    else if (team == 1) red++;
                }

                if (blue != 2 || red != 2)
                {
                    SetLobbyStatus("Mỗi đội phải có đúng 2 người trước khi bắt đầu.");
                    return;
                }
            }

            NetworkLobbyManager.Instance?.ConfigureMatchRoster(m_IsTeam2v2, roster);
            if (NetworkLobbyManager.Instance == null ||
                !await NetworkLobbyManager.Instance.LockCurrentSessionAsync())
            {
                SetLobbyStatus("Không thể khoá phòng. Trận đấu chưa bắt đầu.");
                return;
            }

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost ||
                NetworkManager.Singleton.ConnectedClientsList.Count != requiredPlayers)
            {
                await NetworkLobbyManager.Instance.UnlockCurrentSessionAsync();
                SetLobbyStatus("Có người vừa rời phòng. Hãy chờ đủ người rồi thử lại.");
                return;
            }
            SetLobbyStatus($"Đang tải bản đồ {m_SelectedMap}...");
            // Netcode sẽ tự đồng bộ scene này sang cho Khách.
            NetworkManager.Singleton.SceneManager.LoadScene(m_SelectedMap, LoadSceneMode.Single);
        }

        private async void OnLobby1v1Back()
        {
            var mgr = NetworkLobbyManager.Instance;
            Debug.Log($"[Lobby1v1] OnLobby1v1Back: mgr={(mgr != null)}, inSession={(mgr != null && mgr.IsInSession)} -> chuyển về ModeSelect.");
            if (mgr != null && mgr.IsInSession)
            {
                SetLobbyStatus("Đang rời phòng...");
                await mgr.LeaveLobbyAsync();
            }
            UpdateState(MenuState.ModeSelect);
            Debug.Log("[Lobby1v1] OnLobby1v1Back: đã gọi UpdateState(ModeSelect).");
        }

        private void SelectMap(string map)
        {
            int mapIndex = System.Array.IndexOf(k_Lobby1v1Maps, map);
            if (mapIndex < 0) return;

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && !nm.IsHost)
                return;

            ApplySelectedMap(map);

            // Host là nguồn dữ liệu duy nhất. Gửi kèm map trong full lobby state để cả
            // khách hiện tại và người vào muộn đều thấy cùng lựa chọn.
            if (nm != null && nm.IsHost)
                BroadcastAllProfiles();
        }

        private void ApplySelectedMap(string map)
        {
            int mapIndex = System.Array.IndexOf(k_Lobby1v1Maps, map);
            if (mapIndex < 0) return;

            m_SelectedMap = map;
            for (int i = 0; i < k_Lobby1v1Maps.Length; i++)
            {
                bool selected = k_Lobby1v1Maps[i] == map;
                if (m_MapPickFills[i] != null)
                    m_MapPickFills[i].color = selected ? Color.white : new Color(0.76f, 0.78f, 0.80f, 1f);
                if (m_MapPickVisuals[i] != null)
                    m_MapPickVisuals[i].SetSelected(selected, !m_MapSelectionInitialized);
            }

            if (m_SelectedMapLabel != null)
                m_SelectedMapLabel.text = $"ĐÃ CHỌN  •  {map.ToUpperInvariant()}";

            m_MapSelectionInitialized = true;
        }

        // =====================================================================
        //  MÁY TRẠNG THÁI (mỗi frame khi ở sảnh 1v1)
        // =====================================================================
        private void RefreshLobby()
        {
            var lobby = NetworkLobbyManager.Instance;
            var ugs = UGSManager.Instance;
            bool inSession = lobby != null && lobby.IsInSession;

            if (m_EntryView != null) m_EntryView.SetActive(!inSession);
            if (m_RoomView != null) m_RoomView.SetActive(inSession);

            // --- Đã ở trong phòng: chỉ cập nhật thông tin phòng ---
            if (inSession)
            {
                if (m_RetryButtonGo != null) m_RetryButtonGo.SetActive(false);
                RefreshRoom(lobby);
                return;
            }

            // --- Chưa vào phòng: máy trạng thái kết nối UGS ---
            bool servicesMissing = ugs == null || lobby == null;
            bool signedIn = ugs != null && ugs.IsSignedIn;
            if (signedIn) m_ConnState = UgsConnState.Connected;

            bool ready = signedIn && !servicesMissing;          // Success -> bật nút
            bool failed = servicesMissing || m_ConnState == UgsConnState.Failed;

            // Loading/Failure: khoá nút tương tác. Success: mở nút.
            if (m_CreateRoomButton != null) m_CreateRoomButton.interactable = ready;
            if (m_JoinRoomButton != null) m_JoinRoomButton.interactable = ready;
            if (m_RetryButtonGo != null) m_RetryButtonGo.SetActive(failed);

            if (servicesMissing)
                SetDefaultStatus("Lỗi: Thiếu UGSManager/NetworkLobbyManager trong scene. Hãy chạy từ scene Start (hoặc thêm NetworkBootstrap).");
            else if (m_ConnState == UgsConnState.Failed)
                SetDefaultStatus(string.IsNullOrEmpty(m_ConnError)
                    ? "Lỗi kết nối. Vui lòng kiểm tra internet hoặc tắt các app VPN (1.1.1.1) và thử lại."
                    : "Lỗi: " + m_ConnError);
            else if (!signedIn)
                SetDefaultStatus("Đang kết nối dịch vụ mạng...");
            else
                SetDefaultStatus("Tạo phòng mới hoặc nhập mã để vào chơi.");
        }

        private void RefreshRoom(NetworkLobbyManager lobby)
        {
            var nm = NetworkManager.Singleton;
            ISession session = lobby.CurrentSession;
            bool isHost = nm != null && nm.IsHost;

            if (isHost)
                EnsureHostConnectedProfiles();

            // Số người thật sự đang kết nối, không phải số profile đã nhận được. Chủ phòng chỉ được
            // bắt đầu khi có đủ người trên đường truyền, kể cả khi một gói ProfileSync bị lạc.
            int connected = nm != null && nm.IsListening ? nm.ConnectedClientsList.Count : 0;

            if (m_CodeLabel != null) m_CodeLabel.text = session != null ? session.Code : "------";

            // 2v2: xếp Đội Xanh vào hai slot trái, Đội Đỏ vào hai slot phải.
            // Trong cùng một đội dùng ClientId để thứ tự không nhảy giữa các frame.
            var sortedProfiles = new System.Collections.Generic.List<ProfileInfoMessage>(m_LobbyProfiles.Values);
            if (nm != null && nm.IsListening)
                sortedProfiles.RemoveAll(profile => !nm.ConnectedClients.ContainsKey(profile.ClientId));
            sortedProfiles.Sort((a, b) =>
            {
                int teamOrder = m_IsTeam2v2 ? a.Team.CompareTo(b.Team) : 0;
                return teamOrder != 0 ? teamOrder : a.ClientId.CompareTo(b.ClientId);
            });

            for (int i = 0; i < m_SlotNames.Length; i++)
            {
                bool filled = i < sortedProfiles.Count;
                string displayName = filled ? sortedProfiles[i].DisplayName : "ĐANG CHỜ...";
                string avatarId = filled ? sortedProfiles[i].AvatarId : null;

                ApplySlotVisuals(i, filled, displayName, avatarId);
            }

            if (m_HostControls != null) m_HostControls.SetActive(true);
            if (m_StartMatchButtonGo != null) m_StartMatchButtonGo.SetActive(isHost);
            if (m_WaitingLabelGo != null) m_WaitingLabelGo.SetActive(!isHost);
            for (int i = 0; i < m_MapPickButtons.Length; i++)
                if (m_MapPickButtons[i] != null) m_MapPickButtons[i].interactable = isHost;
            int requiredPlayers = RequiredOnlinePlayers;
            int blueCount = 0;
            int redCount = 0;
            foreach (var profile in sortedProfiles)
            {
                if (profile.Team == 0) blueCount++;
                else redCount++;
            }
            bool balancedTeams = !m_IsTeam2v2 || (blueCount == 2 && redCount == 2);
            bool readyToStart = connected == requiredPlayers && sortedProfiles.Count == requiredPlayers && balancedTeams;
            if (m_StartMatchButton != null) m_StartMatchButton.interactable = isHost && readyToStart;

            if (connected < requiredPlayers)
                SetDefaultStatus($"Đang chờ người chơi... ({connected}/{requiredPlayers})");
            else if (!balancedTeams)
                SetDefaultStatus("Mỗi đội cần đúng 2 người. Hãy dùng nút ĐỔI ĐỘI.");
            else
                SetDefaultStatus(m_IsTeam2v2 ? "Hai đội đã đủ người. Sẵn sàng chiến đấu." : "Hai bên đã đủ người. Sẵn sàng chiến đấu.");
        }

        // RefreshRoom chạy mỗi frame. LoadSpriteWithFallback có thể gọi Sprite.Create, nên nạp lại
        // avatar vô điều kiện sẽ sinh một Sprite mới mỗi frame. Chỉ chạm vào UI khi dữ liệu đổi.
        private readonly string[] m_SlotAppliedAvatar = new string[k_MaxOnlinePlayers];
        private readonly string[] m_SlotAppliedName = new string[k_MaxOnlinePlayers];

        private Color GetSlotAccentColor(int index)
        {
            if (m_IsTeam2v2)
                return index < 2 ? m_CyanColor : m_CardColor1;

            return index == 0 ? m_CyanColor : m_CardColor1;
        }

        private void ApplySlotVisuals(int i, bool filled, string displayName, string avatarId)
        {
            string avatarKey = filled ? (avatarId ?? "") : null;
            if (m_SlotAppliedName[i] == displayName && m_SlotAppliedAvatar[i] == avatarKey)
                return;

            m_SlotAppliedName[i] = displayName;
            m_SlotAppliedAvatar[i] = avatarKey;

            if (m_SlotFills[i] != null)
            {
                m_SlotFills[i].color = filled ? Color.white : new Color(0.70f, 0.72f, 0.74f, 0.86f);

                if (m_SlotThumbs[i] != null)
                {
                    if (filled && !string.IsNullOrEmpty(avatarId))
                    {
                        var s = LoadSpriteWithFallback(avatarId);
                        if (s != null) m_SlotThumbs[i].sprite = s;
                    }
                    else if (!filled)
                    {
                        m_SlotThumbs[i].sprite = CreateTankSilhouetteSprite(256, 150, new Color(0.18f, 0.22f, 0.24f, 1f));
                    }

                    if (m_SlotThumbs[i].sprite != null)
                        m_SlotThumbs[i].color = filled ? Color.white : new Color(0.16f, 0.22f, 0.25f, 0.72f);
                    else
                        m_SlotThumbs[i].color = filled ? m_CardColor1 : new Color(0.1f, 0.1f, 0.1f, 1f);
                }
            }

            if (m_SlotStateLabels[i] != null)
            {
                m_SlotStateLabels[i].text = filled ? "SẴN SÀNG" : "ĐANG CHỜ...";
                m_SlotStateLabels[i].color = filled ? GetSlotAccentColor(i) : new Color(1f, 1f, 1f, 0.48f);
            }

            if (m_SlotNames[i] != null)
            {
                m_SlotNames[i].text = filled ? displayName : "VỊ TRÍ TRỐNG";
                m_SlotNames[i].color = filled ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            }
        }

        private void OnRetryClicked()
        {
            var ugs = UGSManager.Instance;
            if (ugs == null)
            {
                SetLobbyStatus("Không có UGSManager trong scene — hãy khởi động game từ scene Start.");
                return;
            }
            m_ConnState = UgsConnState.Connecting;
            m_ConnError = null;
            SetLobbyStatus("Đang thử kết nối lại...");
            ugs.RetryInitialization();
        }

        private void SetLobbyStatus(string message)
        {
            if (m_Lobby1v1Status != null) m_Lobby1v1Status.text = message;
            m_StatusOverrideUntil = Time.unscaledTime + 3f;
        }

        private void SetDefaultStatus(string message)
        {
            if (Time.unscaledTime < m_StatusOverrideUntil) return;
            if (m_Lobby1v1Status != null) m_Lobby1v1Status.text = message;
        }

        // =====================================================================
        //  VÒNG ĐỜI + SỰ KIỆN
        // =====================================================================
        private void Update()
        {
            TryHookEvents();
            bool inOnlineLobby = m_CurrentState == MenuState.Lobby1v1 || m_CurrentState == MenuState.Lobby5v5;
            if (inOnlineLobby && m_WaitingRadarRt != null && m_WaitingRadarRt.gameObject.activeInHierarchy)
                m_WaitingRadarRt.Rotate(Vector3.forward, -110f * Time.unscaledDeltaTime);

            if (inOnlineLobby) RefreshLobby();
        }

        private int RequiredOnlinePlayers => m_IsTeam2v2 ? 4 : 2;

        private void SetOnlineMode(bool team2v2)
        {
            m_IsTeam2v2 = team2v2;
            ApplyOnlineModeLayout();
        }

        private void ApplyOnlineModeLayout()
        {
            if (m_OnlineLobbyTitle != null)
                m_OnlineLobbyTitle.text = m_IsTeam2v2 ? "PHÒNG ĐẤU 2v2" : "PHÒNG ĐẤU 1v1";
            if (m_VsLabel != null)
                m_VsLabel.text = "VS";
            if (m_ChangeTeamButtonGo != null)
                m_ChangeTeamButtonGo.SetActive(m_IsTeam2v2);
            if (m_BlueTeamHeaderGo != null)
                m_BlueTeamHeaderGo.SetActive(m_IsTeam2v2);
            if (m_RedTeamHeaderGo != null)
                m_RedTeamHeaderGo.SetActive(m_IsTeam2v2);

            Vector2[] teamPositions =
            {
                new Vector2(-475f, 105f),
                new Vector2(-475f, -86f),
                new Vector2(475f, 105f),
                new Vector2(475f, -86f)
            };
            for (int i = 0; i < m_RoomSlots.Length; i++)
            {
                if (m_RoomSlots[i] == null) continue;
                bool visible = m_IsTeam2v2 || i < 2;
                m_RoomSlots[i].SetActive(visible);
                if (!visible) continue;

                var rt = m_RoomSlots[i].GetComponent<RectTransform>();
                rt.anchoredPosition = m_IsTeam2v2
                    ? teamPositions[i]
                    : new Vector2(i == 0 ? -475f : 475f, 8f);

                if (m_SlotTeamBars[i] != null)
                    m_SlotTeamBars[i].color = GetSlotAccentColor(i);

                // Ép ApplySlotVisuals chạy lại khi đổi layout/mode vì cùng profile nhưng màu đội khác.
                m_SlotAppliedName[i] = null;
                m_SlotAppliedAvatar[i] = null;
            }
        }

        private void OnDestroy()
        {
            var lobby = NetworkLobbyManager.Instance;
            if (lobby != null && m_LobbyEventsHooked)
            {
                lobby.OnSessionCreated -= HandleSessionChanged;
                lobby.OnSessionJoined -= HandleSessionChanged;
                lobby.OnSessionLeft -= HandleSessionLeft;
                lobby.OnSessionError -= HandleSessionError;
                lobby.OnPeerLeft -= HandlePeerLeft;
            }

            var ugs = UGSManager.Instance;
            if (ugs != null && m_UgsEventsHooked)
            {
                ugs.OnPlayerSignedIn -= HandleSignedIn;
                ugs.OnSignInFailed -= HandleUgsFailed;
                ugs.OnInitializationFailed -= HandleUgsFailed;
            }

            if (NetworkManager.Singleton != null && m_NetcodeEventsHooked)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                if (NetworkManager.Singleton.CustomMessagingManager != null)
                {
                    NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("ProfileSync");
                    NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("FullProfileSync");
                    NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("TeamChangeRequest");
                }
            }

            if (ProfileManager.Instance != null)
            {
                ProfileManager.Instance.OnProfileLoaded -= UpdateTopBarProfileInfo;
                ProfileManager.Instance.OnProfileSaveSuccess -= UpdateTopBarProfileInfo;
            }
        }

        private void TryHookEvents()
        {
            var lobby = NetworkLobbyManager.Instance;
            if (lobby != null && !m_LobbyEventsHooked)
            {
                lobby.OnSessionCreated += HandleSessionChanged;
                lobby.OnSessionJoined += HandleSessionChanged;
                lobby.OnSessionLeft += HandleSessionLeft;
                lobby.OnSessionError += HandleSessionError;
                lobby.OnPeerLeft += HandlePeerLeft;
                m_LobbyEventsHooked = true;
            }

            // Netcode huỷ CustomMessagingManager khi shutdown, kéo theo mọi handler đã đăng ký.
            // Nếu không hạ cờ này, lần vào phòng sau sẽ không đăng ký lại và profile sync chết hẳn.
            if (m_NetcodeEventsHooked && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening))
            {
                m_NetcodeEventsHooked = false;
                m_LobbyProfiles.Clear();
            }

            var ugs = UGSManager.Instance;
            if (ugs != null && !m_UgsEventsHooked)
            {
                ugs.OnPlayerSignedIn += HandleSignedIn;
                ugs.OnSignInFailed += HandleUgsFailed;
                ugs.OnInitializationFailed += HandleUgsFailed;
                m_UgsEventsHooked = true;

                if (ugs.IsSignedIn) m_ConnState = UgsConnState.Connected;
                else if (ugs.State == UGSManager.ConnectionState.Failed) m_ConnState = UgsConnState.Failed;
            }

            if (NetworkManager.Singleton != null && !m_NetcodeEventsHooked)
            {
                if (NetworkManager.Singleton.CustomMessagingManager != null)
                {
                    NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                    NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ProfileSync", OnProfileSyncMessage);
                    NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("FullProfileSync", OnFullProfileSync);
                    NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("TeamChangeRequest", OnTeamChangeRequest);
                    m_NetcodeEventsHooked = true;
                    
                    if (NetworkManager.Singleton.IsHost)
                    {
                        UpdateLocalProfile();
                    }
                }
            }
        }

        private void HandleSignedIn(string playerId)
        {
            m_ConnState = UgsConnState.Connected;
            m_ConnError = null;
            SetLobbyStatus("Đã kết nối!");
        }

        private void HandleUgsFailed(string error)
        {
            m_ConnState = UgsConnState.Failed;
            m_ConnError = error;
            SetLobbyStatus("Lỗi: " + error);
        }

        private void HandleSessionChanged(ISession session)
        {
            SetLobbyStatus($"Đã vào phòng: {session.Code}");
            if (m_CodeLabel != null) m_CodeLabel.text = session.Code;
            
            // Client kết nối thành công, báo cho host
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsHost)
            {
                SendProfileToHost();
            }

            RefreshLobby();
        }

        private void HandleSessionLeft()
        {
            SetLobbyStatus("Đã rời phòng.");
            if (NetworkManager.Singleton != null && m_NetcodeEventsHooked)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                if (NetworkManager.Singleton.CustomMessagingManager != null)
                {
                    NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("ProfileSync");
                    NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("FullProfileSync");
                    NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("TeamChangeRequest");
                }
                m_NetcodeEventsHooked = false;
            }
            m_LobbyProfiles.Clear();
            RefreshLobby();
        }

        private void HandleSessionError(string errorMsg)
        {
            SetLobbyStatus($"Lỗi: {errorMsg}");
        }

        // Chủ phòng: đối thủ rời sảnh. Bỏ họ khỏi danh sách rồi báo lại cho các máy còn lại.
        private void HandlePeerLeft(ulong clientId)
        {
            if (!m_LobbyProfiles.Remove(clientId)) return;

            SetLobbyStatus("Một người chơi đã rời phòng.");
            BroadcastAllProfiles();
            RefreshLobby();
        }

        // =====================================================================
        //  PROFILE SYNC LOGIC (NETCODE CUSTOM MESSAGING)
        // =====================================================================

        private void UpdateLocalProfile()
        {
            if (NetworkManager.Singleton == null) return;
            ulong myId = NetworkManager.Singleton.LocalClientId;
            int team = m_LobbyProfiles.TryGetValue(myId, out var current)
                ? current.Team
                : ChooseBalancedTeam();
            m_LobbyProfiles[myId] = new ProfileInfoMessage {
                ClientId = myId,
                DisplayName = ProfileManager.Instance?.DisplayName ?? "Player",
                AvatarId = ProfileManager.Instance?.AvatarId ?? "avatar_1",
                Team = team
            };
        }

        // Host luôn tạo placeholder cho mọi kết nối thật. Vì vậy một gói ProfileSync bị lạc
        // không làm roster thiếu người hoặc khiến nút Bắt đầu dùng sai slot.
        private void EnsureHostConnectedProfiles()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsHost) return;

            bool changed = false;
            foreach (var client in nm.ConnectedClientsList)
            {
                if (m_LobbyProfiles.ContainsKey(client.ClientId)) continue;

                bool isLocal = client.ClientId == nm.LocalClientId;
                m_LobbyProfiles[client.ClientId] = new ProfileInfoMessage
                {
                    ClientId = client.ClientId,
                    DisplayName = isLocal ? (ProfileManager.Instance?.DisplayName ?? "Player") : "Player",
                    AvatarId = isLocal ? (ProfileManager.Instance?.AvatarId ?? "avatar_1") : "avatar_1",
                    Team = ChooseBalancedTeam()
                };
                changed = true;
            }

            if (changed)
                BroadcastAllProfiles();
        }

        private int ChooseBalancedTeam()
        {
            int blue = 0;
            int red = 0;
            foreach (var profile in m_LobbyProfiles.Values)
            {
                if (profile.Team == 0) blue++;
                else red++;
            }
            return blue <= red ? 0 : 1;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton.IsHost)
            {
                EnsureHostConnectedProfiles();
            }
            else if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                SendProfileToHost();
            }
        }

        // Giới hạn dữ liệu nhận từ mạng: buffer gửi có trần cố định, và một client sửa đổi
        // có thể gửi chuỗi dài tuỳ ý. Cắt ngắn ngay khi đọc.
        private const int k_MaxNameLength = 24;
        private const int k_ProfileBufferSize = 512;
        private const int k_ProfileBufferMax = 8 * 1024;
        private const int k_LobbyStateVersion = 1;

        private static string Sanitize(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            value = value.Trim();
            return value.Length > k_MaxNameLength ? value.Substring(0, k_MaxNameLength) : value;
        }

        private void SendProfileToHost()
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.CustomMessagingManager == null) return;

            var writer = new FastBufferWriter(k_ProfileBufferSize, Unity.Collections.Allocator.Temp, k_ProfileBufferMax);
            using (writer) {
                writer.WriteValueSafe(Sanitize(ProfileManager.Instance?.DisplayName, "Player"));
                writer.WriteValueSafe(Sanitize(ProfileManager.Instance?.AvatarId, "avatar_1"));
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("ProfileSync", NetworkManager.ServerClientId, writer);
            }
        }

        private void OnProfileSyncMessage(ulong senderId, FastBufferReader payload)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost) return;

            string displayName, avatarId;
            try
            {
                payload.ReadValueSafe(out displayName);
                payload.ReadValueSafe(out avatarId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Lobby1v1] Bỏ qua ProfileSync hỏng từ client {senderId}: {e.Message}");
                return;
            }

            // Khoá theo senderId do Netcode cung cấp, KHÔNG theo id trong payload: người gửi
            // không được phép tự khai mình là ai (nếu không họ ghi đè được profile của chủ phòng).
            int team = m_LobbyProfiles.TryGetValue(senderId, out var current)
                ? current.Team
                : ChooseBalancedTeam();
            m_LobbyProfiles[senderId] = new ProfileInfoMessage {
                ClientId = senderId,
                DisplayName = Sanitize(displayName, "Player"),
                AvatarId = Sanitize(avatarId, "avatar_1"),
                Team = team
            };

            if (NetworkManager.Singleton.IsHost)
            {
                BroadcastAllProfiles(); // Chuyển tiếp cho các client khác
            }
            RefreshLobby();
        }

        private void BroadcastAllProfiles()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost || NetworkManager.Singleton.CustomMessagingManager == null) return;

            var writer = new FastBufferWriter(k_ProfileBufferSize, Unity.Collections.Allocator.Temp, k_ProfileBufferMax);
            using (writer) {
                writer.WriteValueSafe(k_LobbyStateVersion);
                writer.WriteValueSafe(m_IsTeam2v2);
                writer.WriteValueSafe(System.Array.IndexOf(k_Lobby1v1Maps, m_SelectedMap));
                writer.WriteValueSafe(m_LobbyProfiles.Count);
                foreach (var kvp in m_LobbyProfiles)
                {
                    writer.WriteValueSafe(kvp.Value.ClientId);
                    writer.WriteValueSafe(kvp.Value.DisplayName);
                    writer.WriteValueSafe(kvp.Value.AvatarId);
                    writer.WriteValueSafe(kvp.Value.Team);
                }
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("FullProfileSync", writer);
            }
        }

        private void OnFullProfileSync(ulong senderId, FastBufferReader payload)
        {
            if (NetworkManager.Singleton.IsHost) return;
            if (senderId != NetworkManager.ServerClientId) return;

            var received = new System.Collections.Generic.Dictionary<ulong, ProfileInfoMessage>();
            try
            {
                payload.ReadValueSafe(out int stateVersion);
                if (stateVersion != k_LobbyStateVersion)
                    throw new System.InvalidOperationException("Phiên bản lobby state không tương thích.");

                payload.ReadValueSafe(out bool teamMode);
                payload.ReadValueSafe(out int selectedMapIndex);
                if (selectedMapIndex < 0 || selectedMapIndex >= k_Lobby1v1Maps.Length)
                    throw new System.InvalidOperationException("Chỉ số bản đồ không hợp lệ.");

                payload.ReadValueSafe(out int count);
                if (count < 0 || count > k_MaxOnlinePlayers)
                    throw new System.InvalidOperationException("Số profile vượt giới hạn lobby.");
                for (int i = 0; i < count; i++)
                {
                    payload.ReadValueSafe(out ulong cId);
                    payload.ReadValueSafe(out string dName);
                    payload.ReadValueSafe(out string aId);
                    payload.ReadValueSafe(out int team);
                    received[cId] = new ProfileInfoMessage {
                        ClientId = cId,
                        DisplayName = Sanitize(dName, "Player"),
                        AvatarId = Sanitize(aId, "avatar_1"),
                        Team = Mathf.Clamp(team, 0, 1)
                    };
                }

                if (m_IsTeam2v2 != teamMode)
                    SetOnlineMode(teamMode);

                ApplySelectedMap(k_Lobby1v1Maps[selectedMapIndex]);
            }
            catch (System.Exception e)
            {
                // Giữ nguyên danh sách cũ thay vì xoá sạch UI vì một gói tin hỏng.
                Debug.LogWarning($"[Lobby1v1] Bỏ qua FullProfileSync hỏng: {e.Message}");
                return;
            }

            m_LobbyProfiles = received;
            RefreshLobby();
        }

        private void OnChangeTeamClicked()
        {
            var nm = NetworkManager.Singleton;
            if (!m_IsTeam2v2 || nm == null || !nm.IsListening) return;
            if (!m_LobbyProfiles.TryGetValue(nm.LocalClientId, out var profile))
            {
                SetLobbyStatus("Đang đồng bộ đội. Vui lòng thử lại.");
                return;
            }

            int targetTeam = 1 - profile.Team;
            int targetCount = 0;
            foreach (var player in m_LobbyProfiles.Values)
                if (player.Team == targetTeam) targetCount++;

            if (targetCount >= 2)
            {
                SetLobbyStatus(targetTeam == 0 ? "Đội Xanh đã đủ 2 người." : "Đội Đỏ đã đủ 2 người.");
                return;
            }

            if (nm.IsHost)
            {
                TryChangeTeam(nm.LocalClientId, targetTeam);
                return;
            }

            if (nm.CustomMessagingManager == null) return;
            var writer = new FastBufferWriter(sizeof(int), Unity.Collections.Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(targetTeam);
                nm.CustomMessagingManager.SendNamedMessage("TeamChangeRequest", NetworkManager.ServerClientId, writer);
            }
            SetLobbyStatus("Đã gửi yêu cầu đổi đội.");
        }

        private void OnTeamChangeRequest(ulong senderId, FastBufferReader payload)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsHost || !m_IsTeam2v2) return;

            try
            {
                payload.ReadValueSafe(out int targetTeam);
                TryChangeTeam(senderId, targetTeam);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Lobby1v1] Bỏ qua TeamChangeRequest hỏng từ client {senderId}: {e.Message}");
            }
        }

        private void TryChangeTeam(ulong clientId, int targetTeam)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsHost || !nm.ConnectedClients.ContainsKey(clientId)) return;
            if (!m_LobbyProfiles.TryGetValue(clientId, out var profile)) return;

            targetTeam = Mathf.Clamp(targetTeam, 0, 1);
            if (profile.Team == targetTeam) return;

            int targetCount = 0;
            foreach (var player in m_LobbyProfiles.Values)
                if (player.Team == targetTeam) targetCount++;
            if (targetCount >= 2) return;

            profile.Team = targetTeam;
            m_LobbyProfiles[clientId] = profile;
            BroadcastAllProfiles();
            SetLobbyStatus(targetTeam == 0 ? "Đã chuyển sang Đội Xanh." : "Đã chuyển sang Đội Đỏ.");
        }
    }
}
