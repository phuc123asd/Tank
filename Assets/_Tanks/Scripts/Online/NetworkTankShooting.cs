using Unity.Netcode;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Bridges the reused <see cref="TankShooting"/> (which computes charge, cooldown, audio and the shot
    /// itself, all locally on the owner) to the network (Slice 2). Only the owner subscribes: when its
    /// TankShooting fires, we forward the shot to the server via a ServerRpc, and the server spawns the
    /// authoritative <see cref="NetworkShell"/>. Non-owners have TankShooting disabled (see NetworkTankSetup),
    /// so they never raise the event.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkTankShooting : NetworkBehaviour
    {
        [Tooltip("The server-authoritative shell prefab (must be registered in the NetworkManager prefab list).")]
        public GameObject m_NetworkShellPrefab;

        private TankShooting m_Shooting;

        public override void OnNetworkSpawn()
        {
            m_Shooting = GetComponent<TankShooting>();
            if (IsOwner && m_Shooting != null)
                m_Shooting.OnNetworkFire += HandleFire;
        }

        public override void OnNetworkDespawn()
        {
            if (m_Shooting != null)
                m_Shooting.OnNetworkFire -= HandleFire;
        }

        // Raised on the owner by TankShooting.Fire().
        private void HandleFire(Vector3 pos, Quaternion rot, Vector3 velocity, float damage, float force, float radius)
        {
            FireServerRpc(pos, rot, velocity, damage, force, radius);
        }

        [ServerRpc]
        private void FireServerRpc(Vector3 pos, Quaternion rot, Vector3 velocity, float damage, float force, float radius)
        {
            if (m_NetworkShellPrefab == null)
            {
                Debug.LogError("[NetworkTankShooting] m_NetworkShellPrefab is not assigned.");
                return;
            }

            var shellGo = Instantiate(m_NetworkShellPrefab, pos, rot);

            var rb = shellGo.GetComponent<Rigidbody>();
            if (rb != null)
                rb.linearVelocity = velocity;

            var shell = shellGo.GetComponent<NetworkShell>();
            if (shell != null)
            {
                shell.m_MaxDamage = damage;
                shell.m_ExplosionForce = force;
                shell.m_ExplosionRadius = radius;
            }

            var netObj = shellGo.GetComponent<NetworkObject>();
            netObj.Spawn(true);
        }
    }
}
