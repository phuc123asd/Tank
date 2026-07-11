using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Tanks.Complete
{
    public partial class MainMenuController
    {
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
            CreateShadowedTitle(titleRt.transform, "PHÒNG CHỜ 2v2", 90f);

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
    }
}
