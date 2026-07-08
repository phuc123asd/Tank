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
            bg.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.15f);

            var headerTmp = CreateTMP(panel.transform, "Header", "CHỌN CHIẾN TRƯỜNG", 70, FontStyles.Bold | FontStyles.Italic, Color.white, TextAlignmentOptions.Center);
            headerTmp.rectTransform.anchorMin = new Vector2(0, 1);
            headerTmp.rectTransform.anchorMax = new Vector2(1, 1);
            headerTmp.rectTransform.pivot = new Vector2(0.5f, 1);
            headerTmp.rectTransform.anchoredPosition = new Vector2(0, -80);
            headerTmp.characterSpacing = 8f;

            var backBtnRt = CreateElement(panel.transform, "BackBtnArea", typeof(RectTransform)).GetComponent<RectTransform>();
            backBtnRt.anchorMin = new Vector2(0, 1);
            backBtnRt.anchorMax = new Vector2(0, 1);
            backBtnRt.pivot = new Vector2(0, 1);
            backBtnRt.anchoredPosition = new Vector2(180, -80);
            CreatePillButton(backBtnRt, "< TRỞ VỀ", m_CyanColor, 250, 70, 36, () => UpdateState(MenuState.ModeSelect));

            var content = CreateElement(panel.transform, "Content", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 0);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.offsetMin = new Vector2(100, 100);
            contentRt.offsetMax = new Vector2(-100, -220);
            
            var hlg = content.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 80;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var card1 = CreateMapCard(content.transform, "DESERT", "Khí hậu khắc nghiệt,\nkhông chỗ ẩn nấp.", new Color(0.9f, 0.5f, 0.1f), () => StartOfflineMap("Desert"));
            var card2 = CreateMapCard(content.transform, "JUNGLE", "Cây cối rậm rạp,\nlý tưởng phục kích.", new Color(0.2f, 0.7f, 0.3f), () => StartOfflineMap("Jungle"));
            var card3 = CreateMapCard(content.transform, "MOON", "Trọng lực yếu,\nmôi trường không gian.", new Color(0.5f, 0.3f, 0.8f), () => StartOfflineMap("Moon"));

            return panel;
        }

        private GameObject CreateMapCard(Transform parent, string title, string desc, Color accentColor, Action onClick)
        {
            var cardGo = CreateElement(parent, $"MapCard_{title}", typeof(RectTransform));
            var rt = cardGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(450, 600);

            var fillImg = CreateImage(cardGo.transform, "Fill", CreateRoundedRectSprite(120, 40, accentColor, m_OutlineColor, 6), Image.Type.Sliced, true);
            var button = fillImg.gameObject.AddComponent<Button>();
            
            var vibe = fillImg.gameObject.AddComponent<UIButtonVibe>();
            vibe.clickSound = m_ClickSound;
            
            button.onClick.AddListener(() => onClick());

            var titleTmp = CreateTMP(fillImg.transform, "Title", title, 60, FontStyles.Bold | FontStyles.Italic, Color.white, TextAlignmentOptions.Center);
            titleTmp.rectTransform.anchorMin = new Vector2(0, 0.4f);
            titleTmp.rectTransform.anchorMax = new Vector2(1, 0.6f);
            
            var descTmp = CreateTMP(fillImg.transform, "Desc", desc, 32, FontStyles.Bold, new Color(0.95f, 0.95f, 0.95f), TextAlignmentOptions.Center);
            descTmp.rectTransform.anchorMin = new Vector2(0, 0.1f);
            descTmp.rectTransform.anchorMax = new Vector2(1, 0.4f);
            descTmp.rectTransform.offsetMin = new Vector2(20, 0);
            descTmp.rectTransform.offsetMax = new Vector2(-20, 0);

            return cardGo;
        }

        private void StartOfflineMap(string mapName)
        {
            UnityEngine.Debug.Log("Đang tải bản đồ: " + mapName);
            UnityEngine.SceneManagement.SceneManager.LoadScene(mapName);
        }
    }
}
