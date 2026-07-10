using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace Tanks.Complete
{
    public partial class MainMenuController
    {
        private GameObject CreateModeSelectPanel(Transform parent)
        {
            var panel = CreatePanel("ModeSelectPanel", parent);
            
            // Thêm hình nền để biến thành trang riêng biệt (che lấp hoàn toàn trang Home)
            var bg = CreateElement(panel.transform, "Background", typeof(RectTransform), typeof(Image));
            StretchFull(bg.GetComponent<RectTransform>());
            var bgImg = bg.GetComponent<Image>();
            
            // Tải ảnh nền từ Resources nếu có
            var bgSprite = LoadSpriteWithFallback("background_desert");
            if (bgSprite != null)
            {
                bgImg.sprite = bgSprite;
                bgImg.color = Color.white;
            }
            else
            {
                bgImg.color = new Color(0.85f, 0.51f, 0.16f); // Nền cam fallback
            }

            // Tiêu đề
            var titleRt = CreateElement(panel.transform, "TitleContainer", typeof(RectTransform)).GetComponent<RectTransform>();
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.88f);
            titleRt.anchoredPosition = Vector2.zero;
            CreateShadowedTitle(titleRt.transform, "CHỌN CHẾ ĐỘ", 90f);

            // Back button
            var backRt = CreateElement(panel.transform, "BackBtn", typeof(RectTransform)).GetComponent<RectTransform>();
            backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
            backRt.anchoredPosition = new Vector2(180, -80);
            CreatePillButton(backRt, "< TRỞ VỀ", m_CyanColor, 250, 70, 36, () => UpdateState(MenuState.Home));

            // Các thẻ chế độ (tăng chiều cao lên 540 để vừa layout mới)
            var modesGroup = CreateElement(panel.transform, "ModesGroup", typeof(RectTransform)).GetComponent<RectTransform>();
            modesGroup.anchorMin = modesGroup.anchorMax = modesGroup.pivot = new Vector2(0.5f, 0.45f);
            modesGroup.sizeDelta = new Vector2(1400, 540);
            modesGroup.anchoredPosition = Vector2.zero;

            var cardOffline = CreateModeCard(modesGroup, "OFFLINE BOTS", "Luyện Tập", m_CyanColor, "Modes/mode_offline", () => Debug.Log("Vào chế độ Offline..."));
            cardOffline.GetComponent<RectTransform>().anchoredPosition = new Vector2(-450, 0);

            var card1 = CreateModeCard(modesGroup, "SOLO ARENA", "1v1 Quyết Đấu", m_CardColor1, "Modes/mode_solo", () => UpdateState(MenuState.Lobby1v1));
            card1.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);

            var card2 = CreateModeCard(modesGroup, "TEAM COMBAT", "5v5 Đồng Đội", m_CardColor2, "Modes/mode_team", () => UpdateState(MenuState.Lobby5v5));
            card2.GetComponent<RectTransform>().anchoredPosition = new Vector2(450, 0);

            return panel;
        }

        private GameObject CreateModeCard(Transform parent, string title, string desc, Color accentColor, string imagePath, UnityEngine.Events.UnityAction onClick)
        {
            var cardGo = CreateElement(parent, $"Card_{title}", typeof(RectTransform));
            var rt = cardGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400, 520);

            // 1. Viền và nền cơ bản của Card (để giữ được viền đen dầy phong cách Arcade)
            var borderImg = CreateImage(cardGo.transform, "Border", CreateRoundedRectSprite(120, 30, accentColor, m_OutlineColor, 6), Image.Type.Sliced, true);

            // 2. Khung Mask tràn viền (Full-bleed) ôm sát viền trong của card
            var imageContainer = CreateElement(borderImg.transform, "ImageMask", typeof(RectTransform), typeof(Image), typeof(Mask));
            var maskRt = imageContainer.GetComponent<RectTransform>();
            StretchFull(maskRt);
            // Co lề nhẹ vào trong 6px để lộ phần viền đen dày 6px của borderImg
            maskRt.offsetMin = new Vector2(6, 6);
            maskRt.offsetMax = new Vector2(-6, -6);

            // Mask shape bo góc theo bo góc của thẻ
            var maskImg = imageContainer.GetComponent<Image>();
            maskImg.sprite = CreateRoundedRectSprite(108, 24, Color.white, Color.clear, 0);
            maskImg.type = Image.Type.Sliced;
            imageContainer.GetComponent<Mask>().showMaskGraphic = false;

            // 3. Ảnh minh họa phủ kín toàn bộ thẻ
            var decorGo = CreateElement(imageContainer.transform, "DecorImage", typeof(RectTransform), typeof(Image));
            var decorRt = decorGo.GetComponent<RectTransform>();
            StretchFull(decorRt);
            var decorImg = decorGo.GetComponent<Image>();
            
            // Tải ảnh từ Resources với chế độ dự phòng thông minh
            var loadedSprite = LoadSpriteWithFallback(imagePath);
            if (loadedSprite != null)
            {
                decorImg.sprite = loadedSprite;
                decorImg.type = Image.Type.Simple;
                // Để fill toàn bộ thẻ, tắt preserveAspect để ảnh bao trùm trọn vẹn
                decorImg.preserveAspect = false; 
            }
            else
            {
                // Nếu chưa có ảnh, dùng màu tối của accentColor làm nền
                decorImg.sprite = CreateRoundedRectSprite(10, 0, new Color(accentColor.r * 0.4f, accentColor.g * 0.4f, accentColor.b * 0.4f, 1f), Color.clear, 0);
                decorImg.type = Image.Type.Sliced;

                var placeholderTxt = CreateTMP(decorGo.transform, "PlaceholderText", "[ CHƯA CÓ ẢNH ]", 24, FontStyles.Bold, new Color(1f, 1f, 1f, 0.3f));
            }

            // 4. Bỏ lớp phủ Gradient tối để hiện rõ ảnh (đã xóa ScrimOverlay)

            // 5. Tên chế độ (Title) - màu chữ tối m_TextDark giúp nổi bật trên nền cát sáng
            var titleTmp = CreateTMP(imageContainer.transform, "Title", title, 38, FontStyles.Bold | FontStyles.Italic, m_TextDark, TextAlignmentOptions.Center);
            titleTmp.rectTransform.anchorMin = titleTmp.rectTransform.anchorMax = titleTmp.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleTmp.rectTransform.anchoredPosition = new Vector2(0, -290);
            titleTmp.rectTransform.sizeDelta = new Vector2(360, 50);

            // 6. Mô tả chế độ (Description) - màu chữ tối m_TextDark bán trong suốt
            var descTmp = CreateTMP(imageContainer.transform, "Desc", desc, 26, FontStyles.Bold | FontStyles.Italic, new Color(m_TextDark.r, m_TextDark.g, m_TextDark.b, 0.85f), TextAlignmentOptions.Center);
            descTmp.rectTransform.anchorMin = descTmp.rectTransform.anchorMax = descTmp.rectTransform.pivot = new Vector2(0.5f, 1f);
            descTmp.rectTransform.anchoredPosition = new Vector2(0, -345);
            descTmp.rectTransform.sizeDelta = new Vector2(360, 40);

            // 7. Nút bấm "CHƠI NGAY" (Không thay đổi logic click)
            var playBtnContainer = CreateElement(imageContainer.transform, "PlayBtnContainer", typeof(RectTransform)).GetComponent<RectTransform>();
            playBtnContainer.anchorMin = playBtnContainer.anchorMax = playBtnContainer.pivot = new Vector2(0.5f, 0f);
            playBtnContainer.anchoredPosition = new Vector2(0, 30);
            
            CreatePillButton(playBtnContainer, "CHƠI NGAY", m_PlayButtonColor, 320, 65, 28, onClick);

            return cardGo;
        }

        public void RefreshBakedModeImages(Transform canvasRoot)
        {
            UpdateBakedCardImage(canvasRoot, "Card_OFFLINE BOTS", "Modes/mode_offline");
            UpdateBakedCardImage(canvasRoot, "Card_SOLO ARENA", "Modes/mode_solo");
            UpdateBakedCardImage(canvasRoot, "Card_TEAM COMBAT", "Modes/mode_team");

            // Nạp ảnh nền của ModeSelectPanel nếu có
            var bgTrans = canvasRoot.Find("ModeSelectPanel/Background");
            if (bgTrans != null)
            {
                var bgImg = bgTrans.GetComponent<Image>();
                var bgSprite = LoadSpriteWithFallback("background_desert");
                if (bgImg != null && bgSprite != null)
                {
                    bgImg.sprite = bgSprite;
                    bgImg.color = Color.white;
                }
            }
        }

        private void UpdateBakedCardImage(Transform canvasRoot, string cardName, string imagePath)
        {
            var modesGroup = canvasRoot.Find("ModeSelectPanel/ModesGroup");
            if (modesGroup == null) return;

            Transform targetCard = null;
            for (int i = 0; i < modesGroup.childCount; i++)
            {
                var child = modesGroup.GetChild(i);
                if (child.name == cardName || child.name.StartsWith(cardName + "\n") || child.name.StartsWith(cardName + " "))
                {
                    targetCard = child;
                    break;
                }
            }

            if (targetCard != null)
            {
                var decorImgTrans = targetCard.Find("Border/ImageMask/DecorImage") ?? targetCard.Find("Fill/ImageMask/DecorImage");
                if (decorImgTrans != null)
                {
                    var img = decorImgTrans.GetComponent<Image>();
                    var sprite = LoadSpriteWithFallback(imagePath);
                    if (img != null && sprite != null)
                    {
                        img.sprite = sprite;
                        img.preserveAspect = false;
                        
                        var placeholder = decorImgTrans.Find("PlaceholderText");
                        if (placeholder != null) placeholder.gameObject.SetActive(false);
                    }
                }

                // Cập nhật màu chữ ở runtime để sửa lỗi độ tương phản cho bản Baked
                var titleTrans = targetCard.Find("Border/ImageMask/Title") ?? targetCard.Find("Fill/ImageMask/Title");
                if (titleTrans != null)
                {
                    var titleTxt = titleTrans.GetComponent<TextMeshProUGUI>();
                    if (titleTxt != null) titleTxt.color = m_TextDark;
                }

                var descTrans = targetCard.Find("Border/ImageMask/Desc") ?? targetCard.Find("Fill/ImageMask/Desc");
                if (descTrans != null)
                {
                    var descTxt = descTrans.GetComponent<TextMeshProUGUI>();
                    if (descTxt != null) descTxt.color = new Color(m_TextDark.r, m_TextDark.g, m_TextDark.b, 0.85f);
                }
            }
        }

        private Sprite LoadSpriteWithFallback(string imagePath)
        {
            // 1. Thử load trực tiếp dưới dạng Sprite (cho Sprite Single)
            var sprite = Resources.Load<Sprite>(imagePath);
            if (sprite != null) return sprite;

            // 2. Thử load dạng Texture2D (cho Default Texture hoặc Sprite Multiple chưa slice)
            var tex = Resources.Load<Texture2D>(imagePath);
            if (tex != null)
            {
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }

            // 3. Thử load dạng Multiple Sprite
            var sprites = Resources.LoadAll<Sprite>(imagePath);
            if (sprites != null && sprites.Length > 0)
            {
                return sprites[0];
            }

            return null;
        }
    }
}
