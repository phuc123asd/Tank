using UnityEngine;
using Unity.Netcode;

namespace Tanks.Complete
{
    public class ShellExplosion : MonoBehaviour
    {
        public LayerMask m_TankMask;                        // Used to filter what the explosion affects, this should be set to "Players".
        public ParticleSystem m_ExplosionParticles;         // Reference to the particles that will play on explosion.
        public AudioSource m_ExplosionAudio;                // Reference to the audio that will play on explosion.
        [HideInInspector] public float m_MaxLifeTime = 2f;  // The time in seconds before the shell is removed.

        // All those are hidden in inspector as they will actually come from the TankShooting scripts
        [HideInInspector] public float m_MaxDamage = 100f;                    // The amount of damage done if the explosion is centred on a tank.
        [HideInInspector] public float m_ExplosionForce = 50f;                // The amount of force added to a tank at the centre of the explosion.
        [HideInInspector] public float m_ExplosionRadius = 5f;                // The maximum distance away from the explosion tanks can be and are still affected.


        private void Start ()
        {
            // If it isn't destroyed by then, destroy the shell after its lifetime.
            Destroy (gameObject, m_MaxLifeTime);
        }


        private void OnTriggerEnter (Collider other)
        {
            Collider[] colliders = Physics.OverlapSphere (transform.position, m_ExplosionRadius, m_TankMask);
            bool isOffline = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;

            if (isOffline || NetworkManager.Singleton.IsServer)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    Rigidbody targetRigidbody = colliders[i].GetComponent<Rigidbody> ();
                    if (!targetRigidbody)
                        continue;

                    targetRigidbody.GetComponent<TankMovement>().AddExplosionForce(m_ExplosionForce, transform.position, m_ExplosionRadius);

                    TankHealth targetHealth = targetRigidbody.GetComponent<TankHealth> ();
                    if (!targetHealth)
                        continue;

                    float damage = CalculateDamage (targetRigidbody.position);
                    targetHealth.TakeDamage (damage);
                }
            }

            m_ExplosionParticles.transform.parent = null;
            m_ExplosionParticles.Play();
            m_ExplosionAudio.Play();

            if (CameraControl.Instance != null)
                CameraControl.Instance.Shake(0.22f, 0.35f);

            ParticleSystem.MainModule mainModule = m_ExplosionParticles.main;
            Destroy (m_ExplosionParticles.gameObject, mainModule.duration);

            if (isOffline)
            {
                Destroy(gameObject);
            }
            else if (NetworkManager.Singleton.IsServer)
            {
                GetComponent<NetworkObject>().Despawn();
            }
        }


        private float CalculateDamage (Vector3 targetPosition)
        {
            // Create a vector from the shell to the target.
            Vector3 explosionToTarget = targetPosition - transform.position;

            // Calculate the distance from the shell to the target.
            float explosionDistance = explosionToTarget.magnitude;

            // Calculate the proportion of the maximum distance (the explosionRadius) the target is away.
            float relativeDistance = (m_ExplosionRadius - explosionDistance) / m_ExplosionRadius;

            // Calculate damage as this proportion of the maximum possible damage.
            float damage = relativeDistance * m_MaxDamage;

            // Make sure that the minimum damage is always 0.
            damage = Mathf.Max (0f, damage);

            return damage;
        }
    }
}