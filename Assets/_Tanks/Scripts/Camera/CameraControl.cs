using UnityEngine;

namespace Tanks.Complete
{
    public class CameraControl : MonoBehaviour
    {
        public float m_DampTime = 0.2f;                 // Approximate time for the camera to refocus.
        public float m_ScreenEdgeBuffer = 4f;           // Space between the top/bottom most target and the screen edge.
        public float m_MinSize = 6.5f;                  // The smallest orthographic size the camera can be.
        public Transform[] m_Targets;                   // All the targets the camera needs to encompass.

        // Simple singleton so gameplay code (e.g. shell explosions) can trigger a screen shake without holding a reference.
        public static CameraControl Instance { get; private set; }

        private Camera m_Camera;                        // Used for referencing the camera.
        private float m_ZoomSpeed;                      // Reference speed for the smooth damping of the orthographic size.
        private Vector3 m_MoveVelocity;                 // Reference velocity for the smooth damping of the position.
        private Vector3 m_DesiredPosition;              // The position the camera is moving towards.

        private Vector3 m_AimToRig;                     // The offset to apply to the position so the child camera aim at the desired point

        private Vector3 m_CameraBaseLocalPos;           // The resting local position of the child camera, restored after a shake.
        private float m_ShakeTimeRemaining;             // How long the current shake still lasts.
        private float m_ShakeDuration;                  // The full duration of the current shake (used to fade it out).
        private float m_ShakeMagnitude;                 // The current shake strength.

        private void Awake ()
        {
            Instance = this;

            m_Camera = GetComponentInChildren<Camera> ();
            m_CameraBaseLocalPos = m_Camera.transform.localPosition;

            // plane in which the camera rig is in
            Plane p = new Plane(Vector3.up, transform.position);
            Ray r = new Ray(m_Camera.transform.position, m_Camera.transform.forward);
            p.Raycast(r, out float d );

            // This is where the camera aim on the rig plane
            var aimTArget = r.GetPoint(d);

            // User can set the camera in random position and rotation as a child of this object, so it won't aim at the
            // center of this, meaning placing this object at the desired position won't make the camera aim at that desired position.
            // This offset correct that so the camera actually aim at the desired position
            m_AimToRig = transform.position - aimTArget;
        }


        private void FixedUpdate ()
        {
            // Move the camera towards a desired position.
            Move ();

            // Change the size of the camera based.
            Zoom ();
        }


        private void Move ()
        {
            // Find the average position of the targets.
            FindAveragePosition ();

            
            // Smoothly transition to that position.
            transform.position = Vector3.SmoothDamp(transform.position, m_DesiredPosition + m_AimToRig, ref m_MoveVelocity, m_DampTime);
        }


        private void FindAveragePosition ()
        {
            Vector3 averagePos = new Vector3 ();
            int numTargets = 0;

            if (m_Targets == null)
                return;

            // Go through all the targets and add their positions together.
            for (int i = 0; i < m_Targets.Length; i++)
            {
                // If the target isn't active, go on to the next one.
                Transform target = m_Targets[i];
                if (target == null || !target.gameObject.activeSelf)
                    continue;

                // Add to the average and increment the number of targets in the average.
                averagePos += target.position;
                numTargets++;
            }

            // If there are targets divide the sum of the positions by the number of them to find the average.
            if (numTargets > 0)
                averagePos /= numTargets;

            // Keep the same y value.
            averagePos.y = transform.position.y;
            
            m_DesiredPosition = averagePos;
        }


        private void Zoom ()
        {
            // Find the required size based on the desired position and smoothly transition to that size.
            float requiredSize = FindRequiredSize();
            m_Camera.orthographicSize = Mathf.SmoothDamp (m_Camera.orthographicSize, requiredSize, ref m_ZoomSpeed, m_DampTime);
        }


        private float FindRequiredSize ()
        {
            // Find the position the camera rig is moving towards in its local space.
            Vector3 desiredLocalPos = m_Camera.transform.InverseTransformPoint(m_DesiredPosition);

            // Start the camera's size calculation at zero.
            float size = 0f;

            // Go through all the targets...
            if (m_Targets == null)
                return m_MinSize;

            for (int i = 0; i < m_Targets.Length; i++)
            {
                // ... and if they aren't active continue on to the next target.
                Transform target = m_Targets[i];
                if (target == null || !target.gameObject.activeSelf)
                    continue;

                // Otherwise, find the position of the target in the camera's local space.
                Vector3 targetLocalPos = m_Camera.transform.InverseTransformPoint(target.position);

                // Find the position of the target from the desired position of the camera's local space.
                Vector3 desiredPosToTarget = targetLocalPos - desiredLocalPos;

                // Choose the largest out of the current size and the distance of the tank 'up' or 'down' from the camera.
                size = Mathf.Max(size, Mathf.Abs(desiredPosToTarget.y));

                // Choose the largest out of the current size and the calculated size based on the tank being to the left or right of the camera.
                size = Mathf.Max(size, Mathf.Abs(desiredPosToTarget.x) / m_Camera.aspect);
            }

            // Add the edge buffer to the size.
            size += m_ScreenEdgeBuffer;

            // Make sure the camera's size isn't below the minimum.
            size = Mathf.Max (size, m_MinSize);

            return size;
        }


        public void SetStartPositionAndSize ()
        {
            // Find the desired position.
            FindAveragePosition ();

            // Set the camera's position to the desired position without damping.
            transform.position = m_DesiredPosition;

            // Find and set the required size of the camera.
            m_Camera.orthographicSize = FindRequiredSize ();
        }


        // Triggers a short screen shake. The strongest active shake wins so several explosions in quick
        // succession don't cancel each other out into a weak wobble.
        public void Shake (float duration = 0.25f, float magnitude = 0.35f)
        {
            if (m_ShakeTimeRemaining > 0f && magnitude <= m_ShakeMagnitude)
                return;

            m_ShakeDuration = duration;
            m_ShakeTimeRemaining = duration;
            m_ShakeMagnitude = magnitude;
        }


        private void LateUpdate ()
        {
            if (m_Camera == null)
                return;

            if (m_ShakeTimeRemaining > 0f)
            {
                m_ShakeTimeRemaining -= Time.deltaTime;

                // Fade the shake out over its lifetime so it settles smoothly.
                float damper = m_ShakeDuration > 0f ? Mathf.Clamp01(m_ShakeTimeRemaining / m_ShakeDuration) : 0f;
                Vector3 offset = UnityEngine.Random.insideUnitSphere * (m_ShakeMagnitude * damper);
                m_Camera.transform.localPosition = m_CameraBaseLocalPos + offset;
            }
            else
            {
                m_Camera.transform.localPosition = m_CameraBaseLocalPos;
            }
        }


        private void OnDestroy ()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
