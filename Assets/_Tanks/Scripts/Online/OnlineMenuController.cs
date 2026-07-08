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
    /// Self-contained "Play Online" menu. Like <see cref="MainMenuController"/> the whole UI is built
    /// in code so the scene only needs a single GameObject carrying this component.
    ///
    /// Phase 1 scope: ensure UGS is signed in and show the player identity/status. The HOST and JOIN
    /// buttons are placeholders here and get wired to the Lobby/Relay layer in Phase 2.
    /// </summary>
    public class OnlineMenuController : MonoBehaviour
    {
        [Tooltip("Scene loaded when pressing BACK (the offline main menu).")]
        public string m_MainMenuScene = "MainMenu";

        private UGSBootstrap m_UGS;
        private OnlineGameConnector m_Connector;
        private TextMeshProUGUI m_StatusText;
        private Button m_HostButton;
        private Button m_JoinButton;
        private TMP_InputField m_JoinCodeInput;
        private string m_LastStatusShown = null;

        private void Awake()
        {
            EnsureEventSystem();
            m_UGS = UGSBootstrap.Ensure();
            m_Connector = OnlineGameConnector.Ensure();
            BuildUI();
        }

        private void Update()
        {
            // Show the connection status once a host/join has been attempted, otherwise the sign-in status.
            if (m_StatusText == null)
                return;

            string label = !string.IsNullOrEmpty(m_Connector != null ? m_Connector.StatusMessage : null)
                ? m_Connector.StatusMessage
                : (m_UGS != null ? m_UGS.StatusLabel() : string.Empty);

            if (label != m_LastStatusShown)
            {
                m_StatusText.text = label;
                m_LastStatusShown = label;
            }

            // Buttons are usable only once signed in and not mid-operation.
            bool ready = m_UGS != null && m_UGS.IsSignedIn && (m_Connector == null || !m_Connector.IsBusy);
            if (m_HostButton != null) m_HostButton.interactable = ready;
            if (m_JoinButton != null) m_JoinButton.interactable = ready;
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

        private void BuildUI()
        {
            var canvasGo = new GameObject("OnlineMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var bg = CreateStretchImage("Background", canvasGo.transform, new Color(0.08f, 0.11f, 0.14f, 1f));

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(bg.transform, false);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(760, 820);
            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 20;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            CreateText("Title", panel.transform, "PLAY ONLINE", 84, FontStyles.Bold, new Color(0.95f, 0.95f, 0.98f), 120);
            m_StatusText = CreateText("Status", panel.transform, "...", 30, FontStyles.Normal, new Color(0.62f, 0.80f, 0.95f), 110);

            CreateSpacer(panel.transform, 12);

            m_HostButton = CreateButton(panel.transform, "TAO PHONG (HOST)", new Color(0.24f, 0.55f, 0.35f), 92, OnHostClicked);
            m_JoinButton = CreateButton(panel.transform, "VAO PHONG (JOIN)", new Color(0.26f, 0.44f, 0.62f), 92, OnJoinClicked);

            // Join code input field.
            m_JoinCodeInput = CreateInputField(panel.transform, "Nhap ma phong...", 70);

            CreateSpacer(panel.transform, 16);
            CreateButton(panel.transform, "QUAY LAI", new Color(0.42f, 0.42f, 0.46f), 72, OnBackClicked);

            // Buttons start disabled until sign-in completes; Update() re-enables them.
            m_HostButton.interactable = false;
            m_JoinButton.interactable = false;
        }

        // ---- Button handlers (Phase 2 wires these to Lobby/Relay) --------------

        private void OnHostClicked()
        {
            Debug.Log("[Online] HOST pressed.");
            // Fire-and-forget: status is polled from the connector in Update().
            _ = m_Connector.HostAsync();
        }

        private void OnJoinClicked()
        {
            var code = m_JoinCodeInput != null ? m_JoinCodeInput.text : string.Empty;
            Debug.Log($"[Online] JOIN pressed with code '{code}'.");
            _ = m_Connector.JoinAsync(code);
        }

        private void OnBackClicked()
        {
            if (Application.CanStreamedLevelBeLoaded(m_MainMenuScene))
                SceneManager.LoadScene(m_MainMenuScene);
            else
                Debug.LogError($"OnlineMenuController: scene '{m_MainMenuScene}' is not in Build Settings.");
        }

        // ---- UI helpers (kept local to stay self-contained, matching MainMenuController) ----

        private static Image CreateStretchImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            return go.GetComponent<Image>();
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

        private static Button CreateButton(Transform parent, string label, Color accent, float height, Action onClick)
        {
            var go = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            go.GetComponent<Image>().color = accent;

            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 40;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            return button;
        }

        private static TMP_InputField CreateInputField(Transform parent, string placeholder, float height)
        {
            var go = new GameObject("JoinCodeInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            go.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.22f, 1f);

            var input = go.GetComponent<TMP_InputField>();

            // Text area (viewport) so text is clipped to the field.
            var textArea = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(go.transform, false);
            var taRt = textArea.GetComponent<RectTransform>();
            taRt.anchorMin = Vector2.zero;
            taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(20, 8);
            taRt.offsetMax = new Vector2(-20, -8);

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            placeholderGo.transform.SetParent(textArea.transform, false);
            StretchToParent(placeholderGo.GetComponent<RectTransform>());
            var phTmp = placeholderGo.GetComponent<TextMeshProUGUI>();
            phTmp.text = placeholder;
            phTmp.fontSize = 34;
            phTmp.fontStyle = FontStyles.Italic;
            phTmp.color = new Color(0.6f, 0.6f, 0.65f, 0.8f);
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(textArea.transform, false);
            StretchToParent(textGo.GetComponent<RectTransform>());
            var txtTmp = textGo.GetComponent<TextMeshProUGUI>();
            txtTmp.fontSize = 34;
            txtTmp.color = Color.white;
            txtTmp.alignment = TextAlignmentOptions.MidlineLeft;

            input.textViewport = taRt;
            input.textComponent = txtTmp;
            input.placeholder = phTmp;
            input.characterLimit = 8;
            input.text = string.Empty;

            return input;
        }

        private static void StretchToParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
