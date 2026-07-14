using System.Collections.Generic;
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
        [SerializeField] private float m_SweepRadius = 0.22f;

        private bool m_Exploded;
        private bool m_HasSourceClient;
        private ulong m_SourceClientId;
        private Collider m_ShellCollider;
        private Vector3 m_LastSweepPosition;

        // Chỉ server cần nguồn bắn để lọc friendly fire; VFX client không phụ thuộc dữ liệu này.
        public void InitializeNetworkShot(ulong sourceClientId)
        {
            m_SourceClientId = sourceClientId;
            m_HasSourceClient = true;
        }

        public static void PlayLaunchEffectFromShellPrefab(Rigidbody shellPrefab, Transform fireTransform)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;
            if (shellPrefab == null || fireTransform == null) return;

            ShellExplosion shellExplosion = shellPrefab.GetComponent<ShellExplosion>();
            if (shellExplosion == null || shellExplosion.m_ExplosionParticles == null) return;

            PlayParticleClone(shellExplosion.m_ExplosionParticles, fireTransform.position, fireTransform.rotation, 0.45f);
        }

        private void Awake()
        {
            m_ShellCollider = GetComponent<Collider>();
            if (m_ShellCollider is CapsuleCollider capsule)
                m_SweepRadius = Mathf.Max(m_SweepRadius, capsule.radius * MaxScale(transform.lossyScale));

            m_LastSweepPosition = transform.position;
        }

        private void Start ()
        {
            // Hết thời gian sống thì cho đạn nổ. Offline: mọi máy tự xử lý. Online: chỉ server đặt hẹn
            // (Explode sẽ despawn + broadcast VFX), tránh client tự Destroy NetworkObject của server.
            bool isOffline = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
            if (isOffline || NetworkManager.Singleton.IsServer)
                Invoke(nameof(ExplodeFromLifetime), m_MaxLifeTime);
        }

        private void FixedUpdate()
        {
            if (m_Exploded || !CanExplodeOnThisPeer())
            {
                m_LastSweepPosition = transform.position;
                return;
            }

            Vector3 currentPosition = transform.position;
            Vector3 travel = currentPosition - m_LastSweepPosition;
            float distance = travel.magnitude;

            if (distance > 0.001f && TryFindSweepHit(m_LastSweepPosition, travel / distance, distance, out RaycastHit hit))
            {
                transform.position = hit.point;
                Explode();
                return;
            }

            m_LastSweepPosition = currentPosition;
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
            // Explode() tự lọc authority (offline hoặc server). Client online sẽ không tới đây vì
            // không đặt hẹn ở Start.
            Explode();
        }

        private void Explode()
        {
            // [ONLINE] Chỉ server mới được kích nổ (tính damage + phát VFX đồng bộ). Client bỏ qua
            // trigger cục bộ để tránh nổ lệch vị trí / lệch thời điểm, và tránh despawn đạn của server.
            if (!CanExplodeOnThisPeer())
                return;

            if (m_Exploded) return;
            m_Exploded = true;

            bool isOffline = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
            GameManager gameManager = isOffline ? null : GetGameManager();
            var affectedBodies = new HashSet<Rigidbody>();
            Collider[] colliders = Physics.OverlapSphere (transform.position, m_ExplosionRadius, m_TankMask);
            for (int i = 0; i < colliders.Length; i++)
            {
                Rigidbody targetRigidbody = colliders[i].attachedRigidbody != null
                    ? colliders[i].attachedRigidbody
                    : colliders[i].GetComponent<Rigidbody>();
                if (!targetRigidbody || !affectedBodies.Add(targetRigidbody))
                    continue;

                if (!isOffline && m_HasSourceClient && gameManager != null)
                {
                    NetworkObject targetObject = targetRigidbody.GetComponentInParent<NetworkObject>();
                    if (targetObject != null &&
                        gameManager.ShouldIgnoreFriendlyFire(m_SourceClientId, targetObject.OwnerClientId))
                    {
                        // Tắt cả damage và lực hất để đồng đội không thể grief nhau.
                        continue;
                    }
                }

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

            if (isOffline)
            {
                PlayOfflineExplosionPrefabEffect();
            }
            else
            {
                // Host phát VFX cục bộ ngay, rồi bảo các client còn lại phát trước khi đạn despawn.
                PlayNetworkedExplosionEffect(transform.position);

                NetworkObject networkObject = GetComponent<NetworkObject>();
                if (gameManager != null && networkObject != null)
                    gameManager.PlayShellExplosion(networkObject.NetworkObjectId, transform.position);
            }

            if (CameraControl.Instance != null)
                CameraControl.Instance.Shake(0.22f, 0.35f);

            if (isOffline)
            {
                Destroy(gameObject);
            }
            else
            {
                NetworkObject networkObject = GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned)
                    networkObject.Despawn();
                else
                    Destroy(gameObject);
            }
        }

        private bool CanExplodeOnThisPeer()
        {
            return NetworkManager.Singleton == null ||
                   !NetworkManager.Singleton.IsListening ||
                   NetworkManager.Singleton.IsServer;
        }

        private bool TryFindSweepHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit bestHit)
        {
            bestHit = default;
            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                m_SweepRadius,
                direction,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.PositiveInfinity;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider == m_ShellCollider || hitCollider.transform.IsChildOf(transform))
                    continue;

                if (hits[i].distance >= bestDistance)
                    continue;

                bestDistance = hits[i].distance;
                bestHit = hits[i];
                found = true;
            }

            return found;
        }

        private static float MaxScale(Vector3 scale)
        {
            return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }

        private static GameManager s_GameManager;
        private static GameManager GetGameManager()
        {
            // == null cũng đúng cho object Unity đã bị destroy -> tự tìm lại sau khi đổi scene.
            if (s_GameManager == null)
                s_GameManager = FindAnyObjectByType<GameManager>();
            return s_GameManager;
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

        // Phát VFX + âm thanh nổ tại vị trí server tính. Tách khỏi đạn (unparent) để không bị hủy
        // theo đạn khi server despawn. Gọi trên MỌI máy (host cục bộ + client qua ClientRpc).
        public void PlayNetworkedExplosionEffect(Vector3 position)
        {
            float life = 1f;

            if (m_ExplosionParticles != null)
            {
                ParticleSystem.MainModule mainModule = m_ExplosionParticles.main;
                life = Mathf.Max(life, mainModule.duration + mainModule.startLifetime.constantMax);
            }
            if (m_ExplosionAudio != null && m_ExplosionAudio.clip != null)
                life = Mathf.Max(life, m_ExplosionAudio.clip.length);

            if (m_ExplosionParticles != null)
            {
                m_ExplosionParticles.transform.parent = null;
                m_ExplosionParticles.transform.position = position;
                m_ExplosionParticles.Play();
                Destroy(m_ExplosionParticles.gameObject, life);
            }

            if (m_ExplosionAudio != null)
            {
                m_ExplosionAudio.transform.parent = null;
                m_ExplosionAudio.transform.position = position;
                m_ExplosionAudio.Play();
                // Nếu audio nằm chung GameObject với particle thì đã được hủy ở trên; chỉ hủy khi khác object.
                if (m_ExplosionParticles == null || m_ExplosionAudio.gameObject != m_ExplosionParticles.gameObject)
                    Destroy(m_ExplosionAudio.gameObject, life);
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
