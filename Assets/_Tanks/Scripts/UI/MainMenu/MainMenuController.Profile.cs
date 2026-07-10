using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Tanks.Backend;

namespace Tanks.Complete
{
    /// <summary>
    /// Partial class xử lý Form Profile overlay.
    /// Khi ấn vào ProfileBox trên TopBar → popup hiện lên cho phép xem/sửa thông tin cá nhân.
    /// Hiện tại chỉ là giao diện, chưa kết nối backend Cloud Save.
    /// </summary>
    public partial class MainMenuController
    {
        // --- Profile overlay references ---
        private GameObject m_ProfileOverlay;
        private TMP_InputField m_ProfileDisplayNameField;
        private TextMeshProUGUI m_ProfileRankValue;
        private TextMeshProUGUI m_ProfilePlayerIdValue;

        // --- Home TopBar text references (cập nhật khi lưu profile) ---
        private TextMeshProUGUI m_HomeNameText;
        private TextMeshProUGUI m_HomeRankText;

        // --- Avatar Selection ---
        private string m_SelectedAvatarId = "avatar_1";
        private Image[] m_AvatarHighlights = new Image[4];
        private readonly string[] k_AvatarOptions = { "avatar_1", "avatar_2", "avatar_3", "avatar_4" };

        /// <summary>
        /// Tạo lớp overlay chứa form profile. Mặc định ẩn (SetActive false).
        /// </summary>
        private GameObject CreateProfileOverlay(Transform canvasRoot)
        {
            // ═══ Dim overlay (nền tối chặn click phía sau) ═══
            var overlay = CreateElement(canvasRoot, "ProfileOverlay", typeof(RectTransform), typeof(Image));
            StretchFull(overlay.GetComponent<RectTransform>());
            overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            // ═══ Card nền form (cam vibe sa mạc, viền đen, bo tròn) ═══
            var card = CreateElement(overlay.transform, "ProfileCard", typeof(RectTransform), typeof(Image));
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(540, 780);

            // Đổ bóng cho card
            CreateShadow(cardRt, 128, 32, new Vector2(8f, -10f), new Vector2(8f, -4f), 0.4f);

            var cardBg = card.GetComponent<Image>();
            cardBg.sprite = CreateRoundedRectSprite(128, 32, new Color(0.9f, 0.55f, 0.18f), m_OutlineColor, 5);
            cardBg.type = Image.Type.Sliced;

            // ═══ Tiêu đề "HỒ SƠ CÁ NHÂN" (Cyan callout) ═══
            var titleRt = CreateElement(card.transform, "TitleContainer", typeof(RectTransform)).GetComponent<RectTransform>();
            titleRt.anchorMin = titleRt.anchorMax = titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0, -25);
            CreateCallout(titleRt, "HỒ SƠ CÁ NHÂN", m_CyanColor, 340, 60);

            // ═══ Ô nhập Tên hiển thị ═══
            m_ProfileDisplayNameField = CreateProfileInput(card.transform, "TÊN HIỂN THỊ", new Vector2(0, -110));

            // ═══ Chọn Ảnh Nhân Vật ═══
            CreateAvatarSelector(card.transform, new Vector2(0, -205));

            // ═══ Hàng RANK (chỉ xem) ═══
            m_ProfileRankValue = CreateProfileInfoRow(card.transform, "RANK", "---", new Vector2(0, -365));

            // ═══ Hàng PLAYER ID (chỉ xem) ═══
            m_ProfilePlayerIdValue = CreateProfileInfoRow(card.transform, "PLAYER ID", "---", new Vector2(0, -435));

            // ═══ Nút LƯU THAY ĐỔI (vàng, nổi bật) ═══
            var saveBtnRt = CreateElement(card.transform, "SaveBtnContainer", typeof(RectTransform)).GetComponent<RectTransform>();
            saveBtnRt.anchorMin = saveBtnRt.anchorMax = saveBtnRt.pivot = new Vector2(0.5f, 1f);
            saveBtnRt.anchoredPosition = new Vector2(0, -515);
            CreatePillButton(saveBtnRt, "LƯU THAY ĐỔI", m_PlayButtonColor, 420, 65, 30, OnSaveProfileClicked);

            // ═══ Nút ĐÓNG (xám trung tính) ═══
            var closeBtnRt = CreateElement(card.transform, "CloseBtnContainer", typeof(RectTransform)).GetComponent<RectTransform>();
            closeBtnRt.anchorMin = closeBtnRt.anchorMax = closeBtnRt.pivot = new Vector2(0.5f, 1f);
            closeBtnRt.anchoredPosition = new Vector2(0, -590);
            CreatePillButton(closeBtnRt, "ĐÓNG", new Color(0.5f, 0.5f, 0.5f), 420, 50, 26, () => HideProfilePanel());

            // ═══ Đường kẻ phân cách ═══
            var separator = CreateImage(card.transform, "Separator",
                CreateRoundedRectSprite(4, 1, new Color(1f, 1f, 1f, 0.3f), Color.clear, 0), Image.Type.Sliced, false);
            var sepRt = separator.rectTransform;
            sepRt.anchorMin = sepRt.anchorMax = sepRt.pivot = new Vector2(0.5f, 1f);
            sepRt.anchoredPosition = new Vector2(0, -660);
            sepRt.sizeDelta = new Vector2(380, 2);

            // ═══ Nút ĐĂNG XUẤT (đỏ cảnh báo) ═══
            var logoutBtnRt = CreateElement(card.transform, "LogoutBtnContainer", typeof(RectTransform)).GetComponent<RectTransform>();
            logoutBtnRt.anchorMin = logoutBtnRt.anchorMax = logoutBtnRt.pivot = new Vector2(0.5f, 1f);
            logoutBtnRt.anchoredPosition = new Vector2(0, -680);
            CreatePillButton(logoutBtnRt, "ĐĂNG XUẤT", new Color(0.75f, 0.28f, 0.28f), 420, 50, 26, OnLogoutClicked);

            overlay.SetActive(false);
            return overlay;
        }

        /// <summary>
        /// Tạo ô nhập text kiểu pill (bo tròn, viền đen, đổ bóng) — giống StartScreen.
        /// </summary>
        private TMP_InputField CreateProfileInput(Transform parent, string placeholder, Vector2 position)
        {
            var wrapper = CreateElement(parent, "ProfileInput", typeof(RectTransform)).GetComponent<RectTransform>();
            wrapper.anchorMin = wrapper.anchorMax = wrapper.pivot = new Vector2(0.5f, 1f);
            wrapper.anchoredPosition = position;
            wrapper.sizeDelta = new Vector2(440, 75);

            // Đổ bóng
            CreateShadow(wrapper, 96, 36, new Vector2(5f, -6f), new Vector2(5f, -2f));

            // Nền trắng bo tròn + viền đen
            var fillGo = CreateElement(wrapper, "Fill", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            StretchFull(fillGo.GetComponent<RectTransform>());
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.sprite = CreateRoundedRectSprite(96, 36, Color.white, m_OutlineColor, 4);
            fillImg.type = Image.Type.Sliced;

            // Icon @ bên trái
            var iconTmp = CreateElement(fillGo.transform, "Icon", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            var iconRt = iconTmp.rectTransform;
            iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(22f, 0f);
            iconRt.sizeDelta = new Vector2(40f, 40f);
            iconTmp.text = "@";
            iconTmp.fontSize = 28f;
            iconTmp.color = new Color(0.6f, 0.6f, 0.6f);
            iconTmp.alignment = TextAlignmentOptions.MidlineLeft;
            iconTmp.textWrappingMode = TextWrappingModes.NoWrap;

            // Vùng TextArea (lệch phải chừa icon)
            var textArea = CreateElement(fillGo.transform, "TextArea", typeof(RectTransform), typeof(RectMask2D));
            var textAreaRt = textArea.GetComponent<RectTransform>();
            StretchFull(textAreaRt);
            textAreaRt.offsetMin = new Vector2(65f, 10f);
            textAreaRt.offsetMax = new Vector2(-20f, -10f);

            var placeholderTmp = CreateTMP(textArea.transform, "Placeholder", placeholder, 26, FontStyles.Bold, new Color(0.55f, 0.55f, 0.55f), TextAlignmentOptions.MidlineLeft);
            var textTmp = CreateTMP(textArea.transform, "Text", "", 26, FontStyles.Bold, m_TextDark, TextAlignmentOptions.MidlineLeft);

            var input = fillGo.GetComponent<TMP_InputField>();
            input.textViewport = textAreaRt;
            input.textComponent = textTmp;
            input.placeholder = placeholderTmp;
            input.characterLimit = 20;
            input.contentType = TMP_InputField.ContentType.Standard;

            return input;
        }

        private void CreateAvatarSelector(Transform parent, Vector2 position)
        {
            var wrapper = CreateElement(parent, "AvatarSelector", typeof(RectTransform)).GetComponent<RectTransform>();
            wrapper.anchorMin = wrapper.anchorMax = wrapper.pivot = new Vector2(0.5f, 1f);
            wrapper.anchoredPosition = position;
            wrapper.sizeDelta = new Vector2(480, 140);

            var titleTmp = CreateTMP(wrapper, "Title", "ẢNH ĐẠI DIỆN", 24, FontStyles.Bold, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center);
            titleTmp.rectTransform.anchorMin = titleTmp.rectTransform.anchorMax = titleTmp.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleTmp.rectTransform.anchoredPosition = new Vector2(0, 0);
            titleTmp.rectTransform.sizeDelta = new Vector2(400, 30);

            float startX = -180f;
            float gapX = 120f;
            
            for (int i = 0; i < k_AvatarOptions.Length; i++)
            {
                int index = i; // capture cho lambda
                string avatarId = k_AvatarOptions[i];
                
                var slotRt = CreateElement(wrapper, $"AvatarSlot_{i}", typeof(RectTransform)).GetComponent<RectTransform>();
                slotRt.anchorMin = slotRt.anchorMax = slotRt.pivot = new Vector2(0.5f, 1f);
                slotRt.anchoredPosition = new Vector2(startX + i * gapX, -40);
                slotRt.sizeDelta = new Vector2(100, 130);
                
                // Border/Highlight
                var highlight = CreateImage(slotRt, "Highlight", CreateRoundedRectSprite(16, 4, Color.white, m_OutlineColor, 4), Image.Type.Sliced, false);
                StretchFull(highlight.rectTransform);
                m_AvatarHighlights[i] = highlight;

                // Avatar Image
                var avatarGo = CreateElement(slotRt, "Image", typeof(RectTransform), typeof(Image));
                var avatarRt = avatarGo.GetComponent<RectTransform>();
                StretchFull(avatarRt);
                avatarRt.offsetMin = new Vector2(4, 4);
                avatarRt.offsetMax = new Vector2(-4, -4);
                
                var img = avatarGo.GetComponent<Image>();
                Sprite s = LoadSpriteWithFallback(avatarId);
                if (s != null) img.sprite = s;
                else img.color = new Color(0.2f, 0.2f, 0.2f, 1f);
                
                // Nút bấm: Để Button hoạt động, GameObject của nó cần có 1 thành phần Graphic.
                // Ta thêm một Image trong suốt vào slotRt làm mục tiêu bắt click (Raycast Target).
                var clickTarget = slotRt.gameObject.AddComponent<Image>();
                clickTarget.color = Color.clear; // Trong suốt hoàn toàn
                
                var btn = slotRt.gameObject.AddComponent<Button>();
                string capturedAvatarId = avatarId;
                btn.onClick.AddListener(() => OnAvatarSelected(capturedAvatarId));
                
                var vibe = slotRt.gameObject.AddComponent<UIButtonVibe>();
                vibe.clickSound = m_ClickSound;
            }
        }

        private void OnAvatarSelected(string avatarId)
        {
            m_SelectedAvatarId = avatarId;
            RefreshAvatarHighlight();
        }

        private void RefreshAvatarHighlight()
        {
            for (int i = 0; i < k_AvatarOptions.Length; i++)
            {
                if (m_AvatarHighlights[i] != null)
                {
                    bool isSelected = k_AvatarOptions[i] == m_SelectedAvatarId;
                    
                    // Đổi màu viền nổi bật (Cyan sáng) hoặc làm tối viền không chọn
                    m_AvatarHighlights[i].color = isSelected ? m_CyanColor : new Color(0f, 0f, 0f, 0.6f);
                    
                    // Hiệu ứng phóng to/thu nhỏ và đưa lên trên cùng (chống bị đè)
                    var slotRt = m_AvatarHighlights[i].transform.parent.GetComponent<RectTransform>();
                    if (slotRt != null)
                    {
                        if (isSelected)
                        {
                            slotRt.localScale = new Vector3(1.15f, 1.15f, 1f);
                            slotRt.SetAsLastSibling(); // Đưa UI element này vẽ sau cùng -> nằm trên cùng
                        }
                        else
                        {
                            slotRt.localScale = new Vector3(0.85f, 0.85f, 1f);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Tạo hàng thông tin chỉ xem: Label bên trái, Value bên phải, nền bán trong suốt.
        /// </summary>
        private TextMeshProUGUI CreateProfileInfoRow(Transform parent, string label, string value, Vector2 position)
        {
            var wrapper = CreateElement(parent, $"InfoRow_{label}", typeof(RectTransform)).GetComponent<RectTransform>();
            wrapper.anchorMin = wrapper.anchorMax = wrapper.pivot = new Vector2(0.5f, 1f);
            wrapper.anchoredPosition = position;
            wrapper.sizeDelta = new Vector2(440, 50);

            // Nền bán trong suốt bo tròn
            var bg = CreateImage(wrapper, "Bg",
                CreateRoundedRectSprite(80, 24, new Color(0f, 0f, 0f, 0.18f), Color.clear, 0), Image.Type.Sliced, true);

            // Label bên trái
            var labelTmp = CreateElement(bg.transform, "Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            var labelRt = labelTmp.rectTransform;
            labelRt.anchorMin = new Vector2(0, 0);
            labelRt.anchorMax = new Vector2(0.45f, 1);
            labelRt.offsetMin = new Vector2(20, 0);
            labelRt.offsetMax = Vector2.zero;
            labelTmp.text = label;
            labelTmp.fontSize = 22;
            labelTmp.fontStyle = FontStyles.Bold | FontStyles.Italic;
            labelTmp.color = new Color(1f, 1f, 1f, 0.75f);
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            labelTmp.textWrappingMode = TextWrappingModes.NoWrap;

            // Value bên phải
            var valueTmp = CreateElement(bg.transform, "Value", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            var valueRt = valueTmp.rectTransform;
            valueRt.anchorMin = new Vector2(0.45f, 0);
            valueRt.anchorMax = new Vector2(1, 1);
            valueRt.offsetMin = Vector2.zero;
            valueRt.offsetMax = new Vector2(-20, 0);
            valueTmp.text = value;
            valueTmp.fontSize = 24;
            valueTmp.fontStyle = FontStyles.Bold | FontStyles.Italic;
            valueTmp.color = Color.white;
            valueTmp.alignment = TextAlignmentOptions.MidlineRight;
            valueTmp.textWrappingMode = TextWrappingModes.NoWrap;

            return valueTmp;
        }

        /// <summary>
        /// Nối ProfileBox trên TopBar vào sự kiện click mở form profile.
        /// Dùng cho cả nhánh bake (Awake) lẫn nhánh BuildUI.
        /// </summary>
        private void WireProfileBox(Transform canvasRoot)
        {
            var profileBox = canvasRoot.Find("HomePanel/TopBar/ProfileBox");
            if (profileBox == null) return;

            // Đặt kích thước ProfileBox để bao trùm Avatar + Tên + Rank
            var boxRt = profileBox.GetComponent<RectTransform>();
            boxRt.sizeDelta = new Vector2(530, 80);

            // Image trong suốt để nhận raycast click
            var boxImg = profileBox.GetComponent<Image>();
            if (boxImg == null) boxImg = profileBox.gameObject.AddComponent<Image>();
            boxImg.color = Color.clear;

            // Button
            var btn = profileBox.GetComponent<Button>();
            if (btn == null) btn = profileBox.gameObject.AddComponent<Button>();
            btn.targetGraphic = boxImg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => ShowProfilePanel());

            // UIButtonVibe (âm thanh click)
            var vibe = profileBox.GetComponent<UIButtonVibe>();
            if (vibe == null) vibe = profileBox.gameObject.AddComponent<UIButtonVibe>();
            vibe.clickSound = m_ClickSound;

            // Lưu tham chiếu text trên TopBar để cập nhật khi lưu profile
            m_HomeNameText = profileBox.Find("NameBox/Fill/Label")?.GetComponent<TextMeshProUGUI>();
            m_HomeRankText = profileBox.Find("RankBox/Fill/Label")?.GetComponent<TextMeshProUGUI>();
        }

        /// <summary>
        /// Cập nhật thông tin Profile lên TopBar.
        /// </summary>
        private void UpdateTopBarProfileInfo(string displayName)
        {
            if (m_HomeNameText != null)
                m_HomeNameText.text = displayName;

            if (m_HomeRankText != null && ProfileManager.Instance != null)
                m_HomeRankText.text = ProfileManager.Instance.Rank;
        }

        /// <summary>
        /// Hiện form profile overlay, điền thông tin hiện tại.
        /// </summary>
        public void ShowProfilePanel()
        {
            if (m_ProfileOverlay == null) return;

            // Điền tên hiển thị hiện tại vào ô input từ ProfileManager
            if (m_ProfileDisplayNameField != null && ProfileManager.Instance != null)
                m_ProfileDisplayNameField.text = ProfileManager.Instance.DisplayName;

            // Đọc AvatarId
            if (ProfileManager.Instance != null)
            {
                m_SelectedAvatarId = ProfileManager.Instance.AvatarId;
                if (string.IsNullOrEmpty(m_SelectedAvatarId)) m_SelectedAvatarId = "avatar_1";
            }
            RefreshAvatarHighlight();

            // Điền rank
            if (m_ProfileRankValue != null && ProfileManager.Instance != null)
                m_ProfileRankValue.text = ProfileManager.Instance.Rank;

            // Điền Player ID từ UGS Authentication
            if (m_ProfilePlayerIdValue != null)
            {
                var ugs = UGSManager.Instance;
                string fullId = (ugs != null && ugs.IsSignedIn) ? ugs.PlayerId : "---";
                // Rút gọn ID dài: hiện 4 ký tự đầu + "..." + 4 ký tự cuối
                if (fullId.Length > 12)
                    fullId = fullId.Substring(0, 4) + "..." + fullId.Substring(fullId.Length - 4);
                m_ProfilePlayerIdValue.text = fullId;
            }

            m_ProfileOverlay.SetActive(true);
        }

        /// <summary>
        /// Ẩn form profile overlay.
        /// </summary>
        public void HideProfilePanel()
        {
            if (m_ProfileOverlay != null)
                m_ProfileOverlay.SetActive(false);
        }

        /// <summary>
        /// Xử lý khi bấm nút "LƯU THAY ĐỔI" — lưu tên hiển thị lên Cloud Save.
        /// </summary>
        private async void OnSaveProfileClicked()
        {
            // Loại bỏ ký tự ẩn của TextMeshPro (Zero-Width Space)
            string newName = m_ProfileDisplayNameField.text.Replace("\u200B", "").Trim();
            if (string.IsNullOrEmpty(newName))
            {
                Debug.LogWarning("[Profile] Tên hiển thị không được để trống.");
                return;
            }

            // Khoá nút, hiện loading
            var saveBtn = m_ProfileOverlay.transform.Find("ProfileCard/SaveBtnContainer/PillButton_LƯU THAY ĐỔI/Fill")?.GetComponent<Button>();
            var saveText = m_ProfileOverlay.transform.Find("ProfileCard/SaveBtnContainer/PillButton_LƯU THAY ĐỔI/Fill/Label")?.GetComponent<TextMeshProUGUI>();

            if (saveBtn != null) saveBtn.interactable = false;
            if (saveText != null) saveText.text = "ĐANG LƯU...";

            try
            {
                await ProfileManager.Instance.SaveProfileAsync(newName, m_SelectedAvatarId);
            }
            finally
            {
                // Trả lại trạng thái
                if (saveBtn != null) saveBtn.interactable = true;
                if (saveText != null) saveText.text = "LƯU THAY ĐỔI";
                HideProfilePanel();
            }
        }

        /// <summary>
        /// Xử lý khi bấm nút "ĐĂNG XUẤT" — về màn hình Start.
        /// </summary>
        private void OnLogoutClicked()
        {
            HideProfilePanel();
            var ugs = UGSManager.Instance;
            if (ugs != null)
            {
                ugs.SignOutAndReturnToLogin();
            }
        }

    }
}
