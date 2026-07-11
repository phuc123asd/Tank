using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace Tanks.Complete
{
    public partial class MainMenuController
    {
        private GameObject CreateHomePanel(Transform parent)
        {
            var panel = CreatePanel("HomePanel", parent);

            var topBar = CreateElement(panel.transform, "TopBar", typeof(RectTransform)).GetComponent<RectTransform>();
            topBar.anchorMin = topBar.anchorMax = topBar.pivot = new Vector2(0.5f, 1);
            topBar.anchoredPosition = new Vector2(0, -40);
            topBar.sizeDelta = new Vector2(1840, 80);

            var profileBox = CreateElement(topBar.transform, "ProfileBox", typeof(RectTransform)).GetComponent<RectTransform>();
            profileBox.anchorMin = profileBox.anchorMax = profileBox.pivot = new Vector2(0, 0.5f);
            profileBox.anchoredPosition = new Vector2(20, 0); // Đẩy nhẹ vào trong
            
            // 1. Avatar Tròn
            var avatarImg = CreateImage(profileBox, "Avatar", CreateRoundedRectSprite(100, 50, Color.white, m_OutlineColor, 4), Image.Type.Simple, false);
            avatarImg.rectTransform.anchorMin = avatarImg.rectTransform.anchorMax = avatarImg.rectTransform.pivot = new Vector2(0, 0.5f);
            avatarImg.rectTransform.sizeDelta = new Vector2(80, 80);
            avatarImg.rectTransform.anchoredPosition = new Vector2(0, 0);

            // 2. Tên người chơi
            var nameRt = CreateElement(profileBox, "NameBox", typeof(RectTransform)).GetComponent<RectTransform>();
            nameRt.anchorMin = nameRt.anchorMax = nameRt.pivot = new Vector2(0, 0.5f);
            nameRt.anchoredPosition = new Vector2(100, 0);
            CreateCallout(nameRt, "OMG1989", m_CyanColor, 220, 50);

            // 3. Rank
            var rankRt = CreateElement(profileBox, "RankBox", typeof(RectTransform)).GetComponent<RectTransform>();
            rankRt.anchorMin = rankRt.anchorMax = rankRt.pivot = new Vector2(0, 0.5f);
            rankRt.anchoredPosition = new Vector2(330, 0);
            CreateCallout(rankRt, "BẠCH KIM", m_CardColor1, 200, 50);

            var currencyBox = CreateElement(topBar.transform, "CurrencyBox", typeof(RectTransform)).GetComponent<RectTransform>();
            currencyBox.anchorMin = currencyBox.anchorMax = currencyBox.pivot = new Vector2(1, 0.5f);
            currencyBox.anchoredPosition = Vector2.zero;
            CreateCallout(currencyBox, "VÀNG: 15,000  |  QUÂN HUY: 250", m_PlayButtonColor, 600, 70);


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

            var chatBox = CreateElement(panel.transform, "ChatBox", typeof(RectTransform)).GetComponent<RectTransform>();
            chatBox.anchorMin = chatBox.anchorMax = chatBox.pivot = new Vector2(0, 0);
            chatBox.anchoredPosition = new Vector2(40, 40);
            CreateCallout(chatBox, "[Thế giới] Tìm đồng đội leo rank 2v2...", new Color(0.15f, 0.15f, 0.15f, 0.8f), 550, 80);

            var startBtnRt = CreateElement(panel.transform, "StartBtnContainer", typeof(RectTransform)).GetComponent<RectTransform>();
            startBtnRt.anchorMin = startBtnRt.anchorMax = new Vector2(1f, 0f); // Góc dưới cùng bên phải
            startBtnRt.anchoredPosition = new Vector2(-240, 165); // Nâng trục Y lên 160 để đè lên logo
            CreatePillButton(startBtnRt, "LEO RANK", m_PlayButtonColor, 400, 100, 60, () => UpdateState(MenuState.ModeSelect));

            return panel;
        }
    }
}
