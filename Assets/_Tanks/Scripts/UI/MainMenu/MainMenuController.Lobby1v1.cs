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
        private readonly TextMeshProUGUI[] m_SlotNames = new TextMeshProUGUI[2];
        private readonly TextMeshProUGUI[] m_SlotStateLabels = new TextMeshProUGUI[2];
        private readonly Image[] m_SlotFills = new Image[2];
        private readonly Image[] m_SlotThumbs = new Image[2];
        private readonly Image[] m_MapPickFills = new Image[3];
        private RectTransform m_WaitingRadarRt;
        private GameObject m_HostControls;   // Chọn map + nút Bắt đầu (chỉ Chủ phòng thấy)
        private GameObject m_WaitingLabelGo;  // Nhãn "Chờ chủ phòng..." (chỉ Khách thấy)
        private Button m_StartMatchButton;

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

            // Phủ một lớp bóng tối (dimmer) mờ 35% để làm nổi bật các Panel UI ở giữa sảnh
            var dimmer = CreateElement(panel.transform, "Dimmer", typeof(RectTransform), typeof(Image));
            StretchFull(dimmer.GetComponent<RectTransform>());
            dimmer.GetComponent<Image>().color = new Color(0, 0, 0, 0.35f);

            // Tiêu đề
            var titleRt = CreateElement(panel.transform, "TitleContainer", typeof(RectTransform)).GetComponent<RectTransform>();
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.88f);
            titleRt.anchoredPosition = Vector2.zero;
            CreateShadowedTitle(titleRt.transform, "ĐẤU TRƯỜNG 1v1", 80f);

            // Nút quay lại / rời phòng
            var backRt = CreateElement(panel.transform, "BackBtn", typeof(RectTransform)).GetComponent<RectTransform>();
            backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
            backRt.anchoredPosition = new Vector2(180, -80);
            CreatePillButton(backRt, "< TRỞ VỀ", m_CardColor1, 260, 70, 34, OnLobby1v1Back);

            // Thanh trạng thái dưới cùng
            var statusRt = CreateElement(panel.transform, "Status", typeof(RectTransform)).GetComponent<RectTransform>();
            statusRt.anchorMin = statusRt.anchorMax = new Vector2(0.5f, 0.08f);
            statusRt.sizeDelta = new Vector2(1400, 60);
            statusRt.anchoredPosition = Vector2.zero;
            m_Lobby1v1Status = CreateTMP(statusRt, "Label", "", 34, FontStyles.Bold | FontStyles.Italic, Color.white, TextAlignmentOptions.Center);

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
            view.sizeDelta = new Vector2(1400, 760);
            view.anchoredPosition = Vector2.zero;
            m_RoomView = view.gameObject;
            m_RoomView.SetActive(false);

            // 1. Cụm Mã Phòng ở phía trên (Thu gọn lại)
            var codeGroup = CreateElement(view, "CodeGroup", typeof(RectTransform)).GetComponent<RectTransform>();
            codeGroup.anchorMin = codeGroup.anchorMax = codeGroup.pivot = new Vector2(0.5f, 1f);
            codeGroup.anchoredPosition = new Vector2(0, -20);
            codeGroup.sizeDelta = new Vector2(800, 100);

            var codeTitle = CreateTMP(codeGroup, "CodeTitle", "MÃ PHÒNG:", 26, FontStyles.Bold, new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.Center);
            codeTitle.rectTransform.anchorMin = codeTitle.rectTransform.anchorMax = codeTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            codeTitle.rectTransform.sizeDelta = new Vector2(300, 30);
            codeTitle.rectTransform.anchoredPosition = new Vector2(0, 0);

            // Bảng mã phòng trắng viền đen
            var codeBox = CreateImage(codeGroup, "CodeBox",
                CreateRoundedRectSprite(64, 40, Color.white, m_OutlineColor, 4), Image.Type.Sliced, true);
            var codeBoxRt = codeBox.rectTransform;
            codeBoxRt.anchorMin = codeBoxRt.anchorMax = codeBoxRt.pivot = new Vector2(0.5f, 1f);
            codeBoxRt.sizeDelta = new Vector2(320, 70);
            codeBoxRt.anchoredPosition = new Vector2(-120, -30);

            m_CodeLabel = CreateTMP(codeBoxRt, "CodeLabel", "------", 50, FontStyles.Bold, m_TextDark, TextAlignmentOptions.Center);
            m_CodeLabel.rectTransform.anchorMin = m_CodeLabel.rectTransform.anchorMax = m_CodeLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            m_CodeLabel.rectTransform.sizeDelta = new Vector2(300, 60);
            m_CodeLabel.rectTransform.anchoredPosition = Vector2.zero;
            m_CodeLabel.characterSpacing = 6f;

            var copyHolder = CreateElement(codeGroup, "CopyHolder", typeof(RectTransform)).GetComponent<RectTransform>();
            copyHolder.anchorMin = copyHolder.anchorMax = copyHolder.pivot = new Vector2(0.5f, 1f);
            copyHolder.anchoredPosition = new Vector2(130, -30);
            CreatePillButton(copyHolder, "SAO CHÉP", m_CardColor2, 160, 70, 22, OnCopyCodeClicked);

            // 2. Hai bay đối đầu: bên đã vào phòng là LOCKED, bên còn lại là silhouette đang quét.
            var slot0 = CreateRoomSlot(view, 0);
            slot0.GetComponent<RectTransform>().anchoredPosition = new Vector2(-420, 70);
            
            var vsRt = CreateElement(view, "VSLabel", typeof(RectTransform)).GetComponent<RectTransform>();
            vsRt.anchorMin = vsRt.anchorMax = vsRt.pivot = new Vector2(0.5f, 0.5f);
            vsRt.sizeDelta = new Vector2(200, 100);
            vsRt.anchoredPosition = new Vector2(0, 70);
            CreateTMP(vsRt, "VS", "VS", 100, FontStyles.Bold | FontStyles.Italic, m_PlayButtonColor, TextAlignmentOptions.Center);
            
            var slot1 = CreateRoomSlot(view, 1);
            slot1.GetComponent<RectTransform>().anchoredPosition = new Vector2(420, 70);

            // 3. Khu điều khiển của Chủ phòng (Nằm gọn dưới đáy Y = 0)
            var host = CreateElement(view, "HostControls", typeof(RectTransform)).GetComponent<RectTransform>();
            host.anchorMin = host.anchorMax = host.pivot = new Vector2(0.5f, 0f);
            host.sizeDelta = new Vector2(1000, 260);
            host.anchoredPosition = new Vector2(0, 0);
            m_HostControls = host.gameObject;

            float[] xs = { -250, 0, 250 };
            for (int i = 0; i < k_Lobby1v1Maps.Length; i++)
            {
                string map = k_Lobby1v1Maps[i];
                CreateMapCard(host, map, xs[i], i);
            }

            var startHolder = CreateElement(host, "StartHolder", typeof(RectTransform)).GetComponent<RectTransform>();
            startHolder.anchorMin = startHolder.anchorMax = startHolder.pivot = new Vector2(0.5f, 0f);
            startHolder.anchoredPosition = new Vector2(0, 18); // Nằm dưới cụm map, không che thẻ.
            m_StartMatchButton = CreatePillButton(startHolder, "BẮT ĐẦU TRẬN ĐẤU", m_PlayButtonColor, 440, 74, 30, OnStartMatchClicked);

            // Nhãn chờ (Khách), dạng banner radar thay vì text trần.
            var waitRt = CreateElement(view, "WaitingLabel", typeof(RectTransform)).GetComponent<RectTransform>();
            waitRt.anchorMin = waitRt.anchorMax = waitRt.pivot = new Vector2(0.5f, 0f);
            waitRt.sizeDelta = new Vector2(820, 72);
            waitRt.anchoredPosition = new Vector2(0, 100);
            var waitBg = CreateImage(waitRt, "Fill",
                CreateRoundedRectSprite(64, 32, new Color(0.05f, 0.07f, 0.08f, 0.84f), m_PlayButtonColor, 3), Image.Type.Sliced, true);
            var radar = CreateImage(waitBg.transform, "Radar",
                CreateRadarSprite(96, m_CyanColor, new Color(1f, 1f, 1f, 0.28f)), Image.Type.Simple, false);
            m_WaitingRadarRt = radar.rectTransform;
            m_WaitingRadarRt.anchorMin = m_WaitingRadarRt.anchorMax = m_WaitingRadarRt.pivot = new Vector2(0f, 0.5f);
            m_WaitingRadarRt.sizeDelta = new Vector2(46, 46);
            m_WaitingRadarRt.anchoredPosition = new Vector2(36, 0);
            var waitLabel = CreateTMP(waitBg.transform, "Label", "ĐÃ SẴN SÀNG - ĐANG CHỜ CHỦ PHÒNG", 28,
                FontStyles.Bold | FontStyles.Italic, Color.white, TextAlignmentOptions.Center);
            waitLabel.rectTransform.offsetMin = new Vector2(86, 0);
            waitLabel.rectTransform.offsetMax = new Vector2(-24, 0);
            m_WaitingLabelGo = waitRt.gameObject;

            SelectMap(m_SelectedMap);
        }

        private GameObject CreateRoomSlot(Transform parent, int index)
        {
            var slotGo = CreateElement(parent, $"Slot_{index}", typeof(RectTransform));
            var rt = slotGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(340, 430);

            CreateShadow(slotGo.transform, 96, 32, new Vector2(12, -14), new Vector2(12, -14), 0.30f);

            // Nền bay/viền.
            var fill = CreateImage(slotGo.transform, "Fill",
                CreateRoundedRectSprite(100, 24, new Color(0.05f, 0.07f, 0.08f, 0.88f), Color.white, 5), Image.Type.Sliced, true);
            m_SlotFills[index] = fill;
            StretchFull(fill.rectTransform);

            var scanLine = CreateImage(fill.transform, "ScanLine",
                CreateRoundedRectSprite(16, 0, new Color(1f, 1f, 1f, 0.18f), Color.clear, 0), Image.Type.Sliced, false);
            scanLine.rectTransform.anchorMin = new Vector2(0.09f, 0.68f);
            scanLine.rectTransform.anchorMax = new Vector2(0.91f, 0.68f);
            scanLine.rectTransform.sizeDelta = new Vector2(0, 4);
            scanLine.rectTransform.anchoredPosition = Vector2.zero;

            // Vùng avatar/silhouette.
            var thumbGo = CreateElement(fill.transform, "Thumb", typeof(RectTransform), typeof(Image));
            var thumbRt = thumbGo.GetComponent<RectTransform>();
            thumbRt.anchorMin = Vector2.zero;
            thumbRt.anchorMax = Vector2.one;
            thumbRt.offsetMin = new Vector2(10, 10);
            thumbRt.offsetMax = new Vector2(-10, -10);
            
            var thumbImg = thumbGo.GetComponent<Image>();
            m_SlotThumbs[index] = thumbImg;
            string avatarName = index == 0 ? "avatar_commander_host" : "avatar_commander_guest";
            Sprite avatarSprite = LoadSpriteWithFallback(avatarName);
            if (avatarSprite != null) {
                thumbImg.sprite = avatarSprite;
                thumbImg.color = new Color(0.18f, 0.22f, 0.24f, 1f);
            } else {
                thumbImg.sprite = CreateTankSilhouetteSprite(256, 150, new Color(0.18f, 0.22f, 0.24f, 1f));
                thumbImg.color = Color.white;
            }

            var topShade = CreateImage(fill.transform, "TopShade",
                CreateVerticalGradientSprite(new Color(0f, 0f, 0f, 0.72f), new Color(0f, 0f, 0f, 0.08f)), Image.Type.Simple, false);
            topShade.rectTransform.anchorMin = new Vector2(0.03f, 0.70f);
            topShade.rectTransform.anchorMax = new Vector2(0.97f, 0.97f);
            topShade.rectTransform.offsetMin = Vector2.zero;
            topShade.rectTransform.offsetMax = Vector2.zero;

            var bottomShade = CreateImage(fill.transform, "BottomShade",
                CreateVerticalGradientSprite(new Color(0f, 0f, 0f, 0.10f), new Color(0f, 0f, 0f, 0.76f)), Image.Type.Simple, false);
            bottomShade.rectTransform.anchorMin = new Vector2(0.03f, 0.03f);
            bottomShade.rectTransform.anchorMax = new Vector2(0.97f, 0.32f);
            bottomShade.rectTransform.offsetMin = Vector2.zero;
            bottomShade.rectTransform.offsetMax = Vector2.zero;

            // Tên người chơi nằm trên đầu card, không hiển thị tên tank.
            var name = CreateTMP(fill.transform, "Name", "", 32, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            name.rectTransform.anchorMin = new Vector2(0, 0.82f);
            name.rectTransform.anchorMax = new Vector2(1, 0.98f);
            name.rectTransform.offsetMin = new Vector2(18, 0);
            name.rectTransform.offsetMax = new Vector2(-18, -6);

            var state = CreateTMP(fill.transform, "State", "ĐANG QUÉT...", 24,
                FontStyles.Bold | FontStyles.Italic, m_CyanColor, TextAlignmentOptions.Center);
            state.rectTransform.anchorMin = new Vector2(0, 0.04f);
            state.rectTransform.anchorMax = new Vector2(1, 0.16f);
            state.rectTransform.offsetMin = new Vector2(18, 0);
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
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(220, 120);
            rt.anchoredPosition = new Vector2(posX, 220);

            // Nền thẻ bản đồ (Border & Background)
            var fill = CreateImage(cardGo.transform, "Fill",
                CreateRoundedRectSprite(32, 20, new Color(0.1f, 0.1f, 0.1f), m_OutlineColor, 4), Image.Type.Sliced, true);
            m_MapPickFills[index] = fill;
            StretchFull(fill.rectTransform);

            // Ảnh thu nhỏ bản đồ (Tràn viền)
            var thumbGo = CreateElement(fill.transform, "Thumb", typeof(RectTransform), typeof(Image));
            var thumbRt = thumbGo.GetComponent<RectTransform>();
            StretchFull(thumbRt);
            thumbRt.offsetMin = new Vector2(4, 4);
            thumbRt.offsetMax = new Vector2(-4, -4);
            
            var thumbImg = thumbGo.GetComponent<Image>();
            thumbImg.color = Color.white; 
            
            Sprite thumbSprite = LoadSpriteWithFallback("map_thumb_" + mapName.ToLower());
            if (thumbSprite != null) {
                thumbImg.sprite = thumbSprite;
            } else {
                thumbImg.color = m_CardColor1;
            }

            // Tên bản đồ (Nằm dưới cùng, đè lên ảnh với nền đen bán trong suốt)
            var nameBgGo = CreateElement(thumbGo.transform, "NameBg", typeof(RectTransform), typeof(Image));
            var nameBgRt = nameBgGo.GetComponent<RectTransform>();
            nameBgRt.anchorMin = new Vector2(0, 0);
            nameBgRt.anchorMax = new Vector2(1, 0.35f);
            nameBgRt.offsetMin = Vector2.zero;
            nameBgRt.offsetMax = Vector2.zero;
            
            var nameBgImg = nameBgGo.GetComponent<Image>();
            nameBgImg.sprite = CreateRoundedRectSprite(16, 0, new Color(0, 0, 0, 0.7f), Color.clear, 0);
            nameBgImg.type = Image.Type.Sliced;

            var nameTxt = CreateTMP(nameBgGo.transform, "Name", mapName.ToUpper(), 22, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            StretchFull(nameTxt.rectTransform);

            // Thêm nút bấm bao toàn bộ thẻ
            var btn = cardGo.AddComponent<Button>();
            btn.targetGraphic = fill;
            btn.onClick.AddListener(() => SelectMap(mapName));

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
                await mgr.CreateLobbyAsync("Phòng 1v1", 2);
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

        private void OnStartMatchClicked()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
            {
                SetLobbyStatus("Chỉ chủ phòng mới bắt đầu được trận đấu.");
                return;
            }
            if (NetworkManager.Singleton.ConnectedClientsList.Count < 2)
            {
                SetLobbyStatus("Cần đủ 2 người chơi mới bắt đầu được.");
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
            m_SelectedMap = map;
            for (int i = 0; i < k_Lobby1v1Maps.Length; i++)
            {
                if (m_MapPickFills[i] == null) continue;
                bool selected = k_Lobby1v1Maps[i] == map;
                m_MapPickFills[i].color = selected ? m_PlayButtonColor : new Color(0.2f, 0.2f, 0.2f, 1f);
            }
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

            // Số người thật sự đang kết nối, không phải số profile đã nhận được. Chủ phòng chỉ được
            // bắt đầu khi có đủ người trên đường truyền, kể cả khi một gói ProfileSync bị lạc.
            int connected = nm != null && nm.IsListening ? nm.ConnectedClientsList.Count : 0;

            if (m_CodeLabel != null) m_CodeLabel.text = session != null ? session.Code : "------";

            // Sắp xếp các Profile theo ClientId để hiển thị cố định
            var sortedProfiles = new System.Collections.Generic.List<ProfileInfoMessage>(m_LobbyProfiles.Values);
            sortedProfiles.Sort((a, b) => a.ClientId.CompareTo(b.ClientId));

            for (int i = 0; i < m_SlotNames.Length; i++)
            {
                bool filled = i < sortedProfiles.Count;
                string displayName = filled ? sortedProfiles[i].DisplayName : "ĐANG CHỜ...";
                string avatarId = filled ? sortedProfiles[i].AvatarId : null;

                ApplySlotVisuals(i, filled, displayName, avatarId);
            }

            if (m_HostControls != null) m_HostControls.SetActive(isHost);
            if (m_WaitingLabelGo != null) m_WaitingLabelGo.SetActive(!isHost);
            if (m_StartMatchButton != null) m_StartMatchButton.interactable = isHost && connected >= 2;

            SetDefaultStatus(connected >= 2 ? "Hai bên đã khóa lựa chọn. Sẵn sàng chiến đấu." : "Đã khóa lựa chọn. Đang chờ đối thủ...");
        }

        // RefreshRoom chạy mỗi frame. LoadSpriteWithFallback có thể gọi Sprite.Create, nên nạp lại
        // avatar vô điều kiện sẽ sinh một Sprite mới mỗi frame. Chỉ chạm vào UI khi dữ liệu đổi.
        private readonly string[] m_SlotAppliedAvatar = new string[2];
        private readonly string[] m_SlotAppliedName = new string[2];

        private void ApplySlotVisuals(int i, bool filled, string displayName, string avatarId)
        {
            string avatarKey = filled ? (avatarId ?? "") : null;
            if (m_SlotAppliedName[i] == displayName && m_SlotAppliedAvatar[i] == avatarKey)
                return;

            m_SlotAppliedName[i] = displayName;
            m_SlotAppliedAvatar[i] = avatarKey;

            if (m_SlotFills[i] != null)
            {
                Color filledColor = i == 0 ? m_PlayButtonColor : m_CyanColor;
                m_SlotFills[i].color = filled ? filledColor : new Color(0.15f, 0.15f, 0.15f, 0.8f);

                if (m_SlotThumbs[i] != null)
                {
                    if (filled && !string.IsNullOrEmpty(avatarId))
                    {
                        var s = LoadSpriteWithFallback(avatarId);
                        if (s != null) m_SlotThumbs[i].sprite = s;
                    }
                    else if (!filled && m_SlotThumbs[i].sprite == null)
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
                m_SlotStateLabels[i].text = filled ? "ĐÃ KHÓA" : "ĐANG QUÉT...";
                m_SlotStateLabels[i].color = filled ? m_PlayButtonColor : m_CyanColor;
            }

            if (m_SlotNames[i] != null)
            {
                m_SlotNames[i].text = displayName;
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
            if (m_CurrentState == MenuState.Lobby1v1 && m_WaitingRadarRt != null && m_WaitingRadarRt.gameObject.activeInHierarchy)
                m_WaitingRadarRt.Rotate(Vector3.forward, -110f * Time.unscaledDeltaTime);

            if (m_CurrentState == MenuState.Lobby1v1) RefreshLobby();
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

            SetLobbyStatus("Đối thủ đã rời phòng.");
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
            m_LobbyProfiles[myId] = new ProfileInfoMessage {
                ClientId = myId,
                DisplayName = ProfileManager.Instance?.DisplayName ?? "Player",
                AvatarId = ProfileManager.Instance?.AvatarId ?? "avatar_1"
            };
        }

        private void OnClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton.IsHost)
            {
                // Khi có người mới vào, Broadcast toàn bộ lại
                BroadcastAllProfiles();
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
            m_LobbyProfiles[senderId] = new ProfileInfoMessage {
                ClientId = senderId,
                DisplayName = Sanitize(displayName, "Player"),
                AvatarId = Sanitize(avatarId, "avatar_1")
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
                writer.WriteValueSafe(m_LobbyProfiles.Count);
                foreach (var kvp in m_LobbyProfiles)
                {
                    writer.WriteValueSafe(kvp.Value.ClientId);
                    writer.WriteValueSafe(kvp.Value.DisplayName);
                    writer.WriteValueSafe(kvp.Value.AvatarId);
                }
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("FullProfileSync", writer);
            }
        }

        private void OnFullProfileSync(ulong senderId, FastBufferReader payload)
        {
            if (NetworkManager.Singleton.IsHost) return;

            var received = new System.Collections.Generic.Dictionary<ulong, ProfileInfoMessage>();
            try
            {
                payload.ReadValueSafe(out int count);
                for (int i = 0; i < count; i++)
                {
                    payload.ReadValueSafe(out ulong cId);
                    payload.ReadValueSafe(out string dName);
                    payload.ReadValueSafe(out string aId);
                    received[cId] = new ProfileInfoMessage {
                        ClientId = cId,
                        DisplayName = Sanitize(dName, "Player"),
                        AvatarId = Sanitize(aId, "avatar_1")
                    };
                }
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
    }
}
