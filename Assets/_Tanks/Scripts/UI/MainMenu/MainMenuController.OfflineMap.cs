using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Tanks.Complete
{
    public partial class MainMenuController
    {
        private GameObject CreateOfflineMapSelectPanel(Transform canvasRoot)
        {
            var panel = CreatePanel("OfflineMapSelectPanel", canvasRoot);

            var bg = CreateElement(panel.transform, "Background", typeof(RectTransform), typeof(Image));
            StretchFull(bg.GetComponent<RectTransform>());
            var bgImg = bg.GetComponent<Image>();
            var bgSprite = LoadSpriteWithFallback("background_desert");
            if (bgSprite != null)
            {
                bgImg.sprite = bgSprite;
                bgImg.color = Color.white;
            }
            else
            {
                bgImg.color = m_BgColor;
            }

            var dimmer = CreateElement(panel.transform, "Dimmer", typeof(RectTransform), typeof(Image));
            StretchFull(dimmer.GetComponent<RectTransform>());
            dimmer.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.48f);

            var headerRt = CreateElement(panel.transform, "Header", typeof(RectTransform)).GetComponent<RectTransform>();
            headerRt.anchorMin = headerRt.anchorMax = headerRt.pivot = new Vector2(0.5f, 0.91f);
            headerRt.sizeDelta = new Vector2(1050, 76);
            headerRt.anchoredPosition = Vector2.zero;
            CreateShadowedTitle(headerRt, "CHỌN CHIẾN TRƯỜNG", 62f);

            var subtitle = CreateTMP(panel.transform, "Subtitle", "TRẬN ĐẤU OFFLINE VỚI MÁY", 23,
                FontStyles.Bold, new Color(1f, 1f, 1f, 0.72f), TextAlignmentOptions.Center);
            subtitle.rectTransform.anchorMin = subtitle.rectTransform.anchorMax = subtitle.rectTransform.pivot = new Vector2(0.5f, 0.825f);
            subtitle.rectTransform.sizeDelta = new Vector2(800, 36);
            subtitle.rectTransform.anchoredPosition = Vector2.zero;
            subtitle.characterSpacing = 3f;

            var backBtnRt = CreateElement(panel.transform, "BackBtnArea", typeof(RectTransform)).GetComponent<RectTransform>();
            backBtnRt.anchorMin = backBtnRt.anchorMax = new Vector2(0f, 1f);
            backBtnRt.anchoredPosition = new Vector2(135, -58);
            CreatePillButton(backBtnRt, "< CHẾ ĐỘ", m_CardColor1, 210, 58, 27, () => UpdateState(MenuState.ModeSelect));

            var content = CreateElement(panel.transform, "Content", typeof(RectTransform));
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = contentRt.anchorMax = contentRt.pivot = new Vector2(0.5f, 0.47f);
            contentRt.sizeDelta = new Vector2(1460, 540);
            contentRt.anchoredPosition = Vector2.zero;

            var card1 = CreateOfflineMapCard(content.transform, "DESERT", "TẦM NHÌN RỘNG",
                "Địa hình mở, ít chỗ ẩn nấp.\nPhù hợp giao tranh trực diện.",
                new Color(0.92f, 0.52f, 0.15f), "map_thumb_desert", () => StartOfflineMap("Desert"));
            card1.GetComponent<RectTransform>().anchoredPosition = new Vector2(-490, 0);

            var card2 = CreateOfflineMapCard(content.transform, "JUNGLE", "NHIỀU VẬT CẢN",
                "Rừng cây dày đặc, nhiều góc khuất.\nLý tưởng cho chiến thuật phục kích.",
                new Color(0.25f, 0.70f, 0.34f), "map_thumb_jungle", () => StartOfflineMap("Jungle"));
            card2.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            var card3 = CreateOfflineMapCard(content.transform, "MOON", "TRỌNG LỰC THẤP",
                "Không gian rộng với địa hình lạ.\nĐòi hỏi khả năng kiểm soát tốt.",
                new Color(0.50f, 0.38f, 0.82f), "map_thumb_moon", () => StartOfflineMap("Moon"));
            card3.GetComponent<RectTransform>().anchoredPosition = new Vector2(490, 0);

            var hintBg = CreateImage(panel.transform, "Hint",
                CreateRoundedRectSprite(64, 26, new Color(0.035f, 0.045f, 0.05f, 0.80f), new Color(1f, 1f, 1f, 0.16f), 2), Image.Type.Sliced, true);
            hintBg.rectTransform.anchorMin = hintBg.rectTransform.anchorMax = hintBg.rectTransform.pivot = new Vector2(0.5f, 0.075f);
            hintBg.rectTransform.sizeDelta = new Vector2(720, 54);
            hintBg.rectTransform.anchoredPosition = Vector2.zero;
            var hint = CreateTMP(hintBg.transform, "Label", "CHỌN MỘT BẢN ĐỒ ĐỂ BẮT ĐẦU TRẬN ĐẤU", 22,
                FontStyles.Bold | FontStyles.Italic, Color.white, TextAlignmentOptions.Center);
            StretchFull(hint.rectTransform);

            return panel;
        }

        private GameObject CreateOfflineMapCard(Transform parent, string title, string trait, string desc,
            Color accentColor, string imagePath, Action onClick)
        {
            var cardGo = CreateElement(parent, $"MapCard_{title}", typeof(RectTransform));
            var rt = cardGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(430, 520);

            CreateShadow(cardGo.transform, 112, 30, new Vector2(10, -12), new Vector2(10, -12), 0.34f);

            var fillImg = CreateImage(cardGo.transform, "Fill",
                CreateRoundedRectSprite(112, 28, new Color(0.035f, 0.045f, 0.05f, 0.96f), accentColor, 5), Image.Type.Sliced, true);
            var button = fillImg.gameObject.AddComponent<Button>();
            button.targetGraphic = fillImg;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.10f;
            button.colors = colors;

            var vibe = fillImg.gameObject.AddComponent<UIButtonVibe>();
            vibe.clickSound = m_ClickSound;
            button.onClick.AddListener(() => onClick());

            var imageMask = CreateElement(fillImg.transform, "ImageMask", typeof(RectTransform), typeof(Image), typeof(Mask));
            var maskRt = imageMask.GetComponent<RectTransform>();
            maskRt.anchorMin = new Vector2(0f, 0.42f);
            maskRt.anchorMax = new Vector2(1f, 1f);
            maskRt.offsetMin = new Vector2(8, 0);
            maskRt.offsetMax = new Vector2(-8, -8);
            var maskImg = imageMask.GetComponent<Image>();
            maskImg.sprite = CreateRoundedRectSprite(96, 22, Color.white, Color.clear, 0);
            maskImg.type = Image.Type.Sliced;
            imageMask.GetComponent<Mask>().showMaskGraphic = false;

            var thumb = CreateElement(imageMask.transform, "Thumbnail", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            var thumbImg = thumb.GetComponent<Image>();
            var sprite = LoadSpriteWithFallback(imagePath);
            if (sprite != null)
            {
                thumbImg.sprite = sprite;
                thumbImg.color = Color.white;
                var fitter = thumb.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            }
            else
            {
                StretchFull(thumb.GetComponent<RectTransform>());
                thumbImg.color = new Color(accentColor.r * 0.45f, accentColor.g * 0.45f, accentColor.b * 0.45f, 1f);
            }

            var imageShade = CreateImage(imageMask.transform, "BottomShade",
                CreateVerticalGradientSprite(new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0.76f)), Image.Type.Simple, false);
            imageShade.rectTransform.anchorMin = new Vector2(0f, 0f);
            imageShade.rectTransform.anchorMax = new Vector2(1f, 0.48f);
            imageShade.rectTransform.offsetMin = Vector2.zero;
            imageShade.rectTransform.offsetMax = Vector2.zero;

            var traitBg = CreateImage(fillImg.transform, "Trait",
                CreateRoundedRectSprite(64, 20, new Color(0.035f, 0.045f, 0.05f, 0.90f), accentColor, 3), Image.Type.Sliced, true);
            traitBg.rectTransform.anchorMin = traitBg.rectTransform.anchorMax = traitBg.rectTransform.pivot = new Vector2(0.5f, 0.42f);
            traitBg.rectTransform.sizeDelta = new Vector2(250, 42);
            traitBg.rectTransform.anchoredPosition = new Vector2(0, 0);
            var traitTmp = CreateTMP(traitBg.transform, "Label", trait, 17, FontStyles.Bold, accentColor, TextAlignmentOptions.Center);
            StretchFull(traitTmp.rectTransform);

            var titleTmp = CreateTMP(fillImg.transform, "Title", title, 38, FontStyles.Bold | FontStyles.Italic, Color.white, TextAlignmentOptions.Center);
            titleTmp.rectTransform.anchorMin = titleTmp.rectTransform.anchorMax = titleTmp.rectTransform.pivot = new Vector2(0.5f, 0f);
            titleTmp.rectTransform.sizeDelta = new Vector2(380, 54);
            titleTmp.rectTransform.anchoredPosition = new Vector2(0, 155);
            titleTmp.characterSpacing = 3f;

            var descTmp = CreateTMP(fillImg.transform, "Desc", desc, 20, FontStyles.Bold,
                new Color(1f, 1f, 1f, 0.70f), TextAlignmentOptions.Center);
            descTmp.rectTransform.anchorMin = descTmp.rectTransform.anchorMax = descTmp.rectTransform.pivot = new Vector2(0.5f, 0f);
            descTmp.rectTransform.sizeDelta = new Vector2(380, 62);
            descTmp.rectTransform.anchoredPosition = new Vector2(0, 91);

            var actionBg = CreateImage(fillImg.transform, "Action",
                CreateRoundedRectSprite(72, 22, accentColor, m_OutlineColor, 4), Image.Type.Sliced, true);
            actionBg.rectTransform.anchorMin = actionBg.rectTransform.anchorMax = actionBg.rectTransform.pivot = new Vector2(0.5f, 0f);
            actionBg.rectTransform.sizeDelta = new Vector2(330, 52);
            actionBg.rectTransform.anchoredPosition = new Vector2(0, 25);
            var actionTmp = CreateTMP(actionBg.transform, "Label", "CHỌN BẢN ĐỒ", 20,
                FontStyles.Bold | FontStyles.Italic, m_TextDark, TextAlignmentOptions.Center);
            StretchFull(actionTmp.rectTransform);

            return cardGo;
        }

        private void StartOfflineMap(string mapName)
        {
            UnityEngine.Debug.Log("Đang tải bản đồ: " + mapName);
            UnityEngine.SceneManagement.SceneManager.LoadScene(mapName);
        }
    }
}
