using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AI
{
    [RequireComponent(typeof(EnemyController))]
    public class EnemyMobile : MonoBehaviour
    {
        public enum AIState
        {
            Patrol,
            Follow,
            Attack,
        }

        public Animator Animator;

        [Tooltip("Fraction of the enemy's attack range at which it will stop moving towards target while attacking")]
        [Range(0f, 1f)]
        public float AttackStopDistanceRatio = 0.5f;

        [Tooltip("The random hit damage effects")]
        public ParticleSystem[] RandomHitSparks;

        public ParticleSystem[] OnDetectVfx;
        public AudioClip OnDetectSfx;

        [Header("Sound")] public AudioClip MovementSound;
        public MinMaxFloat PitchDistortionMovementSpeed;

        public AIState AiState { get; private set; }
        EnemyController m_EnemyController;
        AudioSource m_AudioSource;
        readonly Dictionary<string, AnimatorControllerParameterType> m_AnimatorParameters =
            new Dictionary<string, AnimatorControllerParameterType>();

        const string k_AnimMoveSpeedParameter = "MoveSpeed";
        const string k_AnimAttackParameter = "Attack";
        const string k_AnimAlertedParameter = "Alerted";
        const string k_AnimOnDamagedParameter = "OnDamaged";

        void Start()
        {
            m_EnemyController = GetComponent<EnemyController>();
            DebugUtility.HandleErrorIfNullGetComponent<EnemyController, EnemyMobile>(m_EnemyController, this,
                gameObject);

            m_EnemyController.onAttack += OnAttack;
            m_EnemyController.onDetectedTarget += OnDetectedTarget;
            m_EnemyController.onLostTarget += OnLostTarget;
            m_EnemyController.SetPathDestinationToClosestNode();
            m_EnemyController.onDamaged += OnDamaged;

            // Start patrolling
            AiState = AIState.Patrol;

            // adding a audio source to play the movement sound on it
            m_AudioSource = GetComponent<AudioSource>();
            DebugUtility.HandleErrorIfNullGetComponent<AudioSource, EnemyMobile>(m_AudioSource, this, gameObject);
            m_AudioSource.clip = MovementSound;
            if (m_AudioSource.clip != null)
                m_AudioSource.Play();

            CacheAnimatorParameters();
        }

        void Update()
        {
            UpdateAiStateTransitions();
            UpdateCurrentAiState();

            float moveSpeed = m_EnemyController.NavMeshAgent.velocity.magnitude;

            // Update animator speed parameter
            SetAnimatorFloat(k_AnimMoveSpeedParameter, moveSpeed);

            // changing the pitch of the movement sound depending on the movement speed
            if (m_AudioSource != null && m_EnemyController.NavMeshAgent.speed > 0f)
            {
                m_AudioSource.pitch = Mathf.Lerp(PitchDistortionMovementSpeed.Min, PitchDistortionMovementSpeed.Max,
                    moveSpeed / m_EnemyController.NavMeshAgent.speed);
            }
        }

        void UpdateAiStateTransitions()
        {
            // Handle transitions 
            switch (AiState)
            {
                case AIState.Follow:
                    // Transition to attack when there is a line of sight to the target
                    if (m_EnemyController.IsSeeingTarget && m_EnemyController.IsTargetInAttackRange)
                    {
                        AiState = AIState.Attack;
                        m_EnemyController.SetNavDestination(transform.position);
                    }

                    break;
                case AIState.Attack:
                    // Transition to follow when no longer a target in attack range
                    if (!m_EnemyController.IsTargetInAttackRange)
                    {
                        AiState = AIState.Follow;
                    }

                    break;
            }
        }

        void UpdateCurrentAiState()
        {
            // Handle logic 
            switch (AiState)
            {
                case AIState.Patrol:
                    m_EnemyController.UpdatePathDestination();
                    m_EnemyController.SetNavDestination(m_EnemyController.GetDestinationOnPath());
                    break;
                case AIState.Follow:
                    if (m_EnemyController.KnownDetectedTarget == null)
                    {
                        AiState = AIState.Patrol;
                        break;
                    }

                    m_EnemyController.SetNavDestination(m_EnemyController.KnownDetectedTarget.transform.position);
                    m_EnemyController.OrientTowards(m_EnemyController.KnownDetectedTarget.transform.position);
                    m_EnemyController.OrientWeaponsTowards(m_EnemyController.KnownDetectedTarget.transform.position);
                    break;
                case AIState.Attack:
                    if (m_EnemyController.KnownDetectedTarget == null || m_EnemyController.DetectionModule == null)
                    {
                        AiState = AIState.Patrol;
                        break;
                    }

                    Transform detectionSourcePoint = m_EnemyController.DetectionModule.DetectionSourcePoint != null
                        ? m_EnemyController.DetectionModule.DetectionSourcePoint
                        : transform;

                    if (Vector3.Distance(m_EnemyController.KnownDetectedTarget.transform.position,
                            detectionSourcePoint.position)
                        >= (AttackStopDistanceRatio * m_EnemyController.DetectionModule.AttackRange))
                    {
                        m_EnemyController.SetNavDestination(m_EnemyController.KnownDetectedTarget.transform.position);
                    }
                    else
                    {
                        m_EnemyController.SetNavDestination(transform.position);
                    }

                    m_EnemyController.OrientTowards(m_EnemyController.KnownDetectedTarget.transform.position);
                    m_EnemyController.TryAtack(m_EnemyController.KnownDetectedTarget.transform.position);
                    break;
            }
        }

        void OnAttack()
        {
            SetAnimatorTrigger(k_AnimAttackParameter);
        }

        void OnDetectedTarget()
        {
            if (AiState == AIState.Patrol)
            {
                AiState = AIState.Follow;
            }

            for (int i = 0; i < OnDetectVfx.Length; i++)
            {
                OnDetectVfx[i].Play();
            }

            if (OnDetectSfx)
            {
                AudioUtility.CreateSFX(OnDetectSfx, transform.position, AudioUtility.AudioGroups.EnemyDetection, 1f);
            }

            SetAnimatorBool(k_AnimAlertedParameter, true);
        }

        void OnLostTarget()
        {
            if (AiState == AIState.Follow || AiState == AIState.Attack)
            {
                AiState = AIState.Patrol;
            }

            for (int i = 0; i < OnDetectVfx.Length; i++)
            {
                OnDetectVfx[i].Stop();
            }

            SetAnimatorBool(k_AnimAlertedParameter, false);
        }

        void OnDamaged()
        {
            if (RandomHitSparks.Length > 0)
            {
                int n = Random.Range(0, RandomHitSparks.Length - 1);
                RandomHitSparks[n].Play();
            }

            SetAnimatorTrigger(k_AnimOnDamagedParameter);
        }

        void CacheAnimatorParameters()
        {
            if (Animator == null)
                Animator = GetComponentInChildren<Animator>();

            if (Animator == null)
                return;

            foreach (AnimatorControllerParameter parameter in Animator.parameters)
                m_AnimatorParameters[parameter.name] = parameter.type;
        }

        void SetAnimatorFloat(string parameterName, float value)
        {
            if (Animator != null &&
                m_AnimatorParameters.TryGetValue(parameterName, out AnimatorControllerParameterType type) &&
                type == AnimatorControllerParameterType.Float)
            {
                Animator.SetFloat(parameterName, value);
            }
        }

        void SetAnimatorBool(string parameterName, bool value)
        {
            if (Animator != null &&
                m_AnimatorParameters.TryGetValue(parameterName, out AnimatorControllerParameterType type) &&
                type == AnimatorControllerParameterType.Bool)
            {
                Animator.SetBool(parameterName, value);
            }
        }

        void SetAnimatorTrigger(string parameterName)
        {
            if (Animator != null &&
                m_AnimatorParameters.TryGetValue(parameterName, out AnimatorControllerParameterType type) &&
                type == AnimatorControllerParameterType.Trigger)
            {
                Animator.SetTrigger(parameterName);
            }
        }
    }
}
