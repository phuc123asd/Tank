using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Tanks.Complete
{
    public class PowerUpSpawner : MonoBehaviour
    {
        [Tooltip("Array that holds different power-up prefabs that can be spawned.")]
        public PowerUp[] m_PowerUps;
        [Tooltip("Time in seconds that will wait this spawner to instantiate a new power up when collected the new one.")]
        public float m_RespawnCooldown = 20f;

        private PowerUp m_SpawnedPowerUp;
        private bool m_IsRespawning;

        private void Start()
        {
            if (!IsOnlineMatch() || NetworkManager.Singleton.IsServer)
                SelectRandomPowerUp();
        }

        public void CollectPowerUp()
        {
            if (!IsOnlineMatch())
                BeginRespawn();
            else
                FindAnyObjectByType<GameManager>()?.RequestPowerUpRespawn(transform.position);
        }

        public void BeginRespawn()
        {
            if (m_IsRespawning)
                return;

            m_IsRespawning = true;
            BroadcastSelection(-1);
            StartCoroutine(RespawnPowerUp());
        }

        private IEnumerator RespawnPowerUp()
        {
            yield return new WaitForSeconds(m_RespawnCooldown);
            m_IsRespawning = false;
            SelectRandomPowerUp();
        }

        private void SelectRandomPowerUp()
        {
            if (m_PowerUps == null || m_PowerUps.Length == 0)
                return;

            BroadcastSelection(Random.Range(0, m_PowerUps.Length));
        }

        private void BroadcastSelection(int selectedIndex)
        {
            if (!IsOnlineMatch())
                ApplySelectedPowerUp(selectedIndex);
            else if (NetworkManager.Singleton.IsServer)
                FindAnyObjectByType<GameManager>()?.SyncPowerUpSelection(transform.position, selectedIndex);
        }

        public void ApplySelectedPowerUp(int selectedIndex)
        {
            DestroySpawnedPowerUp();

            if (selectedIndex < 0 || m_PowerUps == null || selectedIndex >= m_PowerUps.Length)
                return;

            Vector3 positionToSpawn = transform.position;
            positionToSpawn.y = 1.09f;
            m_SpawnedPowerUp = Instantiate(m_PowerUps[selectedIndex], positionToSpawn, Quaternion.identity);
            m_SpawnedPowerUp.SetSpawner(this);
        }

        private void DestroySpawnedPowerUp()
        {
            if (m_SpawnedPowerUp == null)
                return;

            Destroy(m_SpawnedPowerUp.gameObject);
            m_SpawnedPowerUp = null;
        }

        private static bool IsOnlineMatch()
        {
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        }
    }
}
