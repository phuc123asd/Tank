using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Server-authoritative shell used online (Slice 2). The server owns the physics and is the only side
    /// that detects impact; a server-auth <c>NetworkTransform</c> replicates the flight to every client. On
    /// impact (or lifetime timeout) the server despawns the shell and fires a ClientRpc so every machine
    /// plays the explosion VFX + camera shake at the same spot.
    ///
    /// Damage is intentionally NOT applied here yet — that arrives in Slice 3 together with networked health.
    /// The offline shell (ShellExplosion) is untouched; this component only lives on the NetworkShell prefab
    /// (which has ShellExplosion removed so the two never both handle a hit).
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkShell : NetworkBehaviour
    {
        [Tooltip("Seconds before an un-hit shell removes itself.")]
        public float m_MaxLifeTime = 2f;

        [Tooltip("Standalone explosion VFX prefab (particles + audio) spawned locally on every client.")]
        public GameObject m_ExplosionPrefab;

        // Filled in by the server from the firing tank; only the server uses them (for future damage).
        [HideInInspector] public float m_MaxDamage = 100f;
        [HideInInspector] public float m_ExplosionForce = 50f;
        [HideInInspector] public float m_ExplosionRadius = 5f;

        private bool m_Exploded;

        public override void OnNetworkSpawn()
        {
            // Only the server simulates the shell; clients follow the replicated transform.
            var rb = GetComponent<Rigidbody>();
            if (rb != null && !IsServer)
                rb.isKinematic = true;

            if (IsServer)
                Invoke(nameof(ServerTimeout), m_MaxLifeTime);
        }

        private void ServerTimeout()
        {
            if (!m_Exploded)
                Explode();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer || m_Exploded)
                return;

            Explode();
        }

        // Server only.
        private void Explode()
        {
            m_Exploded = true;
            CancelInvoke(nameof(ServerTimeout));

            // Server-authoritative area damage: closer tanks take more, up to m_MaxDamage at the centre.
            Collider[] hits = Physics.OverlapSphere(transform.position, m_ExplosionRadius);
            var alreadyHit = new HashSet<NetworkTankHealth>();
            foreach (var col in hits)
            {
                var health = col.GetComponentInParent<NetworkTankHealth>();
                if (health == null || !alreadyHit.Add(health))
                    continue;

                float distance = Vector3.Distance(transform.position, health.transform.position);
                float relative = (m_ExplosionRadius - distance) / m_ExplosionRadius;
                float damage = Mathf.Max(0f, relative * m_MaxDamage);
                if (damage > 0f)
                    health.ServerApplyDamage(damage);
            }

            // Tell everyone (host included, since the host is also a client) to play the explosion locally.
            ExplodeClientRpc(transform.position);

            if (NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn(true);
        }

        [ClientRpc]
        private void ExplodeClientRpc(Vector3 position)
        {
            if (m_ExplosionPrefab != null)
            {
                var vfx = Instantiate(m_ExplosionPrefab, position, Quaternion.identity);

                var ps = vfx.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ps.Play();
                    var main = ps.main;
                    Destroy(vfx, main.duration + main.startLifetime.constantMax);
                }
                else
                {
                    Destroy(vfx, 2f);
                }

                var audio = vfx.GetComponent<AudioSource>();
                if (audio != null)
                    audio.Play();
            }

            if (CameraControl.Instance != null)
                CameraControl.Instance.Shake(0.22f, 0.35f);
        }
    }
}
