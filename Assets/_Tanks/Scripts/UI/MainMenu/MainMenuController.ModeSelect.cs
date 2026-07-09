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

            var card1 = CreateModeCard(modesGroup, "SOLO ARENA\n(1v1)", "Đấu Trường Sa Mạc", m_CardColor1, () => UpdateState(MenuState.Lobby1v1));
            card1.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);

            var card2 = CreateModeCard(modesGroup, "TEAM COMBAT\n(5v5)", "Bản Đồ Chiến Lược", m_CardColor2, () => UpdateState(MenuState.Lobby5v5));
            card2.GetComponent<RectTransform>().anchoredPosition = new Vector2(450, 0);

            return panel;
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
    }
}
