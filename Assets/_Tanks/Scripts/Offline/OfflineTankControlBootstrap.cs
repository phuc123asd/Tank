using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tanks.Complete
{
    /// <summary>
    /// Installs the offline-only tank controller without touching the online movement/shooting scripts or prefabs.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class OfflineTankControlBootstrap : MonoBehaviour
    {
        private static OfflineTankControlBootstrap s_Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (s_Instance != null) return;

            var go = new GameObject(nameof(OfflineTankControlBootstrap));
            DontDestroyOnLoad(go);
            s_Instance = go.AddComponent<OfflineTankControlBootstrap>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InstallOfflineControllers();
        }

        private void LateUpdate()
        {
            InstallOfflineControllers();
        }

        private void InstallOfflineControllers()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                return;

            var movements = FindObjectsByType<TankMovement>(FindObjectsInactive.Include);
            foreach (var movement in movements)
            {
                if (movement == null || movement.IsSpawned)
                    continue;

                bool isGameplayTank = movement.ControlIndex > 0 || movement.m_IsComputerControlled;
                if (!isGameplayTank)
                    continue;

                var health = movement.GetComponent<TankHealth>();
                if (health != null)
                {
                    var offlineHealth = movement.GetComponent<OfflineTankHealthController>();
                    if (offlineHealth == null)
                        offlineHealth = movement.gameObject.AddComponent<OfflineTankHealthController>();

                    if (!offlineHealth.IsConfiguredFor(health))
                        offlineHealth.ConfigureFrom(health);
                }

                if (movement.m_IsComputerControlled || movement.ControlIndex <= 0)
                    continue;

                var shooting = movement.GetComponent<TankShooting>();
                if (shooting == null || shooting.m_IsComputerControlled)
                    continue;

                var controller = movement.GetComponent<OfflineTankController>();
                if (controller == null)
                    controller = movement.gameObject.AddComponent<OfflineTankController>();

                int controlIndex = movement.ControlIndex > 0 ? movement.ControlIndex : movement.m_PlayerNumber;
                if (!controller.IsConfiguredFor(movement, shooting, controlIndex))
                    controller.ConfigureFrom(movement, shooting, controlIndex);
            }
        }
    }
}
