using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace Tanks.Complete
{
    /// <summary>
    /// Keyboard-only tank controller for offline local play.
    /// This deliberately avoids TankInputUser/InputAction ownership logic used by online tanks.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5)]
    public class OfflineTankController : MonoBehaviour
    {
        private static readonly FieldInfo k_OldExplosionForceField =
            typeof(TankMovement).GetField("m_ExplosionForceValue", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo k_OldHasSpecialShellField =
            typeof(TankShooting).GetField("m_HasSpecialShell", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo k_OldSpecialShellMultiplierField =
            typeof(TankShooting).GetField("m_SpecialShellMultiplier", BindingFlags.Instance | BindingFlags.NonPublic);

        private TankMovement m_SourceMovement;
        private TankShooting m_SourceShooting;
        private Rigidbody m_Rigidbody;
        private Canvas m_WorldCanvas;

        private float m_MoveInput;
        private float m_TurnInput;
        private float m_CurrentSpeedInput;
        private float m_ExplosionVelocityDecay;
        private Vector3 m_ExplosionVelocity;

        private bool m_Charging;
        private bool m_Fired;
        private float m_CurrentLaunchForce;
        private float m_ShotCooldownTimer;
        private float m_OriginalPitch;

        public int ControlIndex { get; private set; } = 1;

        public bool IsConfiguredFor(TankMovement movement, TankShooting shooting, int controlIndex)
        {
            return m_SourceMovement == movement
                && m_SourceShooting == shooting
                && ControlIndex == Mathf.Clamp(controlIndex, 1, 2);
        }

        public void ConfigureFrom(TankMovement movement, TankShooting shooting, int controlIndex)
        {
            m_SourceMovement = movement;
            m_SourceShooting = shooting;
            ControlIndex = Mathf.Clamp(controlIndex, 1, 2);
            CacheReferences();
            DisableOnlinePlayerControllers();
            ResetRuntimeState();
        }

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
            ResetRuntimeState();
        }

        private void CacheReferences()
        {
            if (m_SourceMovement == null) m_SourceMovement = GetComponent<TankMovement>();
            if (m_SourceShooting == null) m_SourceShooting = GetComponent<TankShooting>();
            if (m_Rigidbody == null) m_Rigidbody = GetComponent<Rigidbody>();
            if (m_WorldCanvas == null) m_WorldCanvas = GetComponentInChildren<Canvas>(true);

            if (m_SourceMovement != null && m_SourceMovement.m_MovementAudio != null && m_OriginalPitch == 0f)
                m_OriginalPitch = m_SourceMovement.m_MovementAudio.pitch;
        }

        private void ResetRuntimeState()
        {
            m_MoveInput = 0f;
            m_TurnInput = 0f;
            m_CurrentSpeedInput = 0f;
            m_ExplosionVelocity = Vector3.zero;
            m_ExplosionVelocityDecay = 0f;
            m_Charging = false;
            m_Fired = false;

            if (m_SourceShooting != null)
            {
                m_CurrentLaunchForce = m_SourceShooting.m_MinLaunchForce;
                if (m_SourceShooting.m_AimSlider != null)
                {
                    m_SourceShooting.m_AimSlider.minValue = m_SourceShooting.m_MinLaunchForce;
                    m_SourceShooting.m_AimSlider.maxValue = m_SourceShooting.m_MaxLaunchForce;
                    m_SourceShooting.m_AimSlider.value = m_SourceShooting.m_MinLaunchForce;
                }
            }

            if (m_Rigidbody != null)
            {
                m_Rigidbody.isKinematic = false;
                m_Rigidbody.linearVelocity = Vector3.zero;
                m_Rigidbody.angularVelocity = Vector3.zero;
            }
        }

        private void Update()
        {
            if (!CanRunOfflineControl())
            {
                StopMoving();
                return;
            }

            DisableOnlinePlayerControllers();
            ReadKeyboard();
            UpdateEngineAudio();
            UpdateShooting();
        }

        private void FixedUpdate()
        {
            if (!CanRunOfflineControl())
            {
                StopMoving();
                return;
            }

            EnsureRigidbodyCanMove();
            PullExplosionVelocityFromLegacyMovement();
            Move(Time.fixedDeltaTime);
            Turn(Time.fixedDeltaTime);
        }

        private void LateUpdate()
        {
            if (CanRunOfflineControl())
                DisableOnlinePlayerControllers();
        }

        private bool CanRunOfflineControl()
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return false;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return false;
            if (m_SourceMovement == null || m_SourceShooting == null || m_Rigidbody == null) return false;
            if (m_SourceMovement.m_IsComputerControlled || m_SourceShooting.m_IsComputerControlled) return false;

            // TankManager toggles the world-space canvas with control phases in offline mode.
            return m_WorldCanvas == null || m_WorldCanvas.gameObject.activeInHierarchy;
        }

        private void DisableOnlinePlayerControllers()
        {
            if (m_SourceMovement != null && m_SourceMovement.enabled)
                m_SourceMovement.enabled = false;

            if (m_SourceShooting != null && m_SourceShooting.enabled)
                m_SourceShooting.enabled = false;
        }

        private void EnsureRigidbodyCanMove()
        {
            if (m_Rigidbody == null) return;

            if (m_Rigidbody.isKinematic)
                m_Rigidbody.isKinematic = false;

            m_Rigidbody.WakeUp();
        }

        private void ReadKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                m_MoveInput = 0f;
                m_TurnInput = 0f;
                return;
            }

            KeyControl forward;
            KeyControl backward;
            KeyControl left;
            KeyControl right;

            if (ControlIndex == 2)
            {
                forward = keyboard.upArrowKey;
                backward = keyboard.downArrowKey;
                left = keyboard.leftArrowKey;
                right = keyboard.rightArrowKey;
            }
            else
            {
                forward = keyboard.wKey;
                backward = keyboard.sKey;
                left = keyboard.aKey;
                right = keyboard.dKey;
            }

            m_MoveInput = ButtonAxis(forward, backward);
            m_TurnInput = ButtonAxis(right, left);
            m_MoveInput = ApplyDeadZone(m_MoveInput, m_SourceMovement.m_InputDeadZone);
            m_TurnInput = ApplyDeadZone(m_TurnInput, m_SourceMovement.m_InputDeadZone);
        }

        private static float ButtonAxis(ButtonControl positive, ButtonControl negative)
        {
            float value = 0f;
            if (positive != null && positive.isPressed) value += 1f;
            if (negative != null && negative.isPressed) value -= 1f;
            return value;
        }

        private void Move(float fixedDeltaTime)
        {
            float speedChange = Mathf.Abs(m_MoveInput) > Mathf.Abs(m_CurrentSpeedInput)
                ? m_SourceMovement.m_Acceleration
                : m_SourceMovement.m_Deceleration;

            m_CurrentSpeedInput = Mathf.MoveTowards(m_CurrentSpeedInput, m_MoveInput, speedChange * fixedDeltaTime);
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(transform.parent != null ? transform.parent.forward : Vector3.forward, Vector3.up);

            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Vector3 movement = forward * (m_CurrentSpeedInput * m_SourceMovement.m_Speed);

            m_Rigidbody.MovePosition(m_Rigidbody.position + (movement + m_ExplosionVelocity) * fixedDeltaTime);
            m_Rigidbody.linearVelocity = Vector3.zero;
            m_ExplosionVelocity = Vector3.Lerp(m_ExplosionVelocity, Vector3.zero, fixedDeltaTime * 3f);
            m_ExplosionVelocityDecay += fixedDeltaTime;
        }

        private void Turn(float fixedDeltaTime)
        {
            if (Mathf.Abs(m_TurnInput) < 0.001f) return;

            float turn = m_TurnInput * m_SourceMovement.m_TurnSpeed * fixedDeltaTime;
            Quaternion turnRotation = Quaternion.Euler(0f, turn, 0f);
            m_Rigidbody.MoveRotation(m_Rigidbody.rotation * turnRotation);
        }

        private void StopMoving()
        {
            m_MoveInput = 0f;
            m_TurnInput = 0f;
            m_CurrentSpeedInput = 0f;
            m_Charging = false;
            m_Fired = false;

            if (m_Rigidbody != null && !m_Rigidbody.isKinematic)
                m_Rigidbody.linearVelocity = Vector3.zero;
        }

        private void UpdateEngineAudio()
        {
            var audio = m_SourceMovement.m_MovementAudio;
            if (audio == null) return;

            bool moving = Mathf.Abs(m_MoveInput) >= 0.1f || Mathf.Abs(m_TurnInput) >= 0.1f;
            AudioClip targetClip = moving ? m_SourceMovement.m_EngineDriving : m_SourceMovement.m_EngineIdling;
            if (targetClip == null || audio.clip == targetClip) return;

            audio.clip = targetClip;
            audio.pitch = Random.Range(m_OriginalPitch - m_SourceMovement.m_PitchRange, m_OriginalPitch + m_SourceMovement.m_PitchRange);
            audio.Play();
        }

        private void UpdateShooting()
        {
            if (m_SourceShooting == null) return;

            if (m_ShotCooldownTimer > 0f)
                m_ShotCooldownTimer -= Time.deltaTime;

            if (m_SourceShooting.m_AimSlider != null)
                m_SourceShooting.m_AimSlider.value = m_SourceShooting.m_MinLaunchForce;

            if (m_CurrentLaunchForce >= m_SourceShooting.m_MaxLaunchForce && !m_Fired)
            {
                m_CurrentLaunchForce = m_SourceShooting.m_MaxLaunchForce;
                Fire();
                return;
            }

            bool firePressed = IsFirePressed();
            bool fireDown = IsFirePressedThisFrame();
            bool fireUp = IsFireReleasedThisFrame();

            if (m_ShotCooldownTimer <= 0f && fireDown)
            {
                m_Charging = true;
                m_Fired = false;
                m_CurrentLaunchForce = m_SourceShooting.m_MinLaunchForce;

                if (m_SourceShooting.m_ShootingAudio != null && m_SourceShooting.m_ChargingClip != null)
                {
                    m_SourceShooting.m_ShootingAudio.clip = m_SourceShooting.m_ChargingClip;
                    m_SourceShooting.m_ShootingAudio.Play();
                }
            }
            else if (firePressed && m_Charging && !m_Fired)
            {
                float chargeSpeed = (m_SourceShooting.m_MaxLaunchForce - m_SourceShooting.m_MinLaunchForce) / m_SourceShooting.m_MaxChargeTime;
                m_CurrentLaunchForce += chargeSpeed * Time.deltaTime;

                if (m_SourceShooting.m_AimSlider != null)
                    m_SourceShooting.m_AimSlider.value = m_CurrentLaunchForce;
            }
            else if (fireUp && m_Charging && !m_Fired)
            {
                Fire();
            }
        }

        private bool IsFirePressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return ControlIndex == 2
                ? keyboard.rightShiftKey.isPressed || keyboard.leftShiftKey.isPressed
                : keyboard.spaceKey.isPressed;
        }

        private bool IsFirePressedThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return ControlIndex == 2
                ? keyboard.rightShiftKey.wasPressedThisFrame || keyboard.leftShiftKey.wasPressedThisFrame
                : keyboard.spaceKey.wasPressedThisFrame;
        }

        private bool IsFireReleasedThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return ControlIndex == 2
                ? keyboard.rightShiftKey.wasReleasedThisFrame || keyboard.leftShiftKey.wasReleasedThisFrame
                : keyboard.spaceKey.wasReleasedThisFrame;
        }

        private void Fire()
        {
            m_Fired = true;
            m_Charging = false;

            ShellExplosion.PlayLaunchEffectFromShellPrefab(m_SourceShooting.m_Shell, m_SourceShooting.m_FireTransform);
            Rigidbody shellInstance = Instantiate(m_SourceShooting.m_Shell, m_SourceShooting.m_FireTransform.position, m_SourceShooting.m_FireTransform.rotation);
            shellInstance.linearVelocity = m_CurrentLaunchForce * m_SourceShooting.m_FireTransform.forward;

            var explosionData = shellInstance.GetComponent<ShellExplosion>();
            if (explosionData != null)
            {
                explosionData.m_ExplosionForce = m_SourceShooting.m_ExplosionForce;
                explosionData.m_ExplosionRadius = m_SourceShooting.m_ExplosionRadius;
                explosionData.m_MaxDamage = m_SourceShooting.m_MaxDamage;

                if (TryGetSpecialShell(out float damageMultiplier))
                    explosionData.m_MaxDamage *= damageMultiplier;
            }

            if (m_SourceShooting.m_ShootingAudio != null && m_SourceShooting.m_FireClip != null)
            {
                m_SourceShooting.m_ShootingAudio.clip = m_SourceShooting.m_FireClip;
                m_SourceShooting.m_ShootingAudio.Play();
            }

            m_CurrentLaunchForce = m_SourceShooting.m_MinLaunchForce;
            m_ShotCooldownTimer = m_SourceShooting.m_ShotCooldown;
        }

        private bool TryGetSpecialShell(out float damageMultiplier)
        {
            damageMultiplier = 1f;
            if (m_SourceShooting == null || k_OldHasSpecialShellField == null || k_OldSpecialShellMultiplierField == null)
                return false;

            bool hasSpecial = (bool)k_OldHasSpecialShellField.GetValue(m_SourceShooting);
            if (!hasSpecial) return false;

            damageMultiplier = (float)k_OldSpecialShellMultiplierField.GetValue(m_SourceShooting);
            k_OldHasSpecialShellField.SetValue(m_SourceShooting, false);
            k_OldSpecialShellMultiplierField.SetValue(m_SourceShooting, 1f);
            return true;
        }

        private void PullExplosionVelocityFromLegacyMovement()
        {
            if (m_SourceMovement == null || k_OldExplosionForceField == null) return;

            var value = (Vector3)k_OldExplosionForceField.GetValue(m_SourceMovement);
            if (value.sqrMagnitude <= 0.0001f) return;

            m_ExplosionVelocity = value;
            m_ExplosionVelocityDecay = 0f;
            k_OldExplosionForceField.SetValue(m_SourceMovement, Vector3.zero);
        }

        private static float ApplyDeadZone(float value, float deadZone)
        {
            return Mathf.Abs(value) < deadZone ? 0f : Mathf.Clamp(value, -1f, 1f);
        }
    }
}
