using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    /// <summary>
    /// Presentation-only selected state for a map card. Network and map-selection logic
    /// remain in MainMenuController; this component only animates the visual hierarchy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIMapSelectionVisual : MonoBehaviour
    {
        private Image m_Glow;
        private Image m_Frame;
        private Image m_Shade;
        private GameObject m_Badge;
        private TextMeshProUGUI m_Name;
        private UIButtonVibe m_Vibe;
        private Color m_Accent;
        private bool m_Selected;

        public void Configure(
            Color accent,
            Image glow,
            Image frame,
            Image shade,
            GameObject badge,
            TextMeshProUGUI mapName,
            UIButtonVibe vibe)
        {
            m_Accent = accent;
            m_Glow = glow;
            m_Frame = frame;
            m_Shade = shade;
            m_Badge = badge;
            m_Name = mapName;
            m_Vibe = vibe;
        }

        public void SetSelected(bool selected, bool instant)
        {
            m_Selected = selected;

            if (m_Frame != null)
                m_Frame.color = selected ? m_Accent : new Color(1f, 1f, 1f, 0.20f);
            if (m_Shade != null)
                m_Shade.color = selected ? new Color(0f, 0f, 0f, 0.03f) : new Color(0f, 0f, 0f, 0.43f);
            if (m_Badge != null)
                m_Badge.SetActive(selected);
            if (m_Name != null)
                m_Name.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.68f);

            if (m_Vibe != null)
                m_Vibe.SetRestScale(Vector3.one * (selected ? 1.08f : 0.94f), !instant);
            else
                transform.localScale = Vector3.one * (selected ? 1.08f : 0.94f);

            UpdateGlow(0f);
        }

        private void Update()
        {
            if (!m_Selected)
                return;

            UpdateGlow(Time.unscaledTime);
        }

        private void UpdateGlow(float time)
        {
            if (m_Glow == null)
                return;

            float alpha = m_Selected ? 0.30f + Mathf.Sin(time * 3.2f) * 0.10f : 0f;
            m_Glow.color = new Color(m_Accent.r, m_Accent.g, m_Accent.b, alpha);
        }
    }
}
