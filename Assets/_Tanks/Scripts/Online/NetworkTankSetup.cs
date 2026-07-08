using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Slice 1 of the online gameplay conversion (see plan zazzy-questing-quiche). Sits on the ONLINE
    /// tank prefab variant (which also carries a <see cref="NetworkObject"/> and
    /// <see cref="ClientNetworkTransform"/>). It reuses the shipped offline gameplay scripts unchanged:
    ///
    ///  - The OWNER runs <see cref="TankMovement"/> locally (real physics) and the owner-authoritative
    ///    <see cref="ClientNetworkTransform"/> replicates its transform to peers.
    ///  - NON-owners get the control script disabled, so the remote tank is only moved by the network
    ///    transform (its Rigidbody goes kinematic via TankMovement.OnDisable) and never reads local input.
    ///
    /// Shooting stays disabled here — it is routed through the network in Slice 2. The offline path is
    /// untouched: this component only exists on the online prefab. Colour is derived from the deterministic
    /// <see cref="NetworkObject.OwnerClientId"/> so every machine tints each tank the same way.
    ///
    /// Because the player prefab may auto-spawn before the networked arena scene has finished loading,
    /// the owner-only setup (snap to a spawn point + point the local camera at this tank) is deferred to a
    /// coroutine that waits for the arena objects to appear.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkTankSetup : NetworkBehaviour
    {
        // Player colours, indexed by owner client id. Mirrors the classic Tanks! blue/red palette.
        private static readonly Color[] s_PlayerColors =
        {
            new Color(0.10f, 0.45f, 0.90f), // P1 blue
            new Color(0.90f, 0.25f, 0.20f), // P2 red
            new Color(0.20f, 0.75f, 0.35f), // P3 green
            new Color(0.95f, 0.80f, 0.20f), // P4 yellow
        };

        [Tooltip("How long the owner waits for the arena scene (spawn points + camera) before giving up.")]
        public float m_ArenaWaitTimeout = 15f;

        private TankMovement m_Movement;
        private TankShooting m_Shooting;

        public override void OnNetworkSpawn()
        {
            m_Movement = GetComponent<TankMovement>();
            m_Shooting = GetComponent<TankShooting>();

            // Always human-driven online; make sure nothing left these flagged as bots.
            if (m_Movement != null)
                m_Movement.m_IsComputerControlled = false;
            if (m_Shooting != null)
                m_Shooting.m_IsComputerControlled = false;

            ApplyColor();

            if (IsOwner)
            {
                // Owner drives its own tank with the left-keyboard scheme (TankMovement.Start wires this
                // up from ControlIndex when there is no GameManager). TankShooting also runs locally on the
                // owner; NetworkTankShooting forwards each shot to the server (Slice 2).
                if (m_Movement != null)
                {
                    m_Movement.ControlIndex = 1;
                    m_Movement.m_PlayerNumber = 1;
                    m_Movement.enabled = true;
                }
                if (m_Shooting != null)
                    m_Shooting.enabled = true;

                StartCoroutine(OwnerArenaSetup());
            }
            else
            {
                // Remote tanks are moved only by the network transform and never shoot locally; kill both
                // control scripts so they don't read this machine's input or fight the replicated position.
                if (m_Movement != null)
                    m_Movement.enabled = false;
                if (m_Shooting != null)
                    m_Shooting.enabled = false;
            }
        }

        // Tint the tank's coloured materials (same convention TankManager uses: material name contains "TankColor").
        private void ApplyColor()
        {
            Color color = s_PlayerColors[(int)(OwnerClientId % (ulong)s_PlayerColors.Length)];

            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                var materials = renderer.materials;
                for (int j = 0; j < materials.Length; ++j)
                {
                    if (materials[j].name.Contains("TankColor"))
                        materials[j].color = color;
                }
            }
        }

        // Wait until the arena scene is live (its camera rig + spawn points exist), then place this tank at
        // its spawn point and make the local camera follow it. Owner-authoritative, so setting our own
        // transform replicates to everyone.
        private IEnumerator OwnerArenaSetup()
        {
            float elapsed = 0f;
            Transform spawnRoot = null;

            while (elapsed < m_ArenaWaitTimeout)
            {
                if (spawnRoot == null)
                {
                    var go = GameObject.Find("SpawnPoints");
                    if (go != null)
                        spawnRoot = go.transform;
                }

                if (spawnRoot != null && CameraControl.Instance != null)
                    break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            MoveOwnerToSpawn();

            var cam = CameraControl.Instance;
            if (cam != null)
            {
                cam.m_Targets = new[] { transform };
                cam.SetStartPositionAndSize();
            }
        }

        /// <summary>
        /// Owner-only: snap this tank to its spawn point (by owner id). Reused by the round manager on
        /// respawn. Owner-authoritative transform means setting our own position replicates to everyone.
        /// </summary>
        public void MoveOwnerToSpawn()
        {
            if (!IsOwner)
                return;

            var go = GameObject.Find("SpawnPoints");
            if (go == null || go.transform.childCount == 0)
                return;

            int index = (int)(OwnerClientId % (ulong)go.transform.childCount);
            Transform spawn = go.transform.GetChild(index);
            SnapTo(spawn.position, spawn.rotation);
        }

        private void SnapTo(Vector3 position, Quaternion rotation)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = position;
                rb.rotation = rotation;
                // Velocities can only be cleared on a non-kinematic body (a frozen/dead tank is kinematic).
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
