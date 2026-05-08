using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.AI
{
    public class EnemyContactDamage : MonoBehaviour, IContactDamageDealer
    {
        [Header("Contact")]
        public float ContactRange = 1.8f;
        public float Damage = 25f;
        public float DamageCooldown = 1f;
        public bool KillPlayerInstantly = false;

        [Header("Audio")]
        public AudioClip ContactSfx;

        Transform m_Player;
        Health m_PlayerHealth;
        Collider m_PlayerCollider;
        float m_LastDamageTime = Mathf.NegativeInfinity;

        void Start()
        {
            CachePlayerReferences();
        }

        void CachePlayerReferences()
        {
            GameObject playerObject = null;

            TpsPlayerController tpsPlayer = FindFirstObjectByType<TpsPlayerController>();
            if (tpsPlayer != null)
                playerObject = tpsPlayer.gameObject;

            if (playerObject == null)
            {
                PlayerCharacterController fpsPlayer = FindFirstObjectByType<PlayerCharacterController>();
                if (fpsPlayer != null)
                    playerObject = fpsPlayer.gameObject;
            }

            if (playerObject == null)
            {
                ActorsManager actorsManager = FindFirstObjectByType<ActorsManager>();
                if (actorsManager != null)
                    playerObject = actorsManager.Player;
            }

            if (playerObject == null)
            {
                m_Player = null;
                m_PlayerHealth = null;
                m_PlayerCollider = null;
                return;
            }

            m_Player = playerObject.transform;
            m_PlayerHealth = playerObject.GetComponent<Health>();
            m_PlayerCollider = playerObject.GetComponent<Collider>();
        }

        void Update()
        {
            if (m_PlayerHealth == null)
                CachePlayerReferences();

            TryDamagePlayerInRange();
        }

        void OnCollisionEnter(Collision collision)
        {
            TryDamagePlayerFromObject(collision.gameObject);
        }

        void OnCollisionStay(Collision collision)
        {
            TryDamagePlayerFromObject(collision.gameObject);
        }

        void OnTriggerEnter(Collider other)
        {
            TryDamagePlayerFromObject(other.gameObject);
        }

        void OnTriggerStay(Collider other)
        {
            TryDamagePlayerFromObject(other.gameObject);
        }

        void TryDamagePlayerFromObject(GameObject other)
        {
            if (!IsPlayerObject(other))
                return;

            TryDamagePlayer();
        }

        bool IsPlayerObject(GameObject other)
        {
            if (other == null)
                return false;

            if (other.GetComponentInParent<TpsPlayerController>() != null)
                return true;

            if (other.GetComponentInParent<PlayerCharacterController>() != null)
                return true;

            if (m_Player != null && other.transform.IsChildOf(m_Player))
                return true;

            Actor actor = other.GetComponentInParent<Actor>();
            return actor != null && m_Player != null && actor.transform == m_Player;
        }

        public bool TryDamagePlayerInRange()
        {
            if (!IsPlayerInContactRange())
                return false;

            return TryDamagePlayer();
        }

        public bool TryDamagePlayer()
        {
            if (m_PlayerHealth == null)
                CachePlayerReferences();

            if (m_PlayerHealth == null || m_PlayerHealth.IsDead)
                return false;

            if (m_PlayerHealth.Invincible)
                return false;

            if (Time.time < m_LastDamageTime + DamageCooldown)
                return false;

            if (ContactSfx != null)
                AudioUtility.CreateSFX(ContactSfx, transform.position, AudioUtility.AudioGroups.EnemyAttack, 1f);

            if (KillPlayerInstantly)
                m_PlayerHealth.Kill();
            else
                m_PlayerHealth.TakeDamage(Damage, gameObject);

            m_LastDamageTime = Time.time;

            return true;
        }

        bool IsPlayerInContactRange()
        {
            if (m_Player == null || m_PlayerHealth == null)
                return false;

            float distanceToPlayer = m_PlayerCollider != null
                ? Vector3.Distance(transform.position, m_PlayerCollider.ClosestPoint(transform.position))
                : Vector3.Distance(transform.position, m_Player.position);

            return distanceToPlayer <= ContactRange;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, ContactRange);
        }
    }
}
