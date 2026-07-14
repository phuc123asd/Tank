using UnityEngine;

namespace Tanks.Complete
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class ScenePhysicsProfile : MonoBehaviour
    {
        private static readonly Vector3 k_DefaultGravity = new Vector3(0f, -9.81f, 0f);

        [SerializeField] private Vector3 m_Gravity = new Vector3(0f, -9.81f, 0f);

        public Vector3 Gravity => m_Gravity;

        private void Awake()
        {
            Physics.gravity = m_Gravity;
        }

        private void OnDestroy()
        {
            // Mọi scene gameplay đều khai báo profile riêng. Trả về mặc định khi rời scene
            // để gravity của Moon không rò sang menu hoặc scene được tải sau đó.
            Physics.gravity = k_DefaultGravity;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_Gravity.y > -0.01f)
                m_Gravity.y = -0.01f;
        }
#endif
    }
}
