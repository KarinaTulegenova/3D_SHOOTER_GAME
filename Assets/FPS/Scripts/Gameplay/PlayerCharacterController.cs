using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Gameplay
{
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputHandler), typeof(AudioSource))]
    public class PlayerCharacterController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the main camera used for the player")]
        public Camera PlayerCamera;

        [Tooltip("Audio source for footsteps, jump, etc...")]
        public AudioSource AudioSource;

        [Header("General")]
        [Tooltip("Force applied downward when in the air")]
        public float GravityDownForce = 20f;

        [Tooltip("Physic layers checked to consider the player grounded")]
        public LayerMask GroundCheckLayers = -1;

        [Tooltip("distance from the bottom of the character controller capsule to test for grounded")]
        public float GroundCheckDistance = 0.05f;

        [Header("Movement")]
        [Tooltip("Max movement speed when grounded (when not sprinting)")]
        public float MaxSpeedOnGround = 10f;

        [Tooltip("Sharpness for the movement when grounded")]
        public float MovementSharpnessOnGround = 15;

        [Tooltip("Max movement speed when crouching")]
        [Range(0, 1)]
        public float MaxSpeedCrouchedRatio = 0.5f;

        [Tooltip("Max movement speed when not grounded")]
        public float MaxSpeedInAir = 10f;

        [Tooltip("Acceleration speed when in the air")]
        public float AccelerationSpeedInAir = 25f;

        [Tooltip("Multiplicator for the sprint speed")]
        public float SprintSpeedModifier = 2f;

        [Tooltip("Height at which the player dies instantly")]
        public float KillHeight = -50f;

        [Tooltip("Keep this enabled for normal gameplay")]
        public bool ForceVulnerable = true;

        [Header("Rotation")]
        [Tooltip("Rotation speed for moving the camera")]
        public float RotationSpeed = 200f;

        [Range(0.1f, 1f)]
        [Tooltip("Rotation speed multiplier when aiming")]
        public float AimingRotationMultiplier = 0.4f;

        [Header("Jump")]
        [Tooltip("Force applied upward when jumping")]
        public float JumpForce = 9f;

        [Header("Stance")]
        [Tooltip("Ratio (0-1) of the character height where the camera will be at")]
        public float CameraHeightRatio = 0.9f;

        [Tooltip("Height of character when standing")]
        public float CapsuleHeightStanding = 1.8f;

        [Tooltip("Height of character when crouching")]
        public float CapsuleHeightCrouching = 0.9f;

        [Tooltip("Speed of crouching transitions")]
        public float CrouchingSharpness = 10f;

        [Header("Audio")]
        [Tooltip("Amount of footstep sounds played when moving one meter")]
        public float FootstepSfxFrequency = 1f;

        [Tooltip("Amount of footstep sounds played when moving one meter while sprinting")]
        public float FootstepSfxFrequencyWhileSprinting = 1f;

        [Tooltip("Sound played for footsteps")]
        public AudioClip FootstepSfx;

        [Tooltip("Sound played when jumping")]
        public AudioClip JumpSfx;

        [Tooltip("Sound played when landing")]
        public AudioClip LandSfx;

        [Tooltip("Sound played when taking damage from a fall")]
        public AudioClip FallDamageSfx;

        [Header("Fall Damage")]
        [Tooltip("Whether the player will receive damage when hitting the ground at high speed")]
        public bool RecievesFallDamage;

        [Tooltip("Minimum fall speed for receiving fall damage")]
        public float MinSpeedForFallDamage = 10f;

        [Tooltip("Fall speed for receiving the maximum amount of fall damage")]
        public float MaxSpeedForFallDamage = 30f;

        [Tooltip("Damage received when falling at the minimum speed")]
        public float FallDamageAtMinSpeed = 10f;

        [Tooltip("Damage received when falling at the maximum speed")]
        public float FallDamageAtMaxSpeed = 50f;

        public UnityAction<bool> OnStanceChanged;

        public Vector3 CharacterVelocity { get; set; }
        public bool IsGrounded { get; private set; }
        public bool HasJumpedThisFrame { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsCrouching { get; private set; }

        public float RotationMultiplier
        {
            get
            {
                if (m_WeaponsManager.IsAiming)
                {
                    return AimingRotationMultiplier;
                }

                return 1f;
            }
        }

        Health m_Health;
        PlayerInputHandler m_InputHandler;
        CharacterController m_Controller;
        PlayerWeaponsManager m_WeaponsManager;
        Actor m_Actor;
        Rigidbody m_Rigidbody;
        Animator m_Animator;
        readonly Dictionary<string, AnimatorControllerParameterType> m_AnimatorParameters =
            new Dictionary<string, AnimatorControllerParameterType>();

        Vector3 m_GroundNormal;
        Vector3 m_CharacterVelocity;
        Vector3 m_LatestImpactSpeed;

        float m_LastTimeJumped = 0f;
        float m_CameraVerticalAngle = 0f;
        float m_FootstepDistanceCounter;
        float m_TargetCharacterHeight;

        const float k_JumpGroundingPreventionTime = 0.2f;
        const float k_GroundCheckDistanceInAir = 0.07f;
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
            m_WeaponsManager = GetComponent<PlayerWeaponsManager>();
            m_Health = GetComponent<Health>();
            m_Actor = GetComponent<Actor>();

            m_Animator = GetComponentInChildren<Animator>(true);
            CacheAnimatorParameters();

            MakePlayerVulnerable();

            m_Controller.enableOverlapRecovery = true;

            ConfigureRigidbody();

            m_Health.OnDie += OnDie;

            SetCrouchingState(false, true);
            UpdateCharacterHeight(true);
        }

        void ConfigureRigidbody()
        {
            m_Rigidbody = GetComponent<Rigidbody>();

            if (m_Rigidbody == null)
                m_Rigidbody = gameObject.AddComponent<Rigidbody>();

            m_Rigidbody.isKinematic = true;
            m_Rigidbody.useGravity = false;
            m_Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }

        void Update()
        {
            MakePlayerVulnerable();

            if (IsDead)
            {
                CharacterVelocity = Vector3.zero;
                return;
            }

            if (!IsDead && transform.position.y < KillHeight)
            {
                m_Health.Kill();
            }

            HasJumpedThisFrame = false;

            bool wasGrounded = IsGrounded;

            GroundCheck();

            if (IsGrounded && !wasGrounded)
            {
                float fallSpeed = -Mathf.Min(CharacterVelocity.y, m_LatestImpactSpeed.y);

                float fallSpeedRatio =
                    (fallSpeed - MinSpeedForFallDamage) /
                    (MaxSpeedForFallDamage - MinSpeedForFallDamage);

                if (RecievesFallDamage && fallSpeedRatio > 0f)
                {
                    float dmgFromFall =
                        Mathf.Lerp(FallDamageAtMinSpeed, FallDamageAtMaxSpeed, fallSpeedRatio);

                    m_Health.TakeDamage(dmgFromFall, null);

                    AudioSource.PlayOneShot(FallDamageSfx);
                }
                else
                {
                    AudioSource.PlayOneShot(LandSfx);
                }
            }

            if (m_InputHandler.GetCrouchInputDown())
            {
                SetCrouchingState(!IsCrouching, false);
            }

            UpdateCharacterHeight(false);

            HandleCharacterMovement();
        }

        void OnDie()
        {
            IsDead = true;
            CharacterVelocity = Vector3.zero;
            m_CharacterVelocity = Vector3.zero;

            if (m_WeaponsManager != null)
            {
                m_WeaponsManager.SwitchToWeaponIndex(-1, true);
                m_WeaponsManager.enabled = false;
            }

            if (m_InputHandler != null)
                m_InputHandler.enabled = false;

            EventManager.Broadcast(Events.PlayerDeathEvent);
        }

        void MakePlayerVulnerable()
        {
            if (IsDead || m_Health == null || m_Health.IsDead)
                return;

            if (ForceVulnerable)
                m_Health.Invincible = false;
        }

        void GroundCheck()
        {
            float chosenGroundCheckDistance =
                IsGrounded
                    ? (m_Controller.skinWidth + GroundCheckDistance)
                    : k_GroundCheckDistanceInAir;

            IsGrounded = false;
            m_GroundNormal = Vector3.up;

            if (Time.time >= m_LastTimeJumped + k_JumpGroundingPreventionTime)
            {
                if (Physics.CapsuleCast(
                    GetCapsuleBottomHemisphere(),
                    GetCapsuleTopHemisphere(m_Controller.height),
                    m_Controller.radius,
                    Vector3.down,
                    out RaycastHit hit,
                    chosenGroundCheckDistance,
                    GroundCheckLayers,
                    QueryTriggerInteraction.Ignore))
                {
                    m_GroundNormal = hit.normal;

                    if (Vector3.Dot(hit.normal, transform.up) > 0f &&
                        IsNormalUnderSlopeLimit(m_GroundNormal))
                    {
                        IsGrounded = true;

                        if (hit.distance > m_Controller.skinWidth)
                        {
                            m_Controller.Move(Vector3.down * hit.distance);
                        }
                    }
                }
            }
        }

        void HandleCharacterMovement()
        {
            transform.Rotate(
                new Vector3(
                    0f,
                    (m_InputHandler.GetLookInputsHorizontal() *
                     RotationSpeed *
                     RotationMultiplier),
                    0f),
                Space.Self);

            m_CameraVerticalAngle +=
                m_InputHandler.GetLookInputsVertical() *
                RotationSpeed *
                RotationMultiplier;

            m_CameraVerticalAngle =
                Mathf.Clamp(m_CameraVerticalAngle, -89f, 89f);

            PlayerCamera.transform.localEulerAngles =
                new Vector3(m_CameraVerticalAngle, 0, 0);

            bool isSprinting = m_InputHandler.GetSprintInputHeld();

            if (isSprinting)
            {
                isSprinting = SetCrouchingState(false, false);
            }

            float speedModifier = isSprinting ? SprintSpeedModifier : 1f;

            Vector3 worldspaceMoveInput =
                transform.TransformVector(m_InputHandler.GetMoveInput());

            if (IsGrounded)
            {
                Vector3 targetVelocity =
                    worldspaceMoveInput *
                    MaxSpeedOnGround *
                    speedModifier;

                if (IsCrouching)
                    targetVelocity *= MaxSpeedCrouchedRatio;

                targetVelocity =
                    GetDirectionReorientedOnSlope(
                        targetVelocity.normalized,
                        m_GroundNormal) *
                    targetVelocity.magnitude;

                CharacterVelocity = Vector3.Lerp(
                    CharacterVelocity,
                    targetVelocity,
                    MovementSharpnessOnGround * Time.deltaTime);

                if (worldspaceMoveInput.sqrMagnitude == 0f)
                {
                    CharacterVelocity =
                        new Vector3(0f, CharacterVelocity.y, 0f);

                    m_FootstepDistanceCounter = 0f;
                }

                if (IsGrounded && m_InputHandler.GetJumpInputDown())
                {
                    if (SetCrouchingState(false, false))
                    {
                        CharacterVelocity =
                            new Vector3(
                                CharacterVelocity.x,
                                0f,
                                CharacterVelocity.z);

                        CharacterVelocity += Vector3.up * JumpForce;

                        AudioSource.PlayOneShot(JumpSfx);

                        m_LastTimeJumped = Time.time;

                        HasJumpedThisFrame = true;

                        IsGrounded = false;
                        m_GroundNormal = Vector3.up;
                    }
                }

                float chosenFootstepSfxFrequency =
                    isSprinting
                        ? FootstepSfxFrequencyWhileSprinting
                        : FootstepSfxFrequency;

                if (m_FootstepDistanceCounter >=
                    1f / chosenFootstepSfxFrequency)
                {
                    m_FootstepDistanceCounter = 0f;
                    AudioSource.PlayOneShot(FootstepSfx);
                }

                m_FootstepDistanceCounter +=
                    CharacterVelocity.magnitude * Time.deltaTime;
            }
            else
            {
                CharacterVelocity +=
                    worldspaceMoveInput *
                    AccelerationSpeedInAir *
                    Time.deltaTime;

                float verticalVelocity = CharacterVelocity.y;

                Vector3 horizontalVelocity =
                    Vector3.ProjectOnPlane(CharacterVelocity, Vector3.up);

                if (worldspaceMoveInput.sqrMagnitude == 0f)
                    horizontalVelocity = Vector3.zero;
                else
                    horizontalVelocity =
                        Vector3.ClampMagnitude(
                            horizontalVelocity,
                            MaxSpeedInAir * speedModifier);

                CharacterVelocity =
                    horizontalVelocity +
                    (Vector3.up * verticalVelocity);

                CharacterVelocity +=
                    Vector3.down *
                    GravityDownForce *
                    Time.deltaTime;
            }

            Vector3 capsuleBottomBeforeMove =
                GetCapsuleBottomHemisphere();

            Vector3 capsuleTopBeforeMove =
                GetCapsuleTopHemisphere(m_Controller.height);

            m_Controller.Move(CharacterVelocity * Time.deltaTime);

            float horizontalSpeed =
                new Vector3(
                    CharacterVelocity.x,
                    0,
                    CharacterVelocity.z).magnitude;

            if (m_Animator != null)
            {
                Vector3 horizontalVelocity = m_Controller.velocity;
                horizontalVelocity.y = 0f;

                float speed = horizontalVelocity.magnitude;

                SetAnimatorFloat("Speed", speed);

                if (m_InputHandler.GetFireInputHeld())
                {
                    SetAnimatorTrigger("Shoot");
                }

                if (Input.GetKeyDown(KeyCode.R))
                {
                    SetAnimatorTrigger("Reload");
                }
            }

            m_LatestImpactSpeed = Vector3.zero;

            if (Physics.CapsuleCast(
                capsuleBottomBeforeMove,
                capsuleTopBeforeMove,
                m_Controller.radius,
                CharacterVelocity.normalized,
                out RaycastHit hit,
                CharacterVelocity.magnitude * Time.deltaTime,
                -1,
                QueryTriggerInteraction.Ignore))
            {
                m_LatestImpactSpeed = CharacterVelocity;

                CharacterVelocity =
                    Vector3.ProjectOnPlane(
                        CharacterVelocity,
                        hit.normal);
            }

            if (IsGrounded &&
                m_InputHandler.GetMoveInput().sqrMagnitude == 0f)
            {
                CharacterVelocity =
                    new Vector3(
                        0f,
                        CharacterVelocity.y,
                        0f);

                m_FootstepDistanceCounter = 0f;
            }
        }

        bool IsNormalUnderSlopeLimit(Vector3 normal)
        {
            return Vector3.Angle(transform.up, normal) <= m_Controller.slopeLimit;
        }

        Vector3 GetCapsuleBottomHemisphere()
        {
            return transform.position + (transform.up * m_Controller.radius);
        }

        Vector3 GetCapsuleTopHemisphere(float atHeight)
        {
            return transform.position +
                   (transform.up * (atHeight - m_Controller.radius));
        }

        public Vector3 GetDirectionReorientedOnSlope(
            Vector3 direction,
            Vector3 slopeNormal)
        {
            Vector3 directionRight =
                Vector3.Cross(direction, transform.up);

            return Vector3.Cross(slopeNormal, directionRight).normalized;
        }

        void UpdateCharacterHeight(bool force)
        {
            if (force)
            {
                m_Controller.height = m_TargetCharacterHeight;

                m_Controller.center =
                    Vector3.up * m_Controller.height * 0.5f;

                PlayerCamera.transform.localPosition =
                    Vector3.up *
                    m_TargetCharacterHeight *
                    CameraHeightRatio;

                m_Actor.AimPoint.transform.localPosition =
                    m_Controller.center;
            }
            else if (m_Controller.height != m_TargetCharacterHeight)
            {
                m_Controller.height =
                    Mathf.Lerp(
                        m_Controller.height,
                        m_TargetCharacterHeight,
                        CrouchingSharpness * Time.deltaTime);

                m_Controller.center =
                    Vector3.up * m_Controller.height * 0.5f;

                PlayerCamera.transform.localPosition =
                    Vector3.Lerp(
                        PlayerCamera.transform.localPosition,
                        Vector3.up *
                        m_TargetCharacterHeight *
                        CameraHeightRatio,
                        CrouchingSharpness * Time.deltaTime);

                m_Actor.AimPoint.transform.localPosition =
                    m_Controller.center;
            }
        }

        bool SetCrouchingState(bool crouched, bool ignoreObstructions)
        {
            if (crouched)
            {
                m_TargetCharacterHeight = CapsuleHeightCrouching;
            }
            else
            {
                if (!ignoreObstructions)
                {
                    Collider[] standingOverlaps =
                        Physics.OverlapCapsule(
                            GetCapsuleBottomHemisphere(),
                            GetCapsuleTopHemisphere(CapsuleHeightStanding),
                            m_Controller.radius,
                            -1,
                            QueryTriggerInteraction.Ignore);

                    foreach (Collider c in standingOverlaps)
                    {
                        if (c != m_Controller)
                        {
                            return false;
                        }
                    }
                }

                m_TargetCharacterHeight = CapsuleHeightStanding;
            }

            if (OnStanceChanged != null)
            {
                OnStanceChanged.Invoke(crouched);
            }

            IsCrouching = crouched;

            return true;
        }

        void CacheAnimatorParameters()
        {
            m_AnimatorParameters.Clear();

            if (m_Animator == null)
                return;

            foreach (AnimatorControllerParameter parameter in m_Animator.parameters)
                m_AnimatorParameters[parameter.name] = parameter.type;
        }

        void SetAnimatorFloat(string parameterName, float value)
        {
            if (m_Animator != null &&
                m_AnimatorParameters.TryGetValue(parameterName, out AnimatorControllerParameterType type) &&
                type == AnimatorControllerParameterType.Float)
            {
                m_Animator.SetFloat(parameterName, value);
            }
        }

        void SetAnimatorTrigger(string parameterName)
        {
            if (m_Animator != null &&
                m_AnimatorParameters.TryGetValue(parameterName, out AnimatorControllerParameterType type) &&
                type == AnimatorControllerParameterType.Trigger)
            {
                m_Animator.SetTrigger(parameterName);
            }
        }
    }
}
