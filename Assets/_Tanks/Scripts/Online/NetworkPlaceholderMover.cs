using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Tanks.Complete
{
    /// <summary>
    /// Minimal placeholder used to validate the Lobby + Relay + Netcode pipeline (Phase 2): each
    /// connected player spawns one of these; the owner drives it with WASD/arrows and the
    /// <see cref="ClientNetworkTransform"/> syncs it to all peers. Owner capsule is tinted green,
    /// remote ones red, so it is obvious which is which on each machine.
    /// </summary>
    public class NetworkPlaceholderMover : NetworkBehaviour
    {
        public float m_Speed = 6f;

        public override void OnNetworkSpawn()
        {
            var rend = GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = IsOwner ? new Color(0.3f, 0.8f, 0.4f) : new Color(0.85f, 0.35f, 0.35f);
        }

        private void Update()
        {
            if (!IsOwner)
                return;

            var kb = Keyboard.current;
            if (kb == null)
                return;

            Vector3 dir = Vector3.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.z += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.z -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1f;

            if (dir.sqrMagnitude > 0.001f)
                transform.position += dir.normalized * (m_Speed * Time.deltaTime);
        }
    }
}
