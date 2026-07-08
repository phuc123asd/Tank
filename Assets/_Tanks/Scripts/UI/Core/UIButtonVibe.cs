using UnityEngine;
using UnityEngine.EventSystems;

namespace Tanks.Complete
{
    /// <summary>
    /// Hiệu ứng click (Vibe) cho nút bấm UI.
    /// Tự động thu nhỏ nút khi bấm, phóng to khi thả ra và phát âm thanh Click.
    /// </summary>
    public class UIButtonVibe : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public AudioClip clickSound;

        private Vector3 m_OriginalScale;
        private bool m_IsHovered;

        private void Start()
        {
            m_OriginalScale = transform.localScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_IsHovered = true;
            StopAllCoroutines();
            StartCoroutine(ScaleTo(m_OriginalScale * 1.05f, 0.1f)); // Phóng to nhẹ khi di chuột vào
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_IsHovered = false;
            StopAllCoroutines();
            StartCoroutine(ScaleTo(m_OriginalScale, 0.1f)); // Trả về bình thường
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            StopAllCoroutines();
            StartCoroutine(ScaleTo(m_OriginalScale * 0.9f, 0.05f)); // Thu nhỏ móp vào khi bấm

            if (clickSound != null)
            {
                MusicManager.PlaySFX(clickSound);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            StopAllCoroutines();
            StartCoroutine(ScaleTo(m_IsHovered ? m_OriginalScale * 1.05f : m_OriginalScale, 0.1f)); // Bật ngửa ra
        }

        private System.Collections.IEnumerator ScaleTo(Vector3 target, float duration)
        {
            Vector3 startScale = transform.localScale;
            float t = 0;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                transform.localScale = Vector3.Lerp(startScale, target, t / duration);
                yield return null;
            }
            transform.localScale = target;
        }
    }
}
