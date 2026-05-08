using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.FPS.AI
{
    [RequireComponent(typeof(Health), typeof(Actor), typeof(NavMeshAgent))]
    [RequireComponent(typeof(Damageable), typeof(CapsuleCollider))]
    public class ZombieEnemy : MonoBehaviour
    {
        [Header("Movement")]
        public float DetectionRange = 25f;
        public float AttackRange = 1.8f;
        public float MoveSpeed = 3.5f;
        public float OrientationSpeed = 12f;
        public float SelfDestructYHeight = -20f;

        [Header("Attack")]
        public float Damage = 15f;
        public float AttackCooldown = 1.2f;
        public float AttackDamageDelay = 0.35f;

        [Header("Debug")]
        [SerializeField] float m_CurrentHealth;

        [Header("Animation")]
        public Animator Animator;
        public string MoveSpeedParameter = "MoveSpeed";
        public string AttackTrigger = "Attack";
        public string DamagedTrigger = "OnDamaged";
        public string DeathTrigger = "Dead";

        [Header("VFX")]
        public GameObject DeathVfx;
        public Transform DeathVfxSpawnPoint;

        [Header("Audio")]
        public AudioClip DeathSfx;

        public float DestroyDelay = 0.2f;

        EnemyManager m_EnemyManager;
        ActorsManager m_ActorsManager;
        Health m_Health;
        Actor m_Actor;
        NavMeshAgent m_NavMeshAgent;
        Actor m_Target;
        float m_LastAttackTime = Mathf.NegativeInfinity;
        bool m_IsDead;
        Coroutine m_AttackDamageCoroutine;

        readonly Dictionary<string, AnimatorControllerParameterType> m_AnimatorParameters =
            new Dictionary<string, AnimatorControllerParameterType>();

        void Start()
        {
            m_EnemyManager = FindAnyObjectByType<EnemyManager>();
            DebugUtility.HandleErrorIfNullFindObject<EnemyManager, ZombieEnemy>(m_EnemyManager, this);
            m_EnemyManager.RegisterEnemyObject(gameObject);

            m_ActorsManager = FindAnyObjectByType<ActorsManager>();
            DebugUtility.HandleErrorIfNullFindObject<ActorsManager, ZombieEnemy>(m_ActorsManager, this);

            m_Health = GetComponent<Health>();
            m_Health.OnDie += OnDie;
            m_Health.OnDamaged += OnDamaged;

            m_Actor = GetComponent<Actor>();
            EnsureAimPoint();
            m_NavMeshAgent = GetComponent<NavMeshAgent>();
            m_NavMeshAgent.speed = MoveSpeed;
            m_NavMeshAgent.stoppingDistance = AttackRange * 0.8f;

            if (Animator == null)
                Animator = GetComponentInChildren<Animator>();

            if (Animator != null)
                Animator.applyRootMotion = false;

            ConfigureCapsuleCollider();
            CacheAnimatorParameters();
        }

        void Update()
        {
            if (m_IsDead)
                return;

            if (transform.position.y < SelfDestructYHeight)
            {
                m_Health.Kill();
                return;
            }

            m_Target = FindClosestHostileActor();
            m_CurrentHealth = m_Health.CurrentHealth;

            if (m_Target == null)
            {
                if (m_NavMeshAgent.isOnNavMesh)
                    m_NavMeshAgent.ResetPath();

                SetAnimatorFloat(MoveSpeedParameter, 0f);
                return;
            }

            Vector3 targetPosition = m_Target.transform.position;
            float distanceToTarget = GetDistanceToTarget(m_Target);

            OrientTowards(targetPosition);

            if (distanceToTarget > AttackRange)
            {
                if (m_NavMeshAgent.isOnNavMesh)
                    m_NavMeshAgent.SetDestination(targetPosition);
            }
            else
            {
                if (m_NavMeshAgent.isOnNavMesh)
                    m_NavMeshAgent.ResetPath();

                TryAttack();
            }

            SetAnimatorFloat(MoveSpeedParameter, m_NavMeshAgent.velocity.magnitude);
        }

        Actor FindClosestHostileActor()
        {
            Actor closestActor = null;
            float closestSqrDistance = DetectionRange <= 0f ? Mathf.Infinity : DetectionRange * DetectionRange;

            foreach (Actor otherActor in m_ActorsManager.Actors)
            {
                if (otherActor == null || otherActor.Affiliation == m_Actor.Affiliation)
                    continue;

                float sqrDistance = (otherActor.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance <= closestSqrDistance)
                {
                    closestActor = otherActor;
                    closestSqrDistance = sqrDistance;
                }
            }

            return closestActor;
        }

        void TryAttack()
        {
            if (Time.time < m_LastAttackTime + AttackCooldown)
                return;

            m_LastAttackTime = Time.time;
            SetAnimatorTrigger(AttackTrigger);

            if (m_AttackDamageCoroutine != null)
                StopCoroutine(m_AttackDamageCoroutine);

            m_AttackDamageCoroutine = StartCoroutine(ApplyAttackDamageAfterDelay(m_Target));
        }

        IEnumerator ApplyAttackDamageAfterDelay(Actor target)
        {
            if (AttackDamageDelay > 0f)
                yield return new WaitForSeconds(AttackDamageDelay);

            if (!m_IsDead && target != null)
            {
                float distanceToTarget = GetDistanceToTarget(target);
                if (distanceToTarget <= AttackRange + 0.35f)
                {
                    // Melee hit uses Zombie damage directly. EnemyContactDamage handles bump damage with its own cooldown.
                    Health targetHealth = target.GetComponentInParent<Health>();
                    if (targetHealth != null && !targetHealth.IsDead)
                        targetHealth.TakeDamage(Damage, gameObject);
                }
            }

            m_AttackDamageCoroutine = null;
        }

        float GetDistanceToTarget(Actor target)
        {
            if (target == null)
                return Mathf.Infinity;

            Collider targetCollider = target.GetComponentInParent<Collider>();
            if (targetCollider != null)
                return Vector3.Distance(transform.position, targetCollider.ClosestPoint(transform.position));

            return Vector3.Distance(transform.position, target.transform.position);
        }

        void OrientTowards(Vector3 lookPosition)
        {
            Vector3 lookDirection = Vector3.ProjectOnPlane(lookPosition - transform.position, Vector3.up).normalized;
            if (lookDirection.sqrMagnitude == 0f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * OrientationSpeed);
        }

        void OnDamaged(float damage, GameObject damageSource)
        {
            SetAnimatorTrigger(DamagedTrigger);
        }

        void OnDie()
        {
            if (m_IsDead)
                return;

            m_IsDead = true;
            if (m_AttackDamageCoroutine != null)
            {
                StopCoroutine(m_AttackDamageCoroutine);
                m_AttackDamageCoroutine = null;
            }

            m_EnemyManager.UnregisterEnemyObject(gameObject);
            SetAnimatorTrigger(DeathTrigger);

            if (DeathVfx != null)
            {
                Transform spawnPoint = DeathVfxSpawnPoint != null ? DeathVfxSpawnPoint : transform;
                GameObject vfx = Instantiate(DeathVfx, spawnPoint.position, Quaternion.identity);
                Destroy(vfx, 5f);
            }

            if (DeathSfx != null)
                AudioUtility.CreateSFX(DeathSfx, transform.position, AudioUtility.AudioGroups.Impact, 1f);

            Destroy(gameObject, DestroyDelay);
        }

        void OnDestroy()
        {
            if (!m_IsDead && m_EnemyManager != null)
                m_EnemyManager.UnregisterEnemyObject(gameObject);
        }

        void CacheAnimatorParameters()
        {
            if (Animator == null)
                return;

            foreach (AnimatorControllerParameter parameter in Animator.parameters)
                m_AnimatorParameters[parameter.name] = parameter.type;
        }

        void EnsureAimPoint()
        {
            if (m_Actor.AimPoint != null)
                return;

            GameObject aimPoint = new GameObject("AimPoint");
            aimPoint.transform.SetParent(transform);
            aimPoint.transform.localPosition = Vector3.up * 1.5f;
            m_Actor.AimPoint = aimPoint.transform;
        }

        void ConfigureCapsuleCollider()
        {
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule == null)
                return;

            capsule.isTrigger = false;
            capsule.direction = 1;

            if (capsule.height < 2.4f)
                capsule.height = 2.4f;

            if (capsule.radius < 0.75f)
                capsule.radius = 0.75f;

            capsule.center = new Vector3(0f, 1.2f, 0f);
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
