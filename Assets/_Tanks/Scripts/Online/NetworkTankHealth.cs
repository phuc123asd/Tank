using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    /// <summary>
    /// Server-authoritative health for the online tank (Slice 3). The server is the only writer of the
    /// <see cref="NetworkVariable{T}"/> health value; every client reflects it on the tank's world-space
    /// health bar via the change callback. When health reaches zero the server drives a synced death
    /// (explosion VFX + the tank hides itself) on all machines.
    ///
    /// It reuses the shipped <see cref="TankHealth"/>'s serialized references (slider, fill image, colours,
    /// explosion prefab) but disables that component so the offline health logic never runs online. The
    /// offline game keeps using TankHealth untouched — this component only lives on the online prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkTankHealth : NetworkBehaviour
    {
        private readonly NetworkVariable<float> m_Health = new NetworkVariable<float>(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private float m_StartingHealth = 100f;
        private Slider m_Slider;
        private Image m_FillImage;
        private Color m_FullHealthColor = Color.green;
        private Color m_ZeroHealthColor = Color.red;
        private GameObject m_ExplosionPrefab;

        private ParticleSystem m_ExplosionParticles;
        private AudioSource m_ExplosionAudio;
        private bool m_Dead;

        /// <summary>Server-side: is this tank currently dead (used by the round manager to count survivors).</summary>
        public bool IsDead => m_Dead;

        /// <summary>Raised on the server the moment this tank dies, so the round manager can react at once.</summary>
        public System.Action<NetworkTankHealth> OnServerDeath;

        private void Awake()
        {
            // Pull the serialized references off the offline TankHealth, then take it out of the loop so the
            // two never fight over the health bar / death.
            var offline = GetComponent<TankHealth>();
            if (offline != null)
            {
                m_StartingHealth = offline.m_StartingHealth;
                m_Slider = offline.m_Slider;
                m_FillImage = offline.m_FillImage;
                m_FullHealthColor = offline.m_FullHealthColor;
                m_ZeroHealthColor = offline.m_ZeroHealthColor;
                m_ExplosionPrefab = offline.m_ExplosionPrefab;
                offline.enabled = false;
            }

            if (m_ExplosionPrefab != null)
            {
                m_ExplosionParticles = Instantiate(m_ExplosionPrefab).GetComponent<ParticleSystem>();
                if (m_ExplosionParticles != null)
                {
                    m_ExplosionAudio = m_ExplosionParticles.GetComponent<AudioSource>();
                    m_ExplosionParticles.gameObject.SetActive(false);
                }
            }

            if (m_Slider != null)
                m_Slider.maxValue = m_StartingHealth;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                m_Health.Value = m_StartingHealth;

            m_Health.OnValueChanged += OnHealthChanged;
            UpdateHealthUI(m_Health.Value);
        }

        public override void OnNetworkDespawn()
        {
            m_Health.OnValueChanged -= OnHealthChanged;
        }

        private void OnDestroy()
        {
            if (m_ExplosionParticles != null)
                Destroy(m_ExplosionParticles.gameObject);
        }

        /// <summary>Server-only: apply area damage from a shell. Clamps at zero and triggers death.</summary>
        public void ServerApplyDamage(float amount)
        {
            if (!IsServer || m_Dead)
                return;

            m_Health.Value = Mathf.Max(0f, m_Health.Value - amount);

            if (m_Health.Value <= 0f)
            {
                m_Dead = true;
                DieClientRpc(transform.position);
                OnServerDeath?.Invoke(this);
            }
        }

        // ---- Round manager hooks (server-driven) ----

        /// <summary>Server-only: bring the tank back to full health and revive it on every client.</summary>
        public void ServerRoundReset()
        {
            if (!IsServer)
                return;

            m_Dead = false;
            m_Health.Value = m_StartingHealth;
            ReviveClientRpc();
        }

        /// <summary>Server-only: enable/disable player control on all clients (owner only actually drives).</summary>
        public void ServerSetControl(bool enabled)
        {
            if (!IsServer)
                return;

            ControlClientRpc(enabled);
        }

        [ClientRpc]
        private void ReviveClientRpc()
        {
            ShowVisuals();

            // Owner-authoritative transform: only the owner repositions itself; that replicates to peers.
            var setup = GetComponent<NetworkTankSetup>();
            if (setup != null)
                setup.MoveOwnerToSpawn();

            // Start frozen; the round manager enables control when the round actually begins.
            SetControl(false);
        }

        [ClientRpc]
        private void ControlClientRpc(bool enabled)
        {
            SetControl(enabled);
        }

        private void OnHealthChanged(float previous, float current)
        {
            UpdateHealthUI(current);
        }

        private void UpdateHealthUI(float health)
        {
            if (m_Slider != null)
                m_Slider.value = health;
            if (m_FillImage != null)
                m_FillImage.color = Color.Lerp(m_ZeroHealthColor, m_FullHealthColor, health / m_StartingHealth);
        }

        [ClientRpc]
        private void DieClientRpc(Vector3 position)
        {
            // Explosion VFX + audio, played on every machine.
            if (m_ExplosionParticles != null)
            {
                m_ExplosionParticles.transform.position = position;
                m_ExplosionParticles.gameObject.SetActive(true);
                m_ExplosionParticles.Play();
            }
            if (m_ExplosionAudio != null)
                m_ExplosionAudio.Play();
            if (CameraControl.Instance != null)
                CameraControl.Instance.Shake(0.3f, 0.5f);

            // Hide + neutralise the tank without deactivating the NetworkObject root (NGO needs it active).
            HideVisuals();
        }

        private void HideVisuals()
        {
            SetRenderersEnabled(false);
            SetControl(false);

            var col = GetComponent<Collider>();
            if (col != null)
                col.enabled = false;
        }

        private void ShowVisuals()
        {
            SetRenderersEnabled(true);

            var col = GetComponent<Collider>();
            if (col != null)
                col.enabled = true;
        }

        private void SetRenderersEnabled(bool enabled)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = enabled;

            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null)
                canvas.enabled = enabled;
        }

        // Only the owner actually drives its tank, so control only ever turns on for the owner.
        private void SetControl(bool enabled)
        {
            bool ownerControl = enabled && IsOwner;

            var move = GetComponent<TankMovement>();
            if (move != null)
                move.enabled = ownerControl;
            var shoot = GetComponent<TankShooting>();
            if (shoot != null)
                shoot.enabled = ownerControl;
        }
    }
}
