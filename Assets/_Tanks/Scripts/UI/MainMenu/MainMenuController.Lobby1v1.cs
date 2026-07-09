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
        private readonly Image[] m_SlotFills = new Image[2];
        private readonly Image[] m_MapPickFills = new Image[3];
        private GameObject m_HostControls;   // Chọn map + nút Bắt đầu (chỉ Chủ phòng thấy)
        private GameObject m_WaitingLabelGo;  // Nhãn "Chờ chủ phòng..." (chỉ Khách thấy)
        private Button m_StartMatchButton;

        private Button m_CreateRoomButton;
        private Button m_JoinRoomButton;
        private GameObject m_RetryButtonGo;

        private bool m_LobbyEventsHooked;
        private bool m_UgsEventsHooked;
        private float m_StatusOverrideUntil;  // Giữ thông báo tạm thời không bị RefreshLobby ghi đè.

        private enum UgsConnState { Connecting, Connected, Failed }
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
            bg.GetComponent<Image>().color = m_BgColor;

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

            // Nút "Thử lại" (chỉ hiện khi kết nối UGS thất bại)
            var retryRt = CreateElement(panel.transform, "RetryBtn", typeof(RectTransform)).GetComponent<RectTransform>();
            retryRt.anchorMin = retryRt.anchorMax = new Vector2(0.5f, 0.15f);
            retryRt.anchoredPosition = Vector2.zero;
            CreatePillButton(retryRt, "THỬ LẠI", m_PlayButtonColor, 280, 74, 34, OnRetryClicked);
            m_RetryButtonGo = retryRt.gameObject;
            m_RetryButtonGo.SetActive(false);

            BuildEntryView(panel.transform);
            BuildRoomView(panel.transform);

            return panel;
        }

        // ------- View 1: chưa vào phòng -------
        private void BuildEntryView(Transform panel)
        {
            var view = CreateElement(panel, "EntryView", typeof(RectTransform)).GetComponent<RectTransform>();
            view.anchorMin = view.anchorMax = view.pivot = new Vector2(0.5f, 0.5f);
            view.sizeDelta = new Vector2(820, 560);
            view.anchoredPosition = Vector2.zero;
            m_EntryView = view.gameObject;

            var createHolder = CreateElement(view, "CreateHolder", typeof(RectTransform)).GetComponent<RectTransform>();
            createHolder.anchoredPosition = new Vector2(0, 170);
            m_CreateRoomButton = CreatePillButton(createHolder, "TẠO PHÒNG MỚI", m_PlayButtonColor, 580, 120, 46, OnCreateRoomClicked);

            var orLabel = CreateTMP(view, "OrLabel", "— HOẶC NHẬP MÃ PHÒNG —", 34,
                FontStyles.Bold | FontStyles.Italic, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
            orLabel.rectTransform.anchorMin = orLabel.rectTransform.anchorMax = orLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            orLabel.rectTransform.sizeDelta = new Vector2(700, 60);
            orLabel.rectTransform.anchoredPosition = new Vector2(0, 30);

            m_JoinCodeInput = CreateInputField(view, "NHẬP MÃ...", 480, 96);
            m_JoinCodeInput.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -80);

            var joinHolder = CreateElement(view, "JoinHolder", typeof(RectTransform)).GetComponent<RectTransform>();
            joinHolder.anchoredPosition = new Vector2(0, -210);
            m_JoinRoomButton = CreatePillButton(joinHolder, "VÀO PHÒNG", m_CyanColor, 380, 100, 40, OnJoinRoomClicked);
        }

        // ------- View 2: đã ở trong phòng -------
        private void BuildRoomView(Transform panel)
        {
            var view = CreateElement(panel, "RoomView", typeof(RectTransform)).GetComponent<RectTransform>();
            view.anchorMin = view.anchorMax = view.pivot = new Vector2(0.5f, 0.5f);
            view.sizeDelta = new Vector2(1300, 720);
            view.anchoredPosition = Vector2.zero;
            m_RoomView = view.gameObject;
            m_RoomView.SetActive(false);

            var codeTitle = CreateTMP(view, "CodeTitle", "MÃ PHÒNG", 34, FontStyles.Bold, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
            codeTitle.rectTransform.anchorMin = codeTitle.rectTransform.anchorMax = codeTitle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            codeTitle.rectTransform.sizeDelta = new Vector2(700, 50);
            codeTitle.rectTransform.anchoredPosition = new Vector2(0, 285);

            m_CodeLabel = CreateTMP(view, "CodeLabel", "------", 88, FontStyles.Bold | FontStyles.Italic, Color.white, TextAlignmentOptions.Center);
            m_CodeLabel.rectTransform.anchorMin = m_CodeLabel.rectTransform.anchorMax = m_CodeLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            m_CodeLabel.rectTransform.sizeDelta = new Vector2(800, 110);
            m_CodeLabel.rectTransform.anchoredPosition = new Vector2(0, 200);
            m_CodeLabel.characterSpacing = 12f;

            var copyHolder = CreateElement(view, "CopyHolder", typeof(RectTransform)).GetComponent<RectTransform>();
            copyHolder.anchoredPosition = new Vector2(0, 110);
            CreatePillButton(copyHolder, "SAO CHÉP MÃ", m_CardColor2, 340, 66, 30, OnCopyCodeClicked);

            // Hai chỗ ngồi
            var slot0 = CreateRoomSlot(view, 0);
            slot0.GetComponent<RectTransform>().anchoredPosition = new Vector2(-210, -70);
            var slot1 = CreateRoomSlot(view, 1);
            slot1.GetComponent<RectTransform>().anchoredPosition = new Vector2(210, -70);

            // Khu điều khiển của Chủ phòng: chọn map + bắt đầu
            var host = CreateElement(view, "HostControls", typeof(RectTransform)).GetComponent<RectTransform>();
            host.anchorMin = host.anchorMax = host.pivot = new Vector2(0.5f, 0.5f);
            host.sizeDelta = new Vector2(1200, 260);
            host.anchoredPosition = new Vector2(0, -260);
            m_HostControls = host.gameObject;

            var mapTitle = CreateTMP(host, "MapTitle", "CHỌN BẢN ĐỒ", 28, FontStyles.Bold, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
            mapTitle.rectTransform.anchorMin = mapTitle.rectTransform.anchorMax = mapTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            mapTitle.rectTransform.sizeDelta = new Vector2(700, 40);
            mapTitle.rectTransform.anchoredPosition = new Vector2(0, -10);

            float[] xs = { -240, 0, 240 };
            for (int i = 0; i < k_Lobby1v1Maps.Length; i++)
            {
                string map = k_Lobby1v1Maps[i];
                var mapHolder = CreateElement(host, "MapPick_" + map, typeof(RectTransform)).GetComponent<RectTransform>();
                mapHolder.anchorMin = mapHolder.anchorMax = mapHolder.pivot = new Vector2(0.5f, 1f);
                mapHolder.anchoredPosition = new Vector2(xs[i], -55);
                var btn = CreatePillButton(mapHolder, map.ToUpper(), m_CyanColor, 210, 80, 28, () => SelectMap(map));
                m_MapPickFills[i] = btn.GetComponent<Image>();
            }

            var startHolder = CreateElement(host, "StartHolder", typeof(RectTransform)).GetComponent<RectTransform>();
            startHolder.anchorMin = startHolder.anchorMax = startHolder.pivot = new Vector2(0.5f, 0f);
            startHolder.anchoredPosition = new Vector2(0, 30);
            m_StartMatchButton = CreatePillButton(startHolder, "BẮT ĐẦU TRẬN ĐẤU", m_PlayButtonColor, 560, 110, 42, OnStartMatchClicked);

            // Nhãn chờ (Khách)
            var waitRt = CreateElement(view, "WaitingLabel", typeof(RectTransform)).GetComponent<RectTransform>();
            waitRt.anchorMin = waitRt.anchorMax = waitRt.pivot = new Vector2(0.5f, 0.5f);
            waitRt.sizeDelta = new Vector2(1000, 80);
            waitRt.anchoredPosition = new Vector2(0, -280);
            CreateTMP(waitRt, "Label", "Đang chờ chủ phòng bắt đầu trận đấu...", 38,
                FontStyles.Bold | FontStyles.Italic, Color.white, TextAlignmentOptions.Center);
            m_WaitingLabelGo = waitRt.gameObject;

            SelectMap(m_SelectedMap);
        }

        private GameObject CreateRoomSlot(Transform parent, int index)
        {
            var slotGo = CreateElement(parent, $"Slot_{index}", typeof(RectTransform));
            var rt = slotGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(360, 240);

            var fill = CreateImage(slotGo.transform, "Fill",
                CreateRoundedRectSprite(100, 30, m_CyanColor, m_OutlineColor, 5), Image.Type.Sliced, true);
            m_SlotFills[index] = fill;

            var name = CreateTMP(fill.transform, "Name", "", 40, FontStyles.Bold | FontStyles.Italic, Color.white, TextAlignmentOptions.Center);
            m_SlotNames[index] = name;

            return slotGo;
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
                m_MapPickFills[i].color = selected ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
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
            ISession session = lobby.CurrentSession;
            int count = session != null && session.Players != null ? session.Players.Count : 0;
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

            if (m_CodeLabel != null) m_CodeLabel.text = session != null ? session.Code : "------";

            for (int i = 0; i < m_SlotNames.Length; i++)
            {
                bool filled = i < count;
                if (m_SlotFills[i] != null)
                    m_SlotFills[i].color = filled ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
                if (m_SlotNames[i] != null)
                    m_SlotNames[i].text = filled ? (i == 0 ? "CHỦ PHÒNG" : "ĐỐI THỦ") : "Đang chờ...";
            }

            if (m_HostControls != null) m_HostControls.SetActive(isHost);
            if (m_WaitingLabelGo != null) m_WaitingLabelGo.SetActive(!isHost);
            if (m_StartMatchButton != null) m_StartMatchButton.interactable = isHost && count >= 2;

            SetDefaultStatus(count >= 2 ? "Đã đủ người! Sẵn sàng chiến đấu." : "Đang chờ đối thủ vào phòng...");
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
            }

            var ugs = UGSManager.Instance;
            if (ugs != null && m_UgsEventsHooked)
            {
                ugs.OnPlayerSignedIn -= HandleSignedIn;
                ugs.OnSignInFailed -= HandleUgsFailed;
                ugs.OnInitializationFailed -= HandleUgsFailed;
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
                m_LobbyEventsHooked = true;
            }

            var ugs = UGSManager.Instance;
            if (ugs != null && !m_UgsEventsHooked)
            {
                ugs.OnPlayerSignedIn += HandleSignedIn;
                ugs.OnSignInFailed += HandleUgsFailed;
                ugs.OnInitializationFailed += HandleUgsFailed;
                m_UgsEventsHooked = true;

                // Đồng bộ trạng thái hiện tại nếu đã đăng nhập trước khi mở menu.
                if (ugs.IsSignedIn) m_ConnState = UgsConnState.Connected;
                else if (ugs.State == UGSManager.ConnectionState.Failed) m_ConnState = UgsConnState.Failed;
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
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
            SetLobbyStatus(isHost ? $"Đã tạo phòng! Mã: {session.Code}" : "Đã vào phòng thành công!");
        }

        private void HandleSessionLeft() => SetLobbyStatus("Đã rời phòng.");
        private void HandleSessionError(string error) => SetLobbyStatus($"Lỗi: {error}");
    }
}
