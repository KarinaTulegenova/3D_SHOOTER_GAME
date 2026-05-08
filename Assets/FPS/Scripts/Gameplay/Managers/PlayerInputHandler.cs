using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.FPS.Gameplay
{
    public class PlayerInputHandler : MonoBehaviour
    {
        [Tooltip("Sensitivity multiplier for moving the camera around")]
        public float LookSensitivity = 1f;

        [Tooltip("Additional sensitivity multiplier for WebGL")]
        public float WebglLookSensitivityMultiplier = 0.25f;

        [Tooltip("Limit to consider an input when using a trigger on a controller")]
        public float TriggerAxisThreshold = 0.4f;

        [Tooltip("Used to flip the vertical input axis")]
        public bool InvertYAxis = false;

        [Tooltip("Used to flip the horizontal input axis")]
        public bool InvertXAxis = false;

        [Tooltip("Small input values below this are ignored so movement can stop cleanly.")]
        public float MoveInputDeadZone = 0.15f;

        GameFlowManager m_GameFlowManager;
        PlayerCharacterController m_PlayerCharacterController;
        bool m_FireInputWasHeld;

        private InputAction m_MoveAction;
        private InputAction m_LookAction;
        private InputAction m_JumpAction;
        private InputAction m_FireAction;
        private InputAction m_AimAction;
        private InputAction m_SprintAction;
        private InputAction m_CrouchAction;
        private InputAction m_ReloadAction;
        private InputAction m_NextWeaponAction;

        void Start()
        {
            m_PlayerCharacterController = GetComponent<PlayerCharacterController>();
            DebugUtility.HandleErrorIfNullGetComponent<PlayerCharacterController, PlayerInputHandler>(
                m_PlayerCharacterController, this, gameObject);
            m_GameFlowManager = FindFirstObjectByType<GameFlowManager>();
            DebugUtility.HandleErrorIfNullFindObject<GameFlowManager, PlayerInputHandler>(m_GameFlowManager, this);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            m_MoveAction = InputSystem.actions.FindAction("Player/Move");
            m_LookAction = InputSystem.actions.FindAction("Player/Look");
            m_JumpAction = InputSystem.actions.FindAction("Player/Jump");
            m_FireAction = InputSystem.actions.FindAction("Player/Fire");
            m_AimAction = InputSystem.actions.FindAction("Player/Aim");
            m_SprintAction = InputSystem.actions.FindAction("Player/Sprint");
            m_CrouchAction = InputSystem.actions.FindAction("Player/Crouch");
            m_ReloadAction = InputSystem.actions.FindAction("Player/Reload");
            m_NextWeaponAction = InputSystem.actions.FindAction("Player/NextWeapon");
            
            m_MoveAction.Enable();
            m_LookAction.Enable();
            m_JumpAction.Enable();
            m_FireAction.Enable();
            m_AimAction.Enable();
            m_SprintAction.Enable();
            m_CrouchAction.Enable();
            m_ReloadAction.Enable();
            m_NextWeaponAction.Enable();
        }

        void LateUpdate()
        {
            m_FireInputWasHeld = GetFireInputHeld();
        }

        public bool CanProcessInput()
        {
            if (!isActiveAndEnabled)
                return false;

            if (m_GameFlowManager == null)
                m_GameFlowManager = FindFirstObjectByType<GameFlowManager>();

            return Cursor.lockState == CursorLockMode.Locked &&
                   (m_GameFlowManager == null || !m_GameFlowManager.GameIsEnding);
        }

        public Vector3 GetMoveInput()
        {
            if (CanProcessInput())
            {
                Vector2 input = GetSanitizedMoveInput();
                Vector3 move = new Vector3(input.x, 0f, input.y);

                // constrain move input to a maximum magnitude of 1, otherwise diagonal movement might exceed the max move speed defined
                move = Vector3.ClampMagnitude(move, 1);

                return move;
            }

            return Vector3.zero;
        }

        Vector2 GetSanitizedMoveInput()
        {
            Vector2 input = m_MoveAction != null ? m_MoveAction.ReadValue<Vector2>() : Vector2.zero;

            if (Keyboard.current != null)
            {
                Vector2 keyboardInput = Vector2.zero;

                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                    keyboardInput.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                    keyboardInput.y -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                    keyboardInput.x += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                    keyboardInput.x -= 1f;

                if (keyboardInput.sqrMagnitude > 0f)
                    return Vector2.ClampMagnitude(keyboardInput, 1f);

                // No WASD / arrow keys held: do not fall back to MoveAction.ReadValue (composite can stick non-zero).
                bool gamepadDrivingMove = Gamepad.current != null &&
                    m_MoveAction != null &&
                    m_MoveAction.activeControl != null &&
                    m_MoveAction.activeControl.device is Gamepad &&
                    input.magnitude >= MoveInputDeadZone;

                if (!gamepadDrivingMove)
                    return Vector2.zero;

                return Vector2.ClampMagnitude(input, 1f);
            }

            return input.magnitude < MoveInputDeadZone ? Vector2.zero : Vector2.ClampMagnitude(input, 1f);
        }

        public float GetLookInputsHorizontal()
        {
            if (!CanProcessInput())
                return 0.0f;
            
            float input = m_LookAction.ReadValue<Vector2>().x;

            if (InvertXAxis)
                input *= -1;

            input *= LookSensitivity;
            
#if UNITY_WEBGL
            // Mouse tends to be even more sensitive in WebGL due to mouse acceleration, so reduce it even more
            input *= WebglLookSensitivityMultiplier;
#endif

            return input;
        }

        public float GetLookInputsVertical()
        {
            if (!CanProcessInput())
                return 0.0f;
            
            float input = m_LookAction.ReadValue<Vector2>().y;

            if (InvertYAxis)
                input *= -1;

            input *= LookSensitivity;
            
#if UNITY_WEBGL
            // Mouse tends to be even more sensitive in WebGL due to mouse acceleration, so reduce it even more
            input *= WebglLookSensitivityMultiplier;
#endif

            return input;
        }

        public bool GetJumpInputDown()
        {
            if (CanProcessInput())
            {
                return m_JumpAction.WasPressedThisFrame();
            }

            return false;
        }

        public bool GetJumpInputHeld()
        {
            if (CanProcessInput())
            {
                return m_JumpAction.IsPressed();
            }

            return false;
        }

        public bool GetFireInputDown()
        {
            return GetFireInputHeld() && !m_FireInputWasHeld;
        }

        public bool GetFireInputReleased()
        {
            return !GetFireInputHeld() && m_FireInputWasHeld;
        }

        public bool GetFireInputHeld()
        {
            if (CanProcessInput())
            {
                return m_FireAction.IsPressed();
            }

            return false;
        }

        public bool GetAimInputHeld()
        {
            if (CanProcessInput())
            {
                return m_AimAction.IsPressed();
            }

            return false;
        }

        public bool GetSprintInputHeld()
        {
            if (CanProcessInput())
            {
                return m_SprintAction.IsPressed();
            }

            return false;
        }

        public bool GetCrouchInputDown()
        {
            if (CanProcessInput())
            {
                return m_CrouchAction.WasPressedThisFrame();
            }

            return false;
        }

        public bool GetCrouchInputReleased()
        {
            if (CanProcessInput())
            {
                return m_CrouchAction.WasReleasedThisFrame();
            }

            return false;
        }

        public bool GetReloadButtonDown()
        {
            if (CanProcessInput())
            {
                return m_ReloadAction.WasPressedThisFrame();
            }

            return false;
        }

        public int GetSwitchWeaponInput()
        {
            if (CanProcessInput())
            {
                var input = m_NextWeaponAction.ReadValue<float>();

                if (input > 0f)
                    return -1;
                
                if (input < 0f)
                    return 1;
            }

            return 0;
        }

        public int GetSelectWeaponInput()
        {
            if (CanProcessInput())
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame)
                    return 1;
                if (Keyboard.current.digit2Key.wasPressedThisFrame)
                    return 2;
                if (Keyboard.current.digit3Key.wasPressedThisFrame)
                    return 3;
                if (Keyboard.current.digit4Key.wasPressedThisFrame)
                    return 4;
                if (Keyboard.current.digit5Key.wasPressedThisFrame)
                    return 5;
                if (Keyboard.current.digit6Key.wasPressedThisFrame)
                    return 6;
                if (Keyboard.current.digit7Key.wasPressedThisFrame)
                    return 7;
                if (Keyboard.current.digit8Key.wasPressedThisFrame)
                    return 8;
                if (Keyboard.current.digit9Key.wasPressedThisFrame)
                    return 9;
            }

            return 0;
        }
    }
}
