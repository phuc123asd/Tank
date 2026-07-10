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

        private bool m_Exploded;

        public static void PlayLaunchEffectFromShellPrefab(Rigidbody shellPrefab, Transform fireTransform)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;
            if (shellPrefab == null || fireTransform == null) return;

            ShellExplosion shellExplosion = shellPrefab.GetComponent<ShellExplosion>();
            if (shellExplosion == null || shellExplosion.m_ExplosionParticles == null) return;

            PlayParticleClone(shellExplosion.m_ExplosionParticles, fireTransform.position, fireTransform.rotation, 0.45f);
        }

        private void Start ()
        {
            // If it isn't destroyed by then, destroy the shell after its lifetime.
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                Invoke(nameof(ExplodeFromLifetime), m_MaxLifeTime);
            else
                Destroy (gameObject, m_MaxLifeTime);
        }


        private void OnTriggerEnter (Collider other)
        {
            Explode();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                Explode();
        }

        private void ExplodeFromLifetime()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                Explode();
        }

        private void Explode()
        {
            Collider[] colliders = Physics.OverlapSphere (transform.position, m_ExplosionRadius, m_TankMask);
            bool isOffline = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
            if (m_Exploded) return;
            m_Exploded = true;

            if (isOffline || NetworkManager.Singleton.IsServer)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    Rigidbody targetRigidbody = colliders[i].GetComponent<Rigidbody> ();
                    if (!targetRigidbody)
                        continue;

                    float damage = CalculateDamage (targetRigidbody.position);
                    TankMovement targetMovement = targetRigidbody.GetComponent<TankMovement>();
                    if (targetMovement != null)
                        targetMovement.AddExplosionForce(m_ExplosionForce, transform.position, m_ExplosionRadius);

                    if (isOffline)
                    {
                        OfflineTankHealthController offlineHealth = targetRigidbody.GetComponent<OfflineTankHealthController>();
                        if (offlineHealth != null)
                        {
                            offlineHealth.TakeDamage(damage);
                            continue;
                        }
                    }

                    TankHealth targetHealth = targetRigidbody.GetComponent<TankHealth> ();
                    if (targetHealth != null)
                        targetHealth.TakeDamage (damage);
                }
            }

            if (isOffline)
                PlayOfflineExplosionPrefabEffect();
            else
                PlayOnlineExplosionEffect();

            if (CameraControl.Instance != null)
                CameraControl.Instance.Shake(0.22f, 0.35f);

            if (isOffline)
            {
                Destroy(gameObject);
            }
            else if (NetworkManager.Singleton.IsServer)
            {
                NetworkObject networkObject = GetComponent<NetworkObject>();
                if (networkObject != null)
                    networkObject.Despawn();
                else
                    Destroy(gameObject);
            }
        }

        private void PlayOfflineExplosionPrefabEffect()
        {
            ParticleSystem particles = null;

            if (m_ExplosionParticles != null)
            {
                particles = PlayParticleClone(m_ExplosionParticles, transform.position, m_ExplosionParticles.transform.rotation, 1f);
            }

            if (m_ExplosionAudio != null)
            {
                AudioSource audio = Instantiate(m_ExplosionAudio, transform.position, Quaternion.identity);
                audio.gameObject.SetActive(true);
                audio.Play();
                Destroy(audio.gameObject, Mathf.Max(0.2f, audio.clip != null ? audio.clip.length : 1f));
            }

            if (particles != null)
            {
                ParticleSystem.MainModule mainModule = particles.main;
                Destroy(particles.gameObject, Mathf.Max(mainModule.duration, mainModule.startLifetime.constantMax) + 0.25f);
            }
        }

        private static ParticleSystem PlayParticleClone(ParticleSystem source, Vector3 position, Quaternion rotation, float scaleMultiplier)
        {
            ParticleSystem particles = Instantiate(source, position, rotation);
            particles.transform.localScale *= Mathf.Max(0.01f, scaleMultiplier);
            particles.gameObject.SetActive(true);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(true);
            return particles;
        }

        private void PlayOnlineExplosionEffect()
        {
            if (m_ExplosionParticles != null)
            {
                m_ExplosionParticles.transform.parent = null;
                m_ExplosionParticles.Play();

                ParticleSystem.MainModule mainModule = m_ExplosionParticles.main;
                Destroy(m_ExplosionParticles.gameObject, mainModule.duration);
            }

            if (m_ExplosionAudio != null)
                m_ExplosionAudio.Play();
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
