using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    /// <summary>
    /// Offline-only health runtime. Online health still belongs to TankHealth/NetworkVariable.
    /// </summary>
    [DisallowMultipleComponent]
    public class OfflineTankHealthController : MonoBehaviour
    {
        private TankHealth m_SourceHealth;
        private ParticleSystem m_ExplosionParticles;
        private AudioSource m_ExplosionAudio;

        private float m_CurrentHealth;
        private bool m_Dead;

        private bool IsOffline => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;

        public bool IsConfiguredFor(TankHealth sourceHealth)
        {
            return m_SourceHealth == sourceHealth;
        }

        public void ConfigureFrom(TankHealth sourceHealth)
        {
            if (m_SourceHealth == sourceHealth)
                return;

            m_SourceHealth = sourceHealth;
            CacheExplosionPrefab();
            ResetHealth();
        }

        private void Awake()
        {
            if (m_SourceHealth == null)
                m_SourceHealth = GetComponent<TankHealth>();

            CacheExplosionPrefab();
        }

        private void OnEnable()
        {
            if (!IsOffline || m_SourceHealth == null) return;
            ResetHealth();
        }

        private void OnDestroy()
        {
            if (m_ExplosionParticles != null)
                Destroy(m_ExplosionParticles.gameObject);
        }

        public void TakeDamage(float amount)
        {
            if (!IsOffline || m_SourceHealth == null || m_Dead) return;

            m_CurrentHealth = Mathf.Max(0f, m_CurrentHealth - Mathf.Max(0f, amount));
            SetHealthUI();

            if (m_CurrentHealth <= 0f)
                Die();
        }

        public void Heal(float amount)
        {
            if (!IsOffline || m_SourceHealth == null || m_Dead) return;

            m_CurrentHealth = Mathf.Min(m_SourceHealth.m_StartingHealth, m_CurrentHealth + Mathf.Max(0f, amount));
            SetHealthUI();
        }

        private void ResetHealth()
        {
            m_CurrentHealth = m_SourceHealth.m_StartingHealth;
            m_Dead = false;
            SetHealthUI();
        }

        private void SetHealthUI()
        {
            if (m_SourceHealth == null) return;

            Slider slider = m_SourceHealth.m_Slider;
            Image fill = m_SourceHealth.m_FillImage;

            if (slider != null)
            {
                slider.maxValue = m_SourceHealth.m_StartingHealth;
                slider.value = m_CurrentHealth;
            }

            if (fill != null)
            {
                float healthRatio = m_SourceHealth.m_StartingHealth > 0f
                    ? m_CurrentHealth / m_SourceHealth.m_StartingHealth
                    : 0f;
                fill.color = Color.Lerp(m_SourceHealth.m_ZeroHealthColor, m_SourceHealth.m_FullHealthColor, healthRatio);
            }
        }

        private void CacheExplosionPrefab()
        {
            if (m_SourceHealth == null || m_SourceHealth.m_ExplosionPrefab == null || m_ExplosionParticles != null)
                return;

            m_ExplosionParticles = Instantiate(m_SourceHealth.m_ExplosionPrefab).GetComponent<ParticleSystem>();
            if (m_ExplosionParticles == null) return;

            m_ExplosionAudio = m_ExplosionParticles.GetComponent<AudioSource>();
            m_ExplosionParticles.gameObject.SetActive(false);
        }

        private void Die()
        {
            m_Dead = true;

            if (m_ExplosionParticles != null)
            {
                m_ExplosionParticles.transform.position = transform.position;
                m_ExplosionParticles.gameObject.SetActive(true);
                m_ExplosionParticles.Play();
            }

            if (m_ExplosionAudio != null)
                m_ExplosionAudio.Play();

            gameObject.SetActive(false);
        }
    }
}
