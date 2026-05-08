using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class PlayerHealthBar : MonoBehaviour
    {
        [Tooltip("Image component dispplaying current health")]
        public Image HealthFillImage;

        Health m_PlayerHealth;

        void Start()
        {
            m_PlayerHealth = FindPlayerHealth();
            DebugUtility.HandleErrorIfNullFindObject<Health, PlayerHealthBar>(m_PlayerHealth, this);
            if (m_PlayerHealth != null)
            {
                m_PlayerHealth.OnDamaged += OnHealthDamaged;
                m_PlayerHealth.OnHealed += OnHealthHealed;
                UpdateHealthBar();
            }
        }

        Health FindPlayerHealth()
        {
            Health tpsHealth = FindHealthOnController("TpsPlayerController");
            if (tpsHealth != null)
                return tpsHealth;

            PlayerCharacterController fpsPlayer = GameObject.FindFirstObjectByType<PlayerCharacterController>();
            if (fpsPlayer != null)
            {
                Health fpsHealth = fpsPlayer.GetComponent<Health>();
                if (fpsHealth != null)
                    return fpsHealth;
            }

            ActorsManager actorsManager = GameObject.FindFirstObjectByType<ActorsManager>();
            if (actorsManager != null && actorsManager.Player != null)
                return actorsManager.Player.GetComponent<Health>();

            return null;
        }

        Health FindHealthOnController(string controllerTypeName)
        {
            MonoBehaviour[] behaviours = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null || behaviour.GetType().Name != controllerTypeName)
                    continue;

                Health health = behaviour.GetComponent<Health>();
                if (health != null)
                    return health;
            }

            return null;
        }

        void OnDestroy()
        {
            if (m_PlayerHealth != null)
            {
                m_PlayerHealth.OnDamaged -= OnHealthDamaged;
                m_PlayerHealth.OnHealed -= OnHealthHealed;
            }
        }

        void OnHealthDamaged(float amount, GameObject source)
        {
            UpdateHealthBar();
        }

        void OnHealthHealed(float amount)
        {
            UpdateHealthBar();
        }

        void UpdateHealthBar()
        {
            if (HealthFillImage != null && m_PlayerHealth != null)
                HealthFillImage.fillAmount = m_PlayerHealth.CurrentHealth / m_PlayerHealth.MaxHealth;
        }
    }
}
