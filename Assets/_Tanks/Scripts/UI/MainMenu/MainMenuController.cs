using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tanks.Complete
{
    /// <summary>
    /// Trình quản lý Main Menu của game, tự động sinh UI bằng code.
    /// Giữ nguyên phong cách (vibe) Arcade vui nhộn của màn hình StartScreen (Sa mạc, nút viền đen, đổ bóng).
    /// Bao gồm: Trang Chủ (Home), Chọn Chế Độ (ModeSelect), và Phòng Chờ 5v5 (Lobby5v5).
    /// </summary>
    public partial class MainMenuController : MonoBehaviour
    {
        public enum MenuState { Home, ModeSelect, Lobby5v5, OfflineMapSelect }

        private MenuState m_CurrentState = MenuState.Home;

        [SerializeField] private GameObject m_HomePanel;
        [SerializeField] private GameObject m_ModeSelectPanel;
        [SerializeField] private GameObject m_Lobby5v5Panel;
        [SerializeField] private GameObject m_OfflineMapSelectPanel;

        // --- Bảng màu chuẩn Vibe "Tanks!" ---
        private readonly Color m_BgColor = new Color(0.85f, 0.51f, 0.16f);       // Cam nền
        private readonly Color m_DuneColor = new Color(0.75f, 0.42f, 0.12f);     // Cam đậm đụn cát
        private readonly Color m_OutlineColor = new Color(0.15f, 0.15f, 0.15f);  // Đen viền nút
        private readonly Color m_PlayButtonColor = new Color(0.95f, 0.85f, 0.35f); // Vàng nút Play
        private readonly Color m_CyanColor = new Color(0.25f, 0.70f, 0.85f);     // Xanh biển Cyan
        private readonly Color m_CardColor1 = new Color(0.70f, 0.30f, 0.30f);    // Đỏ nhạt
        private readonly Color m_CardColor2 = new Color(0.30f, 0.62f, 0.32f);    // Xanh lá
        private readonly Color m_TextDark = new Color(0.2f, 0.2f, 0.2f);         // Chữ đen xám

        [Header("Audio Settings")]
        [SerializeField] private AudioClip m_MusicHome;
        [SerializeField] private AudioClip m_MusicModeSelect;
        [SerializeField] private AudioClip m_MusicLobby;
        [SerializeField] private AudioClip m_ClickSound;

        private void Awake()
        {
            EnsureEventSystem();
            
            if (m_MusicHome == null)
            {
#if UNITY_EDITOR
                m_MusicHome = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Tanks/Audio/Music/Music_Western.ogg");
                m_MusicModeSelect = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Tanks/Audio/Music/Music_Funky.ogg");
                m_MusicLobby = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Tanks/Audio/Music/Music_Steady.ogg");
#endif
            }

            if (m_ClickSound == null)
            {
#if UNITY_EDITOR
                m_ClickSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Tanks/Audio/SFX/PickupPowerUp.wav");
#endif
            }

            var existingCanvas = transform.Find("MainMenuCanvas");
            if (existingCanvas != null)
            {
                m_HomePanel = existingCanvas.Find("HomePanel")?.gameObject;
                m_ModeSelectPanel = existingCanvas.Find("ModeSelectPanel")?.gameObject;
                m_Lobby5v5Panel = existingCanvas.Find("Lobby5v5Panel")?.gameObject;
                m_OfflineMapSelectPanel = existingCanvas.Find("OfflineMapSelectPanel")?.gameObject;
                
                // Tự động tạo nếu chưa được bake
                if (m_OfflineMapSelectPanel == null) {
                    m_OfflineMapSelectPanel = CreateOfflineMapSelectPanel(existingCanvas);
                }

                var allButtons = existingCanvas.GetComponentsInChildren<Button>(true);
                foreach (var btn in allButtons)
                {
                    var vibe = btn.GetComponent<UIButtonVibe>();
                    if (vibe == null) vibe = btn.gameObject.AddComponent<UIButtonVibe>();
                    vibe.clickSound = m_ClickSound;

                    if (btn.transform.parent == null) continue;
                    string parentName = btn.transform.parent.name;

                    if (parentName == "PillButton_LEO RANK")
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => UpdateState(MenuState.ModeSelect));
                    }
                    else if (parentName == "PillButton_< TRỞ VỀ")
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => UpdateState(MenuState.Home));
                    }
                    else if (parentName == "Card_TEAM COMBAT\n(5v5)")
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => UpdateState(MenuState.Lobby5v5));
                    }
                    else if (parentName == "PillButton_< RỜI PHÒNG")
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => UpdateState(MenuState.ModeSelect));
                    }
                    else if (parentName == "Card_OFFLINE BOTS")
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => UpdateState(MenuState.OfflineMapSelect));
                    }
                    else if (parentName == "MapCard_DESERT")
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => StartOfflineMap("Desert"));
                    }
                    else if (parentName == "MapCard_JUNGLE")
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => StartOfflineMap("Jungle"));
                    }
                    else if (parentName == "MapCard_MOON")
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => StartOfflineMap("Moon"));
                    }
                }

                UpdateState(MenuState.Home);
            }
            else
            {
                BuildUI();
                UpdateState(MenuState.Home);
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            CreateImage(canvasGo.transform, "Background", CreateVerticalGradientSprite(m_BgColor, m_BgColor), Image.Type.Simple, true);

            var duneGo = CreateImage(canvasGo.transform, "Dune", CreateDuneSilhouetteSprite(1024, 200, m_DuneColor), Image.Type.Simple, false);
            var duneRt = duneGo.rectTransform;
            duneRt.anchorMin = Vector2.zero;
            duneRt.anchorMax = new Vector2(1f, 0f);
            duneRt.offsetMin = Vector2.zero;
            duneRt.offsetMax = new Vector2(0f, 200f);

            m_HomePanel = CreateHomePanel(canvasGo.transform);
            m_ModeSelectPanel = CreateModeSelectPanel(canvasGo.transform);
            m_Lobby5v5Panel = CreateLobby5v5Panel(canvasGo.transform);
            m_OfflineMapSelectPanel = CreateOfflineMapSelectPanel(canvasGo.transform);
        }

        private void UpdateState(MenuState newState)
        {
            m_CurrentState = newState;
            if (m_HomePanel) m_HomePanel.SetActive(m_CurrentState == MenuState.Home);
            if (m_ModeSelectPanel) m_ModeSelectPanel.SetActive(m_CurrentState == MenuState.ModeSelect);
            if (m_Lobby5v5Panel) m_Lobby5v5Panel.SetActive(m_CurrentState == MenuState.Lobby5v5);
            if (m_OfflineMapSelectPanel) m_OfflineMapSelectPanel.SetActive(m_CurrentState == MenuState.OfflineMapSelect);

            AudioClip nextClip = null;
            if (m_CurrentState == MenuState.Home) nextClip = m_MusicHome;
            else if (m_CurrentState == MenuState.ModeSelect) nextClip = m_MusicModeSelect;
            else if (m_CurrentState == MenuState.Lobby5v5) nextClip = m_MusicLobby;

            if (nextClip != null)
            {
                MusicManager.PlayMusic(nextClip);
            }
        }


#if UNITY_EDITOR
        [ContextMenu("Bake UI To Hierarchy")]
        public void BakeUIToHierarchy()
        {
            var old = transform.Find("MainMenuCanvas");
            if (old) DestroyImmediate(old.gameObject);

            BuildUI();

            if (!AssetDatabase.IsValidFolder("Assets/_Tanks/Sprites"))
                AssetDatabase.CreateFolder("Assets/_Tanks", "Sprites");
            if (!AssetDatabase.IsValidFolder("Assets/_Tanks/Sprites/GeneratedUI"))
                AssetDatabase.CreateFolder("Assets/_Tanks/Sprites", "GeneratedUI");

            var images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.sprite != null && img.sprite.texture != null && !AssetDatabase.Contains(img.sprite.texture))
                {
                    string path = $"Assets/_Tanks/Sprites/GeneratedUI/{img.sprite.texture.name}_{img.sprite.texture.GetHashCode()}.asset";
                    var tex = img.sprite.texture;
                    var spr = img.sprite;
                    AssetDatabase.CreateAsset(tex, path);
                    AssetDatabase.AddObjectToAsset(spr, tex);
                    AssetDatabase.SaveAssets();
                }
            }

            m_HomePanel = transform.Find("MainMenuCanvas/HomePanel").gameObject;
            m_ModeSelectPanel = transform.Find("MainMenuCanvas/ModeSelectPanel").gameObject;
            m_Lobby5v5Panel = transform.Find("MainMenuCanvas/Lobby5v5Panel").gameObject;
            var offlinePanel = transform.Find("MainMenuCanvas/OfflineMapSelectPanel");
            if (offlinePanel) m_OfflineMapSelectPanel = offlinePanel.gameObject;
            
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log("Đã nướng (Bake) UI thành công lên Hierarchy và lưu Sprite!");
        }
#endif
    }
}
