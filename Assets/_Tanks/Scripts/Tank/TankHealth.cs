using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;

namespace Tanks.Complete
{
    public class TankHealth : NetworkBehaviour
    {
        public float m_StartingHealth = 100f;               // The amount of health each tank starts with.
        public Slider m_Slider;                             // The slider to represent how much health the tank currently has.
        public Image m_FillImage;                           // The image component of the slider.
        public Color m_FullHealthColor = Color.green;    // The color the health bar will be when on full health.
        public Color m_ZeroHealthColor = Color.red;      // The color the health bar will be when on no health.
        public GameObject m_ExplosionPrefab;                // A prefab that will be instantiated in Awake, then used whenever the tank dies.
        [HideInInspector] public bool m_HasShield;          // Has the tank picked up a shield power up?
        
        
        private AudioSource m_ExplosionAudio;               // The audio source to play when the tank explodes.
        private ParticleSystem m_ExplosionParticles;        // The particle system the will play when the tank is destroyed.
        public NetworkVariable<float> m_CurrentHealth = new NetworkVariable<float>(100f);
        private bool m_Dead;                                // Has the tank been reduced beyond zero health yet?
        private float m_ShieldValue;                        // Percentage of reduced damage when the tank has a shield.
        private bool m_IsInvincible;                        // Is the tank invincible in this moment?

        public override void OnNetworkSpawn()
        {
            m_CurrentHealth.OnValueChanged += OnHealthChanged;
            SetHealthUI();
        }

        private void Awake ()
        {
            // Instantiate the explosion prefab and get a reference to the particle system on it.
            m_ExplosionParticles = Instantiate (m_ExplosionPrefab).GetComponent<ParticleSystem> ();

            // Get a reference to the audio source on the instantiated prefab.
            m_ExplosionAudio = m_ExplosionParticles.GetComponent<AudioSource> ();

            // Disable the prefab so it can be activated when it's required.
            m_ExplosionParticles.gameObject.SetActive (false);
            
            // Set the slider max value to the max health the tank can have
            m_Slider.maxValue = m_StartingHealth;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            m_CurrentHealth.OnValueChanged -= OnHealthChanged;
            if(m_ExplosionParticles != null)
                Destroy(m_ExplosionParticles.gameObject);
        }

        private void OnEnable()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsServer)
            {
                m_CurrentHealth.Value = m_StartingHealth;
            }
            m_Dead = false;
            m_HasShield = false;
            m_ShieldValue = 0;
            m_IsInvincible = false;

            if (IsSpawned)
            {
                SetHealthUI();
            }
        }


        public void TakeDamage (float amount)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;

            if (!m_IsInvincible)
            {
                m_CurrentHealth.Value -= amount * (1 - m_ShieldValue);
            }
        }


        public void IncreaseHealth(float amount)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;

            if (m_CurrentHealth.Value + amount <= m_StartingHealth)
            {
                m_CurrentHealth.Value += amount;
            }
            else
            {
                m_CurrentHealth.Value = m_StartingHealth;
            }
        }


        public void ToggleShield (float shieldAmount)
        {
            // Inverts the value of has shield.
            m_HasShield = !m_HasShield;

            // Stablish the amount of damage that will be reduced by the shield
            if (m_HasShield)
            {
                m_ShieldValue = shieldAmount;
            }
            else
            {
                m_ShieldValue = 0;
            }
        }

        public void ToggleInvincibility()
        {
            m_IsInvincible = !m_IsInvincible;
        }


        private void SetHealthUI ()
        {
            m_Slider.value = m_CurrentHealth.Value;
            m_FillImage.color = Color.Lerp (m_ZeroHealthColor, m_FullHealthColor, m_CurrentHealth.Value / m_StartingHealth);
        }

        private void OnHealthChanged(float oldVal, float newVal)
        {
            SetHealthUI();
            if (newVal <= 0f && !m_Dead)
            {
                OnDeath();
            }
        }


        private void OnDeath ()
        {
            // Set the flag so that this function is only called once.
            m_Dead = true;

            // Move the instantiated explosion prefab to the tank's position and turn it on.
            m_ExplosionParticles.transform.position = transform.position;
            m_ExplosionParticles.gameObject.SetActive (true);

            // Play the particle system of the tank exploding.
            m_ExplosionParticles.Play ();

            // Play the tank explosion sound effect.
            m_ExplosionAudio.Play();

            // Turn the tank off.
            gameObject.SetActive (false);
        }
    }
}