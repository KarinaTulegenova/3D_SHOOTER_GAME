using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.FPS.Gameplay
{
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputHandler), typeof(AudioSource))]
    public class TpsPlayerController : MonoBehaviour
    {
        [Header("Visuals")]
        public GameObject SoldierPrefab;
        public RuntimeAnimatorController RifleAnimatorController;
        public string ArsenalName = "Rifle";
        public Vector3 SoldierLocalPosition = new Vector3(0f, 0.08f, 0f);
        public Vector3 SoldierLocalEulerAngles = Vector3.zero;
        public Vector3 SoldierLocalScale = Vector3.one;

        [Header("Movement")]
        public float WalkSpeed = 2.2f;
        public float RunSpeed = 4.4f;
        public float RotationSharpness = 18f;
        public float MovementSharpness = 18f;
        public float GravityDownForce = 22f;
        public float JumpForce = 7.5f;
        public float KillHeight = -50f;
        public LayerMask GroundCheckLayers = -1;
        public float GroundCheckDistance = 0.08f;
        public float SpawnGroundProbeHeight = 4f;
        public float SpawnGroundProbeDistance = 12f;
        public float SpawnGroundClearance = 0.03f;

        [Header("Camera")]
        public Vector3 CameraOffset = new Vector3(0.55f, 1.75f, -3f);
        public Vector3 CameraLookOffset = new Vector3(0f, 1.2f, 0f);
        public float CameraPitchMin = -25f;
        public float CameraPitchMax = 55f;
        public float CameraPositionSharpness = 16f;
        public float CameraRotationSharpness = 18f;
        public float CameraYawSensitivity = 2f;
        public float CameraPitchSensitivity = 1.4f;

        [Header("Animation")]
        [Tooltip("Animator that contains the Die trigger for the TPS player death animation")]
        [SerializeField] Animator DeathAnimator;
        public string DeathTriggerParameter = "Die";
        public string SpeedParameter = "Speed";
        public float WalkSpeedParameterValue = 0.5f;
        public float RunSpeedParameterValue = 1f;
        public float AnimatorDampTime = 0.14f;
        public float AnimatorSpeedSharpness = 12f;

        [Header("Shooting")]
        public bool EnableShooting = true;
        public Vector3 RuntimeWeaponLocalPosition = new Vector3(0.35f, -0.3f, 0.8f);
        public Vector3 RuntimeWeaponLocalEulerAngles = Vector3.zero;

        [Header("Mobile")]
        public bool AutoCreateMobileControls = true;
        public MobileInputController MobileInput;

        public Vector3 CharacterVelocity { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsDead { get; private set; }

        CharacterController m_Controller;
        PlayerInputHandler m_InputHandler;
        AudioSource m_AudioSource;
        Health m_Health;
        Actor m_Actor;
        Animator m_Animator;
        Camera m_Camera;
        Transform m_CameraTransform;
        WeaponController m_RuntimeWeapon;
        Transform m_RuntimeWeaponSocket;
        InputAction m_MoveAction;
        InputAction m_LookAction;
        InputAction m_JumpAction;
        InputAction m_SprintAction;
        InputAction m_FireAction;
        Vector3 m_CurrentHorizontalVelocity;
        float m_CurrentAnimatorSpeed;
        float m_CameraYaw;
        float m_CameraPitch = 12f;
        int m_SpeedParameterHash;
        readonly Dictionary<string, AnimatorControllerParameterType> m_AnimatorParameters =
            new Dictionary<string, AnimatorControllerParameterType>();

        void Awake()
        {
            ActorsManager actorsManager = FindFirstObjectByType<ActorsManager>();
            if (actorsManager != null)
                actorsManager.SetPlayer(gameObject);
        }

        void Start()
        {
            m_Controller = GetComponent<CharacterController>();
            m_InputHandler = GetComponent<PlayerInputHandler>();
            m_AudioSource = GetComponent<AudioSource>();
            m_Health = GetComponent<Health>();
            m_Actor = GetComponent<Actor>();
            m_SpeedParameterHash = Animator.StringToHash(SpeedParameter);

            if (m_Health != null)
            {
                m_Health.Invincible = false;
                m_Health.OnDie += OnDie;
            }

            ConfigureController();
            SnapPlayerToGround();
            ConfigureAimPoint();
            ConfigureVisualCharacter();
            ConfigureCamera();
            ConfigureInputActions();
            ConfigureMobileInput();
            ConfigureRuntimeWeapon();
        }

        void OnDestroy()
        {
            if (m_Health != null)
                m_Health.OnDie -= OnDie;

            if (m_RuntimeWeapon != null)
                m_RuntimeWeapon.OnShootProcessed -= OnRuntimeWeaponShoot;
        }

        void Update()
        {
            if (IsDead)
            {
                SetAnimatorSpeed(0f);
                return;
            }

            if (m_Health != null && !m_Health.IsDead)
                m_Health.Invincible = false;

            if (transform.position.y < KillHeight && m_Health != null)
            {
                m_Health.Kill();
                return;
            }

            UpdateCameraInput();
            UpdateGroundedState();
            UpdateMovement();
            UpdateRuntimeWeapon();
        }

        void LateUpdate()
        {
            UpdateCameraTransform();
        }

        void ConfigureController()
        {
            if (m_Controller == null)
                return;

            m_Controller.height = 1.8f;
            m_Controller.radius = 0.35f;
            m_Controller.center = new Vector3(0f, 0.9f, 0f);
            m_Controller.stepOffset = 0.3f;
            m_Controller.slopeLimit = 45f;
            m_Controller.enableOverlapRecovery = true;
        }

        void SnapPlayerToGround()
        {
            if (m_Controller == null)
                return;

            Vector3 rayOrigin = transform.position + Vector3.up * SpawnGroundProbeHeight;
            if (!TryFindGround(rayOrigin, SpawnGroundProbeDistance, out RaycastHit hit))
                return;

            bool wasEnabled = m_Controller.enabled;
            m_Controller.enabled = false;
            transform.position = hit.point + Vector3.up * SpawnGroundClearance;
            m_Controller.enabled = wasEnabled;
            CharacterVelocity = Vector3.zero;
            m_CurrentHorizontalVelocity = Vector3.zero;
        }

        void ConfigureAimPoint()
        {
            if (m_Actor == null || m_Actor.AimPoint != null)
                return;

            GameObject aimPoint = new GameObject("TPS AimPoint");
            aimPoint.transform.SetParent(transform, false);
            aimPoint.transform.localPosition = CameraLookOffset;
            m_Actor.AimPoint = aimPoint.transform;
        }

        void ConfigureVisualCharacter()
        {
            GameObject soldier = FindOrCreateSoldier();
            if (soldier == null)
                return;

            soldier.transform.SetParent(transform, false);
            soldier.transform.localPosition = SoldierLocalPosition;
            soldier.transform.localRotation = Quaternion.Euler(SoldierLocalEulerAngles);
            soldier.transform.localScale = SoldierLocalScale;

            global::PlayerController arsenalController = soldier.GetComponent<global::PlayerController>();
            if (arsenalController != null)
            {
                arsenalController.rightWeaponLocalScale = new Vector3(0.3937008f, 0.3937008f, 0.3937008f);
                try
                {
                    arsenalController.SetArsenal(ArsenalName);
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning("[TPS] Could not attach visual rifle: " + exception.Message, soldier);
                }
            }

            m_Animator = DeathAnimator != null ? DeathAnimator : soldier.GetComponentInChildren<Animator>(true);
            if (m_Animator == null)
                return;

            m_Animator.applyRootMotion = false;
            m_Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (RifleAnimatorController != null)
                m_Animator.runtimeAnimatorController = RifleAnimatorController;
            CacheAnimatorParameters();
            SetAnimatorBool("Aiming", false);
            SetAnimatorBool("Squat", false);
            if (CanUseAnimator())
                m_Animator.Play("Idle", 0, 0f);
        }

        GameObject FindOrCreateSoldier()
        {
            Transform existing = transform.Find("ThirdPersonPlayerModel");
            if (existing != null)
                return existing.gameObject;

            if (SoldierPrefab == null)
                return null;

            GameObject soldier = Instantiate(SoldierPrefab, transform);
            soldier.name = "ThirdPersonPlayerModel";
            return soldier;
        }

        void ConfigureCamera()
        {
            DisableExistingCameras();

            GameObject cameraObject = new GameObject("TPS Camera");
            m_CameraTransform = cameraObject.transform;
            m_Camera = cameraObject.AddComponent<Camera>();
            m_Camera.tag = "MainCamera";
            m_Camera.nearClipPlane = 0.05f;
            m_Camera.fieldOfView = 60f;
            cameraObject.AddComponent<AudioListener>();

            m_CameraYaw = transform.eulerAngles.y;
            UpdateCameraTransform(true);
        }

        void ConfigureInputActions()
        {
            InputActionAsset actions = InputSystem.actions;
            if (actions == null)
                return;

            m_MoveAction = actions.FindAction("Player/Move");
            m_LookAction = actions.FindAction("Player/Look");
            m_JumpAction = actions.FindAction("Player/Jump");
            m_SprintAction = actions.FindAction("Player/Sprint");
            m_FireAction = actions.FindAction("Player/Fire");

            m_MoveAction?.Enable();
            m_LookAction?.Enable();
            m_JumpAction?.Enable();
            m_SprintAction?.Enable();
            m_FireAction?.Enable();
        }

        void ConfigureMobileInput()
        {
            if (MobileInput == null)
                MobileInput = MobileInputController.Instance;

            if (MobileInput == null && AutoCreateMobileControls)
                MobileInput = MobileInputController.EnsureExists();

            if (MobileInput != null)
                MobileInput.SetGameplayControlsActive(MobileInput.ForceShowInEditor || MobileInputController.ShouldUseMobileControls);
        }

        void ConfigureRuntimeWeapon()
        {
            if (!EnableShooting || m_CameraTransform == null)
                return;

            PlayerWeaponsManager sourceWeaponsManager = GetComponent<PlayerWeaponsManager>();
            if (sourceWeaponsManager == null || sourceWeaponsManager.StartingWeapons == null ||
                sourceWeaponsManager.StartingWeapons.Count == 0 || sourceWeaponsManager.StartingWeapons[0] == null)
                return;

            GameObject socketObject = new GameObject("TPS Runtime Weapon Socket");
            m_RuntimeWeaponSocket = socketObject.transform;
            m_RuntimeWeaponSocket.SetParent(m_CameraTransform, false);
            m_RuntimeWeaponSocket.localPosition = RuntimeWeaponLocalPosition;
            m_RuntimeWeaponSocket.localRotation = Quaternion.Euler(RuntimeWeaponLocalEulerAngles);

            WeaponController weaponPrefab = sourceWeaponsManager.StartingWeapons[0];
            m_RuntimeWeapon = Instantiate(weaponPrefab, m_RuntimeWeaponSocket);
            m_RuntimeWeapon.name = weaponPrefab.name + "_TPSRuntime";
            m_RuntimeWeapon.transform.localPosition = Vector3.zero;
            m_RuntimeWeapon.transform.localRotation = Quaternion.identity;
            m_RuntimeWeapon.SourcePrefab = weaponPrefab.gameObject;
            m_RuntimeWeapon.Owner = gameObject;
            m_RuntimeWeapon.ShowWeapon(true);
            m_RuntimeWeapon.OnShootProcessed += OnRuntimeWeaponShoot;

            HideRuntimeWeaponRenderers(m_RuntimeWeapon.gameObject);
        }

        void HideRuntimeWeaponRenderers(GameObject weaponObject)
        {
            Renderer[] renderers = weaponObject.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer != null)
                    targetRenderer.enabled = false;
            }
        }

        void DisableExistingCameras()
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (Camera targetCamera in cameras)
            {
                if (targetCamera == null)
                    continue;

                targetCamera.enabled = false;
                if (targetCamera.CompareTag("MainCamera"))
                    targetCamera.tag = "Untagged";

                AudioListener listener = targetCamera.GetComponent<AudioListener>();
                if (listener != null)
                    listener.enabled = false;
            }
        }

        void UpdateCameraInput()
        {
            if (m_LookAction != null)
            {
                Vector2 rawLook = m_LookAction.ReadValue<Vector2>();
                if (MobileInput != null)
                    rawLook += MobileInput.ConsumeLookDelta();

                m_CameraYaw += rawLook.x * CameraYawSensitivity;
                m_CameraPitch -= rawLook.y * CameraPitchSensitivity;
                m_CameraPitch = Mathf.Clamp(m_CameraPitch, CameraPitchMin, CameraPitchMax);
                return;
            }

            if (m_InputHandler == null)
                return;

            Vector2 mobileLook = MobileInput != null ? MobileInput.ConsumeLookDelta() : Vector2.zero;
            m_CameraYaw += (m_InputHandler.GetLookInputsHorizontal() + mobileLook.x) * CameraYawSensitivity;
            m_CameraPitch -= (m_InputHandler.GetLookInputsVertical() + mobileLook.y) * CameraPitchSensitivity;
            m_CameraPitch = Mathf.Clamp(m_CameraPitch, CameraPitchMin, CameraPitchMax);
        }

        void UpdateGroundedState()
        {
            if (m_Controller == null)
                return;

            float checkDistance = m_Controller.skinWidth + GroundCheckDistance;
            Vector3 origin = transform.TransformPoint(m_Controller.center);
            float radius = Mathf.Max(0.05f, m_Controller.radius * 0.9f);
            float halfHeight = Mathf.Max(radius, m_Controller.height * 0.5f - radius);
            Vector3 bottomSphereCenter = origin + Vector3.down * halfHeight;

            IsGrounded = m_Controller.isGrounded ||
                TrySphereCastGround(bottomSphereCenter + Vector3.up * 0.05f, radius, checkDistance + 0.05f);
            if (IsGrounded && CharacterVelocity.y < 0f)
                CharacterVelocity = new Vector3(CharacterVelocity.x, -2f, CharacterVelocity.z);
        }

        void UpdateMovement()
        {
            Vector3 moveInput = ReadMoveInput();
            bool hasInput = moveInput.sqrMagnitude > 0.001f;
            bool isSprinting = hasInput && ReadSprintHeld();
            float targetSpeed = isSprinting ? RunSpeed : WalkSpeed;

            Quaternion yawRotation = Quaternion.Euler(0f, m_CameraYaw, 0f);
            Vector3 desiredDirection = yawRotation * new Vector3(moveInput.x, 0f, moveInput.z);
            desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);

            Vector3 targetHorizontalVelocity = desiredDirection * targetSpeed;
            m_CurrentHorizontalVelocity = Vector3.Lerp(
                m_CurrentHorizontalVelocity,
                targetHorizontalVelocity,
                1f - Mathf.Exp(-MovementSharpness * Time.deltaTime));

            float verticalVelocity = CharacterVelocity.y;
            if (IsGrounded && ReadJumpDown())
                verticalVelocity = JumpForce;
            else
                verticalVelocity -= GravityDownForce * Time.deltaTime;

            CharacterVelocity = new Vector3(m_CurrentHorizontalVelocity.x, verticalVelocity, m_CurrentHorizontalVelocity.z);
            if (m_Controller != null)
            {
                CollisionFlags collisionFlags = m_Controller.Move(CharacterVelocity * Time.deltaTime);
                if ((collisionFlags & CollisionFlags.Below) != 0 && CharacterVelocity.y < 0f)
                {
                    IsGrounded = true;
                    CharacterVelocity = new Vector3(CharacterVelocity.x, -2f, CharacterVelocity.z);
                }
            }

            if (hasInput)
            {
                Quaternion targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    1f - Mathf.Exp(-RotationSharpness * Time.deltaTime));
            }

            float horizontalSpeed = new Vector3(CharacterVelocity.x, 0f, CharacterVelocity.z).magnitude;
            float targetAnimatorSpeed = 0f;
            if (horizontalSpeed > 0.1f)
            {
                float runBlend = Mathf.InverseLerp(WalkSpeed, RunSpeed, horizontalSpeed);
                targetAnimatorSpeed = Mathf.Lerp(WalkSpeedParameterValue, RunSpeedParameterValue, runBlend);
            }

            m_CurrentAnimatorSpeed = Mathf.Lerp(
                m_CurrentAnimatorSpeed,
                targetAnimatorSpeed,
                1f - Mathf.Exp(-AnimatorSpeedSharpness * Time.deltaTime));
            float animatorSpeed = m_CurrentAnimatorSpeed;
            SetAnimatorSpeed(animatorSpeed);
        }

        Vector3 ReadMoveInput()
        {
            Vector2 actionInput = m_MoveAction != null ? m_MoveAction.ReadValue<Vector2>() : Vector2.zero;
            Vector2 keyboardInput = Vector2.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                    keyboardInput.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                    keyboardInput.y -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                    keyboardInput.x += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                    keyboardInput.x -= 1f;
            }

            Vector2 input = keyboardInput.sqrMagnitude > 0f ? keyboardInput : actionInput;
            if (MobileInput != null && MobileInput.MoveInput.sqrMagnitude > 0.001f)
                input = MobileInput.MoveInput;

            if (input.sqrMagnitude <= 0.001f && m_InputHandler != null)
            {
                Vector3 handlerInput = m_InputHandler.GetMoveInput();
                if (handlerInput.sqrMagnitude > 0.001f)
                    return handlerInput;
            }

            input = Vector2.ClampMagnitude(input, 1f);
            return new Vector3(input.x, 0f, input.y);
        }

        bool ReadSprintHeld()
        {
            if (MobileInput != null && MobileInput.SprintHeld)
                return true;

            if (m_SprintAction != null && m_SprintAction.IsPressed())
                return true;

            return Keyboard.current != null &&
                (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        }

        bool ReadJumpDown()
        {
            if (MobileInput != null && MobileInput.ConsumeJumpPressed())
                return true;

            if (m_JumpAction != null && m_JumpAction.WasPressedThisFrame())
                return true;

            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        }

        bool ReadFireDown()
        {
            if (MobileInput != null && MobileInput.ConsumeFirePressed())
                return true;

            if (m_FireAction != null && m_FireAction.WasPressedThisFrame())
                return true;

            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }

        bool ReadFireHeld()
        {
            if (MobileInput != null && MobileInput.FireHeld)
                return true;

            if (m_FireAction != null && m_FireAction.IsPressed())
                return true;

            return Mouse.current != null && Mouse.current.leftButton.isPressed;
        }

        bool ReadFireReleased()
        {
            if (MobileInput != null && MobileInput.ConsumeFireReleased())
                return true;

            if (m_FireAction != null && m_FireAction.WasReleasedThisFrame())
                return true;

            return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
        }

        void UpdateRuntimeWeapon()
        {
            if (m_RuntimeWeapon == null)
                return;

            if (ReadReloadDown())
            {
                m_RuntimeWeapon.StartReloadAnimation();
                SetAnimatorTrigger("Reload");
            }

            if (!m_RuntimeWeapon.IsReloading)
                m_RuntimeWeapon.HandleShootInputs(ReadFireDown(), ReadFireHeld(), ReadFireReleased());
        }

        bool ReadReloadDown()
        {
            if (MobileInput != null && MobileInput.ConsumeReloadPressed())
                return true;

            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
        }

        void OnRuntimeWeaponShoot()
        {
            SetAnimatorTrigger("Attack");
        }

        bool TryFindGround(Vector3 rayOrigin, float maxDistance, out RaycastHit groundHit)
        {
            groundHit = default;
            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, maxDistance,
                GroundCheckLayers, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
                return false;

            float closestDistance = Mathf.Infinity;
            bool foundGround = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || IsOwnCollider(hit.collider))
                    continue;

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    groundHit = hit;
                    foundGround = true;
                }
            }

            return foundGround;
        }

        bool TrySphereCastGround(Vector3 origin, float radius, float maxDistance)
        {
            RaycastHit[] hits = Physics.SphereCastAll(origin, radius, Vector3.down, maxDistance,
                GroundCheckLayers, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
                return false;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || IsOwnCollider(hit.collider))
                    continue;

                return true;
            }

            return false;
        }

        bool IsOwnCollider(Collider candidate)
        {
            return candidate != null && candidate.transform.IsChildOf(transform);
        }

        void SetAnimatorSpeed(float speed)
        {
            if (!CanUseAnimator() ||
                !m_AnimatorParameters.TryGetValue(SpeedParameter, out AnimatorControllerParameterType type) ||
                type != AnimatorControllerParameterType.Float)
                return;

            m_Animator.SetFloat(m_SpeedParameterHash, speed, AnimatorDampTime, Time.deltaTime);
        }

        void CacheAnimatorParameters()
        {
            m_AnimatorParameters.Clear();

            if (!CanUseAnimator())
                return;

            foreach (AnimatorControllerParameter parameter in m_Animator.parameters)
                m_AnimatorParameters[parameter.name] = parameter.type;
        }

        void SetAnimatorBool(string parameterName, bool value)
        {
            if (CanUseAnimator() &&
                m_AnimatorParameters.TryGetValue(parameterName, out AnimatorControllerParameterType type) &&
                type == AnimatorControllerParameterType.Bool)
            {
                m_Animator.SetBool(parameterName, value);
            }
        }

        void SetAnimatorTrigger(string parameterName)
        {
            if (CanUseAnimator() &&
                m_AnimatorParameters.TryGetValue(parameterName, out AnimatorControllerParameterType type) &&
                type == AnimatorControllerParameterType.Trigger)
            {
                m_Animator.SetTrigger(parameterName);
            }
        }

        bool CanUseAnimator()
        {
            return m_Animator != null &&
                m_Animator.isActiveAndEnabled &&
                m_Animator.runtimeAnimatorController != null;
        }

        void UpdateCameraTransform(bool immediate = false)
        {
            if (m_CameraTransform == null)
                return;

            Quaternion cameraRotation = Quaternion.Euler(m_CameraPitch, m_CameraYaw, 0f);
            Vector3 lookTarget = transform.position + CameraLookOffset;
            Vector3 desiredPosition = lookTarget + cameraRotation * CameraOffset;
            Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - desiredPosition, Vector3.up);

            if (immediate)
            {
                m_CameraTransform.SetPositionAndRotation(desiredPosition, desiredRotation);
                return;
            }

            m_CameraTransform.position = Vector3.Lerp(
                m_CameraTransform.position,
                desiredPosition,
                1f - Mathf.Exp(-CameraPositionSharpness * Time.deltaTime));
            m_CameraTransform.rotation = Quaternion.Slerp(
                m_CameraTransform.rotation,
                desiredRotation,
                1f - Mathf.Exp(-CameraRotationSharpness * Time.deltaTime));
        }

        void OnDie()
        {
            IsDead = true;
            CharacterVelocity = Vector3.zero;
            m_CurrentHorizontalVelocity = Vector3.zero;
            m_CurrentAnimatorSpeed = 0f;

            SetAnimatorSpeed(0f);
            SetAnimatorTrigger(DeathTriggerParameter);

            EnableShooting = false;
            DisableRuntimeWeapon();
            DisablePlayerInput();

            if (m_AudioSource != null)
                m_AudioSource.Stop();

            EventManager.Broadcast(Events.PlayerDeathEvent);
        }

        void DisableRuntimeWeapon()
        {
            if (m_RuntimeWeapon == null)
                return;

            m_RuntimeWeapon.OnShootProcessed -= OnRuntimeWeaponShoot;
            m_RuntimeWeapon.ShowWeapon(false);
            m_RuntimeWeapon.enabled = false;
        }

        void DisablePlayerInput()
        {
            m_MoveAction?.Disable();
            m_LookAction?.Disable();
            m_JumpAction?.Disable();
            m_SprintAction?.Disable();
            m_FireAction?.Disable();

            PlayerWeaponsManager weaponsManager = GetComponent<PlayerWeaponsManager>();
            if (weaponsManager != null)
            {
                weaponsManager.SwitchToWeaponIndex(-1, true);
                weaponsManager.enabled = false;
            }

            if (m_InputHandler != null)
                m_InputHandler.enabled = false;

            if (MobileInput != null)
                MobileInput.SetGameplayControlsActive(false);
        }
    }
}
