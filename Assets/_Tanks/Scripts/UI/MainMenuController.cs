using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tanks.Complete
{
    /// <summary>
    /// A self-contained main menu that lets the player pick a battlefield (which loads the matching
    /// game scene) or quit. The whole UI is built in code so the scene only needs a single GameObject
    /// carrying this component. This avoids fragile UI wiring and makes the menu robust to rebuilds
    /// (e.g. when the GameManager returns here via LoadScene(0) after a game is won).
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Serializable]
        public struct MapEntry
        {
            public string DisplayName;   // Label shown on the button
            public string SceneName;     // Scene to load (must be in Build Settings)
            public Color Color;          // Accent color of the button
        }

        [Tooltip("The battlefields the player can choose from. Each loads its scene by name.")]
        public MapEntry[] m_Maps =
        {
            new MapEntry { DisplayName = "DESERT", SceneName = "Desert", Color = new Color(0.85f, 0.62f, 0.28f) },
            new MapEntry { DisplayName = "JUNGLE", SceneName = "Jungle", Color = new Color(0.30f, 0.62f, 0.32f) },
            new MapEntry { DisplayName = "MOON",   SceneName = "Moon",   Color = new Color(0.45f, 0.50f, 0.62f) },
        };

        public string m_Title = "TANKS ARENA";
        public string m_Subtitle = "Select a battlefield";

        private void Awake()
        {
            EnsureEventSystem();
            BuildUI();
        }

        // The project uses the new Input System, so the EventSystem must use InputSystemUIInputModule
        // for UI clicks to register. Create one if the scene doesn't already have it.
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildUI()
        {
            // Root canvas covering the whole screen
            var canvasGo = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Full-screen dark background
            var bg = CreateStretchImage("Background", canvasGo.transform, new Color(0.09f, 0.10f, 0.13f, 1f));

            // Vertical layout container in the centre
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(bg.transform, false);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(700, 760);
            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 22;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Title + subtitle
            CreateText("Title", panel.transform, m_Title, 92, FontStyles.Bold, new Color(0.95f, 0.95f, 0.98f), 130);
            CreateText("Subtitle", panel.transform, m_Subtitle, 34, FontStyles.Normal, new Color(0.65f, 0.68f, 0.75f), 60);

            // Spacer
            CreateSpacer(panel.transform, 20);

            // One button per map
            foreach (var map in m_Maps)
            {
                var captured = map;
                CreateButton(panel.transform, map.DisplayName, map.Color, 96, () => LoadMap(captured.SceneName));
            }

            // Spacer + Quit
            CreateSpacer(panel.transform, 20);
            CreateButton(panel.transform, "QUIT", new Color(0.55f, 0.20f, 0.22f), 80, QuitGame);
        }

        private void LoadMap(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("MainMenuController: map has no scene name assigned.");
                return;
            }

            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SceneManager.LoadScene(sceneName);
            }
            else
            {
                Debug.LogError($"MainMenuController: scene '{sceneName}' is not in Build Settings. Add it to File > Build Settings.");
            }
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- UI helpers -------------------------------------------------------

        private static Image CreateStretchImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size,
            FontStyles style, Color color, float preferredHeight)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            go.GetComponent<LayoutElement>().preferredHeight = preferredHeight;
            return tmp;
        }

        private static void CreateSpacer(Transform parent, float height)
        {
            var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
        }

        private static void CreateButton(Transform parent, string label, Color accent, float height, Action onClick)
        {
            var go = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            go.GetComponent<LayoutElement>().preferredHeight = height;

            var img = go.GetComponent<Image>();
            img.color = accent;

            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f); // slightly brighter
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());

            // Button label
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 44;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
        }
    }
}
