using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tanks.Complete
{
    /// <summary>
    /// The very first screen the game shows (build index 0). It displays the game title and a
    /// "press anything to start" prompt, then loads the main menu on any key / click / gamepad press.
    /// Like <see cref="MainMenuController"/> the whole UI is built in code so the scene only needs a
    /// single GameObject carrying this component.
    /// </summary>
    public class StartScreenController : MonoBehaviour
    {
        public string m_Title = "TANKS ARENA";
        public string m_Prompt = "PRESS ANY KEY TO START";
        [Tooltip("Scene loaded when the player presses anything.")]
        public string m_NextScene = "MainMenu";

        private TextMeshProUGUI m_PromptText;   // Cached so we can pulse its alpha for a bit of juice.
        private bool m_Loading;                 // Guard so we only trigger the load once.

        private void Awake()
        {
            EnsureEventSystem();
            BuildUI();
        }

        // The project uses the new Input System, so the EventSystem must use InputSystemUIInputModule.
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private void Update()
        {
            // Pulse the prompt alpha between ~0.35 and 1 for a gentle "blinking" invitation.
            if (m_PromptText != null)
            {
                float a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f));
                var c = m_PromptText.color;
                c.a = a;
                m_PromptText.color = c;
            }

            if (AnyInputPressed())
                StartGame();
        }

        // Detect a fresh press on keyboard, mouse or gamepad using the new Input System.
        private static bool AnyInputPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame)
                return true;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return true;

            var pad = Gamepad.current;
            if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame))
                return true;

            return false;
        }

        private void StartGame()
        {
            if (m_Loading)
                return;
            m_Loading = true;

            if (Application.CanStreamedLevelBeLoaded(m_NextScene))
            {
                SceneManager.LoadScene(m_NextScene);
            }
            else
            {
                Debug.LogError($"StartScreenController: scene '{m_NextScene}' is not in Build Settings.");
                m_Loading = false;
            }
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("StartCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Full-screen dark background. Clicking anywhere on it also starts the game.
            var bg = CreateStretchImage("Background", canvasGo.transform, new Color(0.07f, 0.08f, 0.11f, 1f));
            var bgButton = bg.gameObject.AddComponent<Button>();
            bgButton.transition = Selectable.Transition.None;
            bgButton.onClick.AddListener(StartGame);

            // Centred vertical stack: title + prompt.
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(bg.transform, false);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(1200, 500);
            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 40;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            CreateText("Title", panel.transform, m_Title, 130, FontStyles.Bold, new Color(0.96f, 0.82f, 0.25f), 180);
            m_PromptText = CreateText("Prompt", panel.transform, m_Prompt, 44, FontStyles.Bold,
                new Color(0.9f, 0.92f, 0.96f), 80);
        }

        // ---- UI helpers (mirrors MainMenuController) --------------------------

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
    }
}
