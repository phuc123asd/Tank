using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Tanks.Complete
{
    public class PowerUpSpawner : NetworkBehaviour
    {
        [Tooltip("Array that holds different power-up prefabs that can be spawned.")]
        public PowerUp[] m_PowerUps;
        [Tooltip("Time in seconds that will wait this spawner to instantiate a new power up when collected the new one.")]
        public float m_RespawnCooldown = 20f;

        private readonly NetworkVariable<int> m_SelectedPowerUpIndex = new(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private PowerUp m_SpawnedPowerUp;
        private bool m_IsRespawning;

        public override void OnNetworkSpawn()
        {
            m_SelectedPowerUpIndex.OnValueChanged += OnSelectedPowerUpChanged;

            // A late-joining client must immediately display the server's current item.
            ApplySelectedPowerUp(m_SelectedPowerUpIndex.Value);

            // Only the server is allowed to choose the random item.
            if (IsServer && m_SelectedPowerUpIndex.Value < 0)
                SelectRandomPowerUp();
        }

        public override void OnNetworkDespawn()
        {
            m_SelectedPowerUpIndex.OnValueChanged -= OnSelectedPowerUpChanged;
            DestroySpawnedPowerUp();
        }

        public void CollectPowerUp()
        {
            if (IsServer)
                BeginRespawn();
            else
                CollectPowerUpServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void CollectPowerUpServerRpc()
        {
            BeginRespawn();
        }

        private void BeginRespawn()
        {
            if (m_IsRespawning)
                return;

            m_IsRespawning = true;
            m_SelectedPowerUpIndex.Value = -1;
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
            if (!IsServer || m_PowerUps == null || m_PowerUps.Length == 0)
                return;

            m_SelectedPowerUpIndex.Value = Random.Range(0, m_PowerUps.Length);
        }

        private void OnSelectedPowerUpChanged(int previousIndex, int currentIndex)
        {
            ApplySelectedPowerUp(currentIndex);
        }

        private void ApplySelectedPowerUp(int selectedIndex)
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
    }
}
