using Unity.FPS.Game;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class ThirdPersonPlayerAnimator : MonoBehaviour
    {
        public Animator Animator;
        public RuntimeAnimatorController AnimatorController;
        public string ArsenalName = "Rifle";
        public Vector3 VisualLocalPosition = new Vector3(0f, 1.55f, 0f);
        public Vector3 VisualLocalEulerAngles = Vector3.zero;
        public Vector3 VisualLocalScale = new Vector3(3.2f, 3.2f, 3.2f);
        public string SpeedParameter = "Speed";
        public string AttackTrigger = "Attack";
        [Tooltip("When true, forces Idle/Walk/Run via CrossFade (reliable with OnePistolController). When false, only Speed drives transitions.")]
        public bool ForceLocomotionState = true;
        public string IdleStateName = "Idle";
        public string WalkStateName = "Walk";
        public string RunStateName = "Run";
        public float WalkStateThreshold = 0.1f;
        public float RunStateThreshold = 0.66f;
        [Range(0.1f, 0.99f)]
        public float WalkSpeedParameterValue = 0.5f;
        public float LocomotionCrossFadeTime = 0.08f;
        public bool KeepWeaponReady = true;
        public string WeaponReadyLayerName = "RightHand";
        public string WeaponReadyParameter = "Aiming";
        public bool KeepBaseAimingOff = true;
        public AnimationClip UpperBodyReadyClip;
        public AnimationClip MovingWeaponClip;
        public bool UseUpperBodyReadyOverlay = false;
        public bool AlwaysUseMovingWeaponClip = false;
        public string UpperBodyRootName = "RigSpine1";
        [Range(0f, 1f)]
        public float UpperBodyReadyWeight = 1f;
        public bool DisableControllerWeaponLayerWhenOverlayIsActive = true;
        public bool LockVisualHeight = true;
        public bool ReadyPoseWhenStanding = true;
        public bool ReadyPoseWhenAirborne = true;
        public bool ReadyPoseWhenMoving = false;
        public float MovingInputThreshold = 0.1f;
        public bool UseTwoHandWeaponPose = false;
        public string LeftHandBoneName = "RigArmLeft3";
        public string WeaponSocketName = "RigPistolRight";
        public Vector3 LeftHandWeaponOffset = new Vector3(-0.08f, -0.02f, 0.08f);
        public Vector3 LeftHandEulerOffset = new Vector3(0f, 0f, 0f);
        [Range(0f, 1f)]
        public float LeftHandPoseWeight = 1f;
        public float SpeedDampTime = 0.1f;
        public bool UseProceduralLegRun = false;
        public string LeftUpperLegBoneName = "RigLegLeft1";
        public string LeftLowerLegBoneName = "RigLegLeft2";
        public string LeftFootBoneName = "RigLegLeft3";
        public string RightUpperLegBoneName = "RigLegRight1";
        public string RightLowerLegBoneName = "RigLegRight2";
        public string RightFootBoneName = "RigLegRight3";
        public float LegRunCycleSpeed = 9f;
        public float UpperLegSwingAngle = 28f;
        public float LowerLegSwingAngle = 34f;
        public float FootSwingAngle = 16f;

        PlayerInputHandler m_InputHandler;
        PlayerWeaponsManager m_WeaponsManager;
        PlayerCharacterController m_CharacterController;
        WeaponController m_CurrentWeapon;
        global::PlayerController m_ThirdPersonController;
        Transform m_VisualRoot;
        Transform m_LeftHandBone;
        Transform m_WeaponSocket;
        Transform m_LeftUpperLegBone;
        Transform m_LeftLowerLegBone;
        Transform m_LeftFootBone;
        Transform m_RightUpperLegBone;
        Transform m_RightLowerLegBone;
        Transform m_RightFootBone;
        Quaternion m_LeftUpperLegBaseRotation;
        Quaternion m_LeftLowerLegBaseRotation;
        Quaternion m_LeftFootBaseRotation;
        Quaternion m_RightUpperLegBaseRotation;
        Quaternion m_RightLowerLegBaseRotation;
        Quaternion m_RightFootBaseRotation;
        PlayableGraph m_AnimationGraph;
        AnimatorControllerPlayable m_ControllerPlayable;
        AnimationLayerMixerPlayable m_LayerMixerPlayable;
        AnimationClipPlayable m_MovingWeaponPlayable;
        bool m_UsingUpperBodyReadyOverlay;
        bool m_UsingFullBodyClipOverride;
        float m_CurrentMovementSpeed;

        void Start()
        {
            m_InputHandler = GetComponent<PlayerInputHandler>();
            m_WeaponsManager = GetComponent<PlayerWeaponsManager>();
            m_CharacterController = GetComponent<PlayerCharacterController>();

            if (Animator == null)
                Animator = GetComponentInChildren<Animator>(true);

            if (Animator != null)
            {
                m_VisualRoot = Animator.transform;
                Animator.applyRootMotion = false;
                Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Debug.Log("[RigFix] Third-person Animator uses avatar " +
                    (Animator.avatar != null ? Animator.avatar.name : "<missing>") +
                    ", Root Motion disabled, visual local position " + VisualLocalPosition + ".", this);
            }

            m_ThirdPersonController = GetComponentInChildren<global::PlayerController>(true);
            if (m_ThirdPersonController != null && !string.IsNullOrEmpty(ArsenalName))
                m_ThirdPersonController.SetArsenal(ArsenalName);

            EnsureAnimatorController();
            LockVisualTransform();
            BuildUpperBodyReadyOverlay();
            CacheLegBones();
            SubscribeToCurrentWeapon();
        }

        /// <summary>
        /// Called when third-person body is shown/hidden so the Humanoid graph re-initializes cleanly.
        /// </summary>
        public void OnThirdPersonVisualToggled(bool thirdPersonActive)
        {
            if (!thirdPersonActive || Animator == null || !Animator.gameObject.activeInHierarchy)
                return;

            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EnsureAnimatorController();
            Animator.Rebind();
            BuildUpperBodyReadyOverlay();
            CacheLegBones();
            Animator.Update(0f);
        }

        void Update()
        {
            LockVisualTransform();
            UpdateMoveAnimation();
            UpdateWeaponReadyPose();
            SubscribeToCurrentWeapon();
        }

        void LateUpdate()
        {
            LockVisualTransform();
            ApplyProceduralLegRun();
            ApplyTwoHandWeaponPose();
        }

        void OnDestroy()
        {
            UnsubscribeFromCurrentWeapon();

            if (m_AnimationGraph.IsValid())
                m_AnimationGraph.Destroy();
        }

        void UpdateMoveAnimation()
        {
            if (m_UsingFullBodyClipOverride)
                return;

            if (Animator == null || m_InputHandler == null || !Animator.isActiveAndEnabled || !Animator.isInitialized)
                return;

            if (!EnsureAnimatorController())
                return;

            Vector3 moveInput = m_InputHandler.GetMoveInput();
            bool hasMoveInput = moveInput.sqrMagnitude > 0.0001f;
            bool isSprinting = hasMoveInput && m_InputHandler.GetSprintInputHeld();
            float speed = hasMoveInput ? GetLocomotionSpeedParameter(isSprinting, moveInput.magnitude) : 0f;
            if (m_CharacterController != null)
            {
                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(m_CharacterController.CharacterVelocity, Vector3.up);
                float maxGroundSpeed = Mathf.Max(0.01f, m_CharacterController.MaxSpeedOnGround *
                    (isSprinting ? m_CharacterController.SprintSpeedModifier : 1f));
                float velNorm = Mathf.Clamp01(horizontalVelocity.magnitude / maxGroundSpeed);
                if (hasMoveInput || velNorm > 0.04f)
                    speed = Mathf.Max(speed, GetLocomotionSpeedParameter(isSprinting, velNorm));
            }

            if (HasAnimatorParameter(SpeedParameter, AnimatorControllerParameterType.Float))
            {
                if (!hasMoveInput && speed <= 0.001f)
                    SetAnimatorFloatImmediate(SpeedParameter, 0f);
                else
                    SetAnimatorFloat(SpeedParameter, speed);
            }

            m_CurrentMovementSpeed = speed;
            ApplyBaseLocomotionState(speed);
        }

        float GetLocomotionSpeedParameter(bool isSprinting, float normalizedInputOrVelocity)
        {
            float normalized = Mathf.Clamp01(normalizedInputOrVelocity);
            if (normalized <= 0.001f)
                return 0f;

            float walkValue = Mathf.Clamp(WalkSpeedParameterValue, WalkStateThreshold, RunStateThreshold - 0.01f);
            if (!isSprinting)
                return Mathf.Lerp(WalkStateThreshold, walkValue, normalized);

            return Mathf.Lerp(RunStateThreshold, 1f, normalized);
        }

        void ApplyBaseLocomotionState(float speed)
        {
            if (!ForceLocomotionState || Animator == null || m_ControllerPlayable.IsValid())
                return;

            string targetState = speed >= RunStateThreshold ? RunStateName :
                speed >= WalkStateThreshold ? WalkStateName : IdleStateName;
            if (string.IsNullOrEmpty(targetState))
                return;

            AnimatorStateInfo state = Animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo nextState = Animator.GetNextAnimatorStateInfo(0);
            if (state.IsName(targetState) || nextState.IsName(targetState) || state.IsTag("Attack") ||
                nextState.IsTag("Attack"))
                return;

            Animator.CrossFadeInFixedTime(targetState, LocomotionCrossFadeTime, 0);
        }

        void UpdateWeaponReadyPose()
        {
            if (!KeepWeaponReady || Animator == null || !Animator.isActiveAndEnabled || !Animator.isInitialized)
                return;

            if (m_UsingFullBodyClipOverride)
            {
                LoopMovingWeaponClip();
                return;
            }

            if (!EnsureAnimatorController())
                return;

            if (m_UsingUpperBodyReadyOverlay)
            {
                bool useMovingWeaponClip = ShouldUseMovingWeaponClip();
                float targetReadyWeight = useMovingWeaponClip ? 0f : ShouldUseReadyPose() ? UpperBodyReadyWeight : 0f;

                if (m_LayerMixerPlayable.IsValid())
                {
                    m_LayerMixerPlayable.SetInputWeight(1, targetReadyWeight);
                    if (m_MovingWeaponPlayable.IsValid())
                        m_LayerMixerPlayable.SetInputWeight(2, useMovingWeaponClip ? 1f : 0f);
                }

                if (useMovingWeaponClip)
                    LoopMovingWeaponClip();

                if (KeepBaseAimingOff && HasAnimatorParameter(WeaponReadyParameter, AnimatorControllerParameterType.Bool))
                    SetAnimatorBool(WeaponReadyParameter, false);
                else if (targetReadyWeight > 0f && HasAnimatorParameter(WeaponReadyParameter, AnimatorControllerParameterType.Bool))
                    SetAnimatorBool(WeaponReadyParameter, true);

                return;
            }

            int readyLayerIndex = Animator.GetLayerIndex(WeaponReadyLayerName);
            bool useReadyPose = ShouldUseReadyPose();
            if (readyLayerIndex >= 0)
                Animator.SetLayerWeight(readyLayerIndex, useReadyPose ? 1f : 0f);

            if (KeepBaseAimingOff && HasAnimatorParameter(WeaponReadyParameter, AnimatorControllerParameterType.Bool))
                SetAnimatorBool(WeaponReadyParameter, false);
            else if (HasAnimatorParameter(WeaponReadyParameter, AnimatorControllerParameterType.Bool))
            {
                SetAnimatorBool(WeaponReadyParameter, useReadyPose);
            }
        }

        bool ShouldUseReadyPose()
        {
            Vector3 moveInput = m_InputHandler != null ? m_InputHandler.GetMoveInput() : Vector3.zero;
            bool isMoving = moveInput.magnitude > MovingInputThreshold ||
                m_CurrentMovementSpeed > MovingInputThreshold;
            bool isAirborne = m_CharacterController != null && !m_CharacterController.IsGrounded;

            if (isMoving)
                return ReadyPoseWhenMoving;

            if (isAirborne)
                return ReadyPoseWhenAirborne;

            return ReadyPoseWhenStanding;
        }

        bool ShouldUseMovingWeaponClip()
        {
            Vector3 moveInput = m_InputHandler != null ? m_InputHandler.GetMoveInput() : Vector3.zero;
            bool isMoving = moveInput.magnitude > MovingInputThreshold;
            return MovingWeaponClip != null && (AlwaysUseMovingWeaponClip || isMoving);
        }

        void SubscribeToCurrentWeapon()
        {
            if (m_WeaponsManager == null)
                return;

            WeaponController activeWeapon = m_WeaponsManager.GetActiveWeapon();
            if (activeWeapon == m_CurrentWeapon)
                return;

            UnsubscribeFromCurrentWeapon();
            m_CurrentWeapon = activeWeapon;

            if (m_CurrentWeapon != null)
                m_CurrentWeapon.OnShootProcessed += PlayShootAnimation;
        }

        void UnsubscribeFromCurrentWeapon()
        {
            if (m_CurrentWeapon != null)
                m_CurrentWeapon.OnShootProcessed -= PlayShootAnimation;

            m_CurrentWeapon = null;
        }

        void PlayShootAnimation()
        {
            if (Animator != null &&
                Animator.isActiveAndEnabled &&
                Animator.isInitialized &&
                EnsureAnimatorController() &&
                HasAnimatorParameter(AttackTrigger, AnimatorControllerParameterType.Trigger))
            {
                SetAnimatorTrigger(AttackTrigger);
            }
        }

        bool EnsureAnimatorController()
        {
            if (Animator == null)
                return false;

            if (AnimatorController != null && Animator.runtimeAnimatorController != AnimatorController)
                Animator.runtimeAnimatorController = AnimatorController;

            return Animator.runtimeAnimatorController != null;
        }

        void BuildUpperBodyReadyOverlay()
        {
            if (m_AnimationGraph.IsValid())
                m_AnimationGraph.Destroy();

            m_UsingUpperBodyReadyOverlay = false;
            m_UsingFullBodyClipOverride = false;

            if (!UseUpperBodyReadyOverlay || Animator == null || (UpperBodyReadyClip == null && MovingWeaponClip == null))
                return;

            m_AnimationGraph = PlayableGraph.Create("ThirdPersonUpperBodyReady");
            m_AnimationGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            if (AlwaysUseMovingWeaponClip && MovingWeaponClip != null)
            {
                m_MovingWeaponPlayable = AnimationClipPlayable.Create(m_AnimationGraph, MovingWeaponClip);
                m_MovingWeaponPlayable.SetApplyFootIK(false);
                m_MovingWeaponPlayable.SetApplyPlayableIK(false);

                AnimationPlayableOutput fullBodyOutput = AnimationPlayableOutput.Create(m_AnimationGraph, "Animation", Animator);
                fullBodyOutput.SetSourcePlayable(m_MovingWeaponPlayable);

                m_AnimationGraph.Play();
                m_UsingUpperBodyReadyOverlay = true;
                m_UsingFullBodyClipOverride = true;
                Debug.Log("[RigFix] Playing full-body test clip '" + MovingWeaponClip.name +
                    "' as Animator override. Old controller layers are bypassed for this test.", this);
                return;
            }

            if (Animator.runtimeAnimatorController == null || UpperBodyReadyClip == null)
                return;

            m_ControllerPlayable = AnimatorControllerPlayable.Create(m_AnimationGraph, Animator.runtimeAnimatorController);

            AnimationClipPlayable readyClipPlayable = AnimationClipPlayable.Create(m_AnimationGraph, UpperBodyReadyClip);
            readyClipPlayable.SetApplyFootIK(false);
            readyClipPlayable.SetApplyPlayableIK(false);

            int playableInputCount = MovingWeaponClip != null ? 3 : 2;
            m_LayerMixerPlayable = AnimationLayerMixerPlayable.Create(m_AnimationGraph, playableInputCount);
            m_AnimationGraph.Connect(m_ControllerPlayable, 0, m_LayerMixerPlayable, 0);
            m_AnimationGraph.Connect(readyClipPlayable, 0, m_LayerMixerPlayable, 1);

            if (MovingWeaponClip != null)
            {
                m_MovingWeaponPlayable = AnimationClipPlayable.Create(m_AnimationGraph, MovingWeaponClip);
                m_MovingWeaponPlayable.SetApplyFootIK(false);
                m_MovingWeaponPlayable.SetApplyPlayableIK(false);
                m_AnimationGraph.Connect(m_MovingWeaponPlayable, 0, m_LayerMixerPlayable, 2);
            }

            m_LayerMixerPlayable.SetInputWeight(0, 1f);
            m_LayerMixerPlayable.SetInputWeight(1, UpperBodyReadyWeight);
            if (MovingWeaponClip != null)
                m_LayerMixerPlayable.SetInputWeight(2, 0f);

            if (DisableControllerWeaponLayerWhenOverlayIsActive)
            {
                int controllerWeaponLayer = GetControllerPlayableLayerIndex(WeaponReadyLayerName);
                if (controllerWeaponLayer >= 0)
                    m_ControllerPlayable.SetLayerWeight(controllerWeaponLayer, 0f);
            }

            AvatarMask upperBodyMask = CreateUpperBodyMask();
            if (upperBodyMask != null)
                m_LayerMixerPlayable.SetLayerMaskFromAvatarMask(1, upperBodyMask);

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(m_AnimationGraph, "Animation", Animator);
            output.SetSourcePlayable(m_LayerMixerPlayable);

            m_AnimationGraph.Play();
            m_UsingUpperBodyReadyOverlay = true;
            m_UsingFullBodyClipOverride = false;
        }

        void LoopMovingWeaponClip()
        {
            if (!m_MovingWeaponPlayable.IsValid() || MovingWeaponClip == null || MovingWeaponClip.length <= 0f)
                return;

            double time = m_MovingWeaponPlayable.GetTime();
            if (time >= MovingWeaponClip.length)
                m_MovingWeaponPlayable.SetTime(time % MovingWeaponClip.length);
        }

        void ApplyTwoHandWeaponPose()
        {
            if (!UseTwoHandWeaponPose || m_VisualRoot == null || LeftHandPoseWeight <= 0f)
                return;

            if (m_LeftHandBone == null)
                m_LeftHandBone = FindChildTransform(m_VisualRoot, LeftHandBoneName);

            if (m_WeaponSocket == null)
                m_WeaponSocket = FindChildTransform(m_VisualRoot, WeaponSocketName);

            if (m_LeftHandBone == null || m_WeaponSocket == null)
                return;

            Vector3 targetPosition = m_WeaponSocket.TransformPoint(LeftHandWeaponOffset);
            Quaternion targetRotation = m_WeaponSocket.rotation * Quaternion.Euler(LeftHandEulerOffset);

            m_LeftHandBone.position = Vector3.Lerp(m_LeftHandBone.position, targetPosition, LeftHandPoseWeight);
            m_LeftHandBone.rotation = Quaternion.Slerp(m_LeftHandBone.rotation, targetRotation, LeftHandPoseWeight);
        }

        void CacheLegBones()
        {
            if (m_VisualRoot == null)
                return;

            m_LeftUpperLegBone = FindChildTransform(m_VisualRoot, LeftUpperLegBoneName);
            m_LeftLowerLegBone = FindChildTransform(m_VisualRoot, LeftLowerLegBoneName);
            m_LeftFootBone = FindChildTransform(m_VisualRoot, LeftFootBoneName);
            m_RightUpperLegBone = FindChildTransform(m_VisualRoot, RightUpperLegBoneName);
            m_RightLowerLegBone = FindChildTransform(m_VisualRoot, RightLowerLegBoneName);
            m_RightFootBone = FindChildTransform(m_VisualRoot, RightFootBoneName);

            if (m_LeftUpperLegBone != null)
                m_LeftUpperLegBaseRotation = m_LeftUpperLegBone.localRotation;
            if (m_LeftLowerLegBone != null)
                m_LeftLowerLegBaseRotation = m_LeftLowerLegBone.localRotation;
            if (m_LeftFootBone != null)
                m_LeftFootBaseRotation = m_LeftFootBone.localRotation;
            if (m_RightUpperLegBone != null)
                m_RightUpperLegBaseRotation = m_RightUpperLegBone.localRotation;
            if (m_RightLowerLegBone != null)
                m_RightLowerLegBaseRotation = m_RightLowerLegBone.localRotation;
            if (m_RightFootBone != null)
                m_RightFootBaseRotation = m_RightFootBone.localRotation;
        }

        void ApplyProceduralLegRun()
        {
            if (!UseProceduralLegRun || m_VisualRoot == null)
                return;

            if (m_LeftUpperLegBone == null || m_RightUpperLegBone == null)
                CacheLegBones();

            float movement = Mathf.Clamp01(m_CurrentMovementSpeed);
            if (movement <= MovingInputThreshold)
            {
                ResetLegPose();
                return;
            }

            float phase = Time.time * LegRunCycleSpeed * Mathf.Lerp(0.65f, 1.2f, movement);
            float leftSwing = Mathf.Sin(phase) * UpperLegSwingAngle * movement;
            float rightSwing = -leftSwing;
            float leftKnee = Mathf.Max(0f, -Mathf.Sin(phase)) * LowerLegSwingAngle * movement;
            float rightKnee = Mathf.Max(0f, Mathf.Sin(phase)) * LowerLegSwingAngle * movement;
            float leftFoot = -leftSwing * 0.35f - leftKnee * 0.25f;
            float rightFoot = -rightSwing * 0.35f - rightKnee * 0.25f;

            ApplyLegPose(m_LeftUpperLegBone, m_LeftUpperLegBaseRotation, leftSwing, 0f, 0f);
            ApplyLegPose(m_RightUpperLegBone, m_RightUpperLegBaseRotation, rightSwing, 0f, 0f);
            ApplyLegPose(m_LeftLowerLegBone, m_LeftLowerLegBaseRotation, 0f, 0f, leftKnee);
            ApplyLegPose(m_RightLowerLegBone, m_RightLowerLegBaseRotation, 0f, 0f, rightKnee);
            ApplyLegPose(m_LeftFootBone, m_LeftFootBaseRotation, 0f, 0f, Mathf.Clamp(leftFoot, -FootSwingAngle, FootSwingAngle));
            ApplyLegPose(m_RightFootBone, m_RightFootBaseRotation, 0f, 0f, Mathf.Clamp(rightFoot, -FootSwingAngle, FootSwingAngle));
        }

        void ResetLegPose()
        {
            ApplyLegPose(m_LeftUpperLegBone, m_LeftUpperLegBaseRotation, 0f, 0f, 0f);
            ApplyLegPose(m_LeftLowerLegBone, m_LeftLowerLegBaseRotation, 0f, 0f, 0f);
            ApplyLegPose(m_LeftFootBone, m_LeftFootBaseRotation, 0f, 0f, 0f);
            ApplyLegPose(m_RightUpperLegBone, m_RightUpperLegBaseRotation, 0f, 0f, 0f);
            ApplyLegPose(m_RightLowerLegBone, m_RightLowerLegBaseRotation, 0f, 0f, 0f);
            ApplyLegPose(m_RightFootBone, m_RightFootBaseRotation, 0f, 0f, 0f);
        }

        void ApplyLegPose(Transform bone, Quaternion baseRotation, float xAngle, float yAngle, float zAngle)
        {
            if (bone == null)
                return;

            bone.localRotation = baseRotation * Quaternion.Euler(xAngle, yAngle, zAngle);
        }

        AvatarMask CreateUpperBodyMask()
        {
            if (m_VisualRoot == null)
                return null;

            Transform upperBodyRoot = FindChildTransform(m_VisualRoot, UpperBodyRootName);
            if (upperBodyRoot == null)
                return null;

            AvatarMask mask = new AvatarMask();
            SetHumanoidMask(mask);
            mask.AddTransformPath(upperBodyRoot, true);
            return mask;
        }

        void SetHumanoidMask(AvatarMask mask)
        {
            for (AvatarMaskBodyPart part = 0; part < AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive(part, false);

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
        }

        int GetControllerPlayableLayerIndex(string layerName)
        {
            if (!m_ControllerPlayable.IsValid() || string.IsNullOrEmpty(layerName))
                return -1;

            for (int i = 0; i < m_ControllerPlayable.GetLayerCount(); i++)
            {
                if (m_ControllerPlayable.GetLayerName(i) == layerName)
                    return i;
            }

            return -1;
        }

        Transform FindChildTransform(Transform root, string childName)
        {
            if (root == null)
                return null;

            if (root.name == childName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindChildTransform(root.GetChild(i), childName);
                if (result != null)
                    return result;
            }

            return null;
        }

        void SetAnimatorFloat(string parameterName, float value)
        {
            if (m_ControllerPlayable.IsValid())
                m_ControllerPlayable.SetFloat(parameterName, value);
            else
                Animator.SetFloat(parameterName, value, SpeedDampTime, Time.deltaTime);
        }

        void SetAnimatorFloatImmediate(string parameterName, float value)
        {
            if (m_ControllerPlayable.IsValid())
                m_ControllerPlayable.SetFloat(parameterName, value);
            else
                Animator.SetFloat(parameterName, value);
        }

        void SetAnimatorBool(string parameterName, bool value)
        {
            if (m_ControllerPlayable.IsValid())
                m_ControllerPlayable.SetBool(parameterName, value);
            else
                Animator.SetBool(parameterName, value);
        }

        void SetAnimatorTrigger(string parameterName)
        {
            if (m_ControllerPlayable.IsValid())
                m_ControllerPlayable.SetTrigger(parameterName);
            else
                Animator.SetTrigger(parameterName);
        }

        bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
        {
            if (Animator == null || string.IsNullOrEmpty(parameterName))
                return false;

            foreach (AnimatorControllerParameter parameter in Animator.parameters)
            {
                if (parameter.name == parameterName && parameter.type == parameterType)
                    return true;
            }

            return false;
        }

        void LockVisualTransform()
        {
            if (m_VisualRoot == null)
                return;

            Vector3 targetPosition = VisualLocalPosition;
            if (LockVisualHeight)
                targetPosition.y = VisualLocalPosition.y;

            m_VisualRoot.localPosition = targetPosition;
            m_VisualRoot.localRotation = Quaternion.Euler(VisualLocalEulerAngles);
            m_VisualRoot.localScale = VisualLocalScale;
        }
    }
}
