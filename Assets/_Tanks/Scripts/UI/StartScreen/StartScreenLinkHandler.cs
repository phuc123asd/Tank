using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Tanks.Complete
{
    public sealed class StartScreenLinkHandler : MonoBehaviour, IPointerClickHandler
    {
        private TextMeshProUGUI m_Text;
        private Action<string> m_OnLinkClicked;

        public void Init(TextMeshProUGUI text, Action<string> onLinkClicked)
        {
            m_Text = text;
            m_OnLinkClicked = onLinkClicked;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (m_Text == null)
                return;

            int linkIndex = TMP_TextUtilities.FindIntersectingLink(
                m_Text,
                eventData.position,
                eventData.pressEventCamera);

            if (linkIndex >= 0)
                m_OnLinkClicked?.Invoke(m_Text.textInfo.linkInfo[linkIndex].GetLinkID());
        }
    }
}
