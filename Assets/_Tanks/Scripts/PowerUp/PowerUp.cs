using UnityEngine;
using Unity.Netcode;

namespace Tanks.Complete
{
    public class PowerUp : MonoBehaviour
    {
        public enum PowerUpType { Speed, DamageReduction, ShootingBonus, Healing, Invincibility, DamageMultiplier }
        [Tooltip("Select the kind of Power Up that you want.")]
        [SerializeField] private PowerUpType m_PowerUpType = PowerUpType.DamageReduction;

        [Tooltip("Particle to emit when this Power Up is collected.")]
        [SerializeField] private ParticleSystem m_CollectFX;
        [Tooltip("Time in seconds that this Power Up will be active.")]
        [SerializeField] private float m_DurationTime = 5f;

        [Header("Damage Reduction")]
        [Tooltip("Percentage of damage reduction [0 , 1].")]
        [SerializeField] private float m_DamageReduction = 0.5f;

        [Header("Speed Bonus")]
        [Tooltip("Extra speed value of the tank.")]
        [SerializeField] private float m_SpeedBonus = 5f;
        [Tooltip("Extra turn speed value of the tank.")]
        [SerializeField] private float m_TurnSpeedBonus = 0f;

        [Header("Shooting Bonus")]
        [Tooltip("Percentage of reduction in the cooldown shooting time (0 , 1].")]
        [SerializeField] private float m_CooldownReduction = 0.5f;

        [Header("Healing")]
        [Tooltip("Life that will recover the tank.")]
        [SerializeField] private float m_HealingAmount = 20f;

        [Header("Extra Damage")]
        [Tooltip("Amount by which the damage will be multiplied.")]
        [SerializeField] private float m_DamageMultiplier = 2f;

        private PowerUpSpawner m_Spawner;               // Reference to the spawner that instantiated this PowerUp

        private void Update()
        {
            // Rotates the power up game object
            transform.rotation = Quaternion.Euler(0, 50f * Time.time, 0);
        }


        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer != LayerMask.NameToLayer("Players"))
                return;

            // Reference to the PowerUpDetector component of the tank.
            PowerUpDetector detector = other.gameObject.GetComponent<PowerUpDetector>();
            if (detector == null)
                return;

            bool isOnline = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

            // [ONLINE] Chỉ server xử lý việc nhặt để áp hiệu ứng theo authority. Client bỏ qua trigger
            // cục bộ, tránh áp hiệu ứng vào bản tank không có quyền (đó chính là lý do trước đây nhặt
            // xong không có tác dụng gì).
            if (isOnline && !NetworkManager.Singleton.IsServer)
                return;

            // Checks that the tank has not picked up other power up
            if (detector.m_HasActivePowerUp)
                return;

            if (isOnline)
            {
                // Server broadcast để MỌI máy áp hiệu ứng cho bản tank cục bộ của mình.
                var tankNetObj = other.gameObject.GetComponent<NetworkObject>();
                var gameManager = FindAnyObjectByType<GameManager>();
                if (tankNetObj != null && gameManager != null)
                {
                    GetNetworkedValues(out float value1, out float value2);
                    gameManager.ApplyPowerUpToTank(tankNetObj, (int)m_PowerUpType, value1, value2, m_DurationTime);
                }
            }
            else
            {
                ApplyOffline(detector);
            }

            // Tells the spawner that the power up has been collected
            if (m_Spawner != null)
                m_Spawner.CollectPowerUp();

            // Instantiates the PowerUp effects
            if (m_CollectFX != null)
                Instantiate(m_CollectFX, transform.position, Quaternion.identity);

            // Destroys the Power Up (bản cục bộ; các máy khác bị hủy qua broadcast respawn)
            Destroy(gameObject);
        }

        // Áp hiệu ứng trực tiếp (chế độ offline, tank cục bộ luôn là bản có authority).
        private void ApplyOffline(PowerUpDetector detector)
        {
            switch (m_PowerUpType)
            {
                case PowerUpType.DamageReduction:
                    detector.PickUpShield(m_DamageReduction, m_DurationTime);
                    break;
                case PowerUpType.Speed:
                    detector.PowerUpSpeed(m_SpeedBonus, m_TurnSpeedBonus, m_DurationTime);
                    break;
                case PowerUpType.ShootingBonus:
                    detector.PowerUpShoootingRate(m_CooldownReduction, m_DurationTime);
                    break;
                case PowerUpType.Healing:
                    detector.PowerUpHealing(m_HealingAmount);
                    break;
                case PowerUpType.Invincibility:
                    detector.PowerUpInvincibility(m_DurationTime);
                    break;
                case PowerUpType.DamageMultiplier:
                    detector.PowerUpSpecialShell(m_DamageMultiplier);
                    break;
            }
        }

        // Gom 2 giá trị cần gửi qua mạng theo từng loại power-up (khớp với ApplyNetworkedPowerUp).
        private void GetNetworkedValues(out float value1, out float value2)
        {
            value1 = 0f;
            value2 = 0f;
            switch (m_PowerUpType)
            {
                case PowerUpType.Speed:
                    value1 = m_SpeedBonus;
                    value2 = m_TurnSpeedBonus;
                    break;
                case PowerUpType.DamageReduction:
                    value1 = m_DamageReduction;
                    break;
                case PowerUpType.ShootingBonus:
                    value1 = m_CooldownReduction;
                    break;
                case PowerUpType.Healing:
                    value1 = m_HealingAmount;
                    break;
                case PowerUpType.DamageMultiplier:
                    value1 = m_DamageMultiplier;
                    break;
                case PowerUpType.Invincibility:
                    break;
            }
        }

        // Sets m_Spawner
        public void SetSpawner(PowerUpSpawner spawner)
        {
            m_Spawner = spawner;
        }
    }
}
