using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Tanks.Complete
{
    public partial class MainMenuController
    {
        #region UI Helpers (Kế thừa Vibe từ StartScreen)

        private static GameObject CreatePanel(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            StretchFull(rt);
            return go;
        }

        private static void CreateShadowedTitle(Transform parent, string text, float size)
        {
            var fore = CreateTMP(parent, "Fore", text, size, FontStyles.Bold | FontStyles.Italic, Color.white);
            fore.rectTransform.anchoredPosition = Vector2.zero;
        }

        private Button CreatePillButton(Transform parent, string label, Color color, float width, float height, float fontSize, UnityEngine.Events.UnityAction onClick)
        {
            var rt = CreateElement(parent, "PillButton_" + label, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = Vector2.zero;

            var fillGo = CreateElement(rt, "Fill", typeof(RectTransform), typeof(Image), typeof(Button));
            StretchFull(fillGo.GetComponent<RectTransform>());
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.sprite = CreateRoundedRectSprite(120, (int)height/2, color, m_OutlineColor, 5);
            fillImg.type = Image.Type.Sliced;

            var button = fillGo.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f); 
            colors.pressedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.colorMultiplier = 1;
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            var vibe = fillGo.AddComponent<UIButtonVibe>();
            vibe.clickSound = m_ClickSound;
            
            var labelTmp = CreateTMP(fillGo.transform, "Label", label, fontSize, FontStyles.Bold | FontStyles.Italic, m_TextDark);
            labelTmp.characterSpacing = 4f;

            button.onClick.AddListener(onClick);
            return button;
        }

        private void CreateCallout(Transform parent, string text, Color color, float width, float height)
        {
            var rt = parent.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);

            var fillImg = CreateImage(rt, "Fill", CreateRoundedRectSprite(64, (int)height/2, color, m_OutlineColor, 4), Image.Type.Sliced, true);

            var label = CreateTMP(fillImg.transform, "Label", text, height * 0.45f, FontStyles.Bold | FontStyles.Italic, Color.white);
            label.characterSpacing = 2f;
        }

        #endregion

        #region Core UI Generators

        private static GameObject CreateElement(Transform parent, string name, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static Image CreateImage(Transform parent, string name, Sprite sprite, Image.Type type = Image.Type.Simple, bool stretch = false)
        {
            var img = CreateElement(parent, name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.sprite = sprite;
            img.type = type;
            if (stretch) StretchFull(img.rectTransform);
            return img;
        }

        private static Image CreateShadow(Transform parent, int size, int radius, Vector2 offsetMin, Vector2 offsetMax, float alpha = 0.35f)
        {
            var img = CreateImage(parent, "Shadow", CreateRoundedRectSprite(size, radius, new Color(0f, 0f, 0f, alpha), Color.clear, 0), Image.Type.Sliced, true);
            var rt = img.rectTransform;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return img;
        }

        private static TextMeshProUGUI CreateTMP(Transform parent, string name, string text, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var tmp = CreateElement(parent, name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            StretchFull(tmp.rectTransform);
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Sprite CreateVerticalGradientSprite(Color top, Color bottom)
        {
            const int h = 256;
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            {
                var c = Color.Lerp(bottom, top, y / (float)(h - 1));
                tex.SetPixel(0, y, c);
                tex.SetPixel(1, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateRoundedRectSprite(int size, int radius, Color fill, Color border, int borderWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = 0f, dy = 0f;
                    if (x < radius) dx = radius - x;
                    else if (x >= size - radius) dx = x - (size - radius - 1);
                    if (y < radius) dy = radius - y;
                    else if (y >= size - radius) dy = y - (size - radius - 1);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    Color c;
                    if (dist > radius) c = Color.clear;
                    else if (borderWidth > 0 && dist > radius - borderWidth) c = border;
                    else c = fill;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            var b = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, b);
        }

        private static Sprite CreateDuneSilhouetteSprite(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var clear = new Color(0f, 0f, 0f, 0f);

            for (int x = 0; x < width; x++)
            {
                float t = x / (float)width;
                float wave = Mathf.Sin(t * Mathf.PI * 2f) * 0.18f + Mathf.Sin(t * Mathf.PI * 5f + 1.3f) * 0.09f + Mathf.Sin(t * Mathf.PI * 11f + 2.7f) * 0.04f;
                int topY = Mathf.Clamp(Mathf.RoundToInt((0.55f + wave) * height), 1, height - 1);
                for (int y = 0; y < height; y++) tex.SetPixel(x, y, y <= topY ? color : clear);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0f));
        }

        #endregion
    }
}
