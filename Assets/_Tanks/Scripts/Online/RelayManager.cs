using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Thin wrapper over Unity Relay. The host allocates a relay server and gets a short join code;
    /// clients use that code to join the same relay. In both cases the NGO <see cref="UnityTransport"/>
    /// is pointed at the relay so all traffic is routed through it (works across different networks
    /// AND on the same LAN).
    ///
    /// Requires Relay to be enabled for the project's environment on the Unity Dashboard.
    /// </summary>
    public static class RelayManager
    {
        // "dtls" = encrypted UDP. "udp" is also possible but dtls is the recommended default.
        private const string ConnectionType = "dtls";

        /// <summary>
        /// Host side: create a relay allocation for up to <paramref name="maxConnections"/> clients
        /// (excluding the host), bind the transport to it, and return the join code clients need.
        /// </summary>
        public static async Task<string> CreateRelayAsync(int maxConnections)
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(new RelayServerData(allocation, ConnectionType));

            Debug.Log($"[Relay] Allocation created. Join code = {joinCode}");
            return joinCode;
        }

        /// <summary>
        /// Client side: join an existing relay by <paramref name="joinCode"/> and bind the transport to it.
        /// </summary>
        public static async Task JoinRelayAsync(string joinCode)
        {
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(new RelayServerData(allocation, ConnectionType));

            Debug.Log($"[Relay] Joined allocation with code {joinCode}.");
        }
    }
}
