using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class EnemyCounter : MonoBehaviour
    {
        [Header("Enemies")] [Tooltip("Text component for displaying enemy objective progress")]
        public Text EnemiesText;

        EnemyManager m_EnemyManager;

        void Awake()
        {
            m_EnemyManager = FindFirstObjectByType<EnemyManager>();
            DebugUtility.HandleErrorIfNullFindObject<EnemyManager, EnemyCounter>(m_EnemyManager, this);
            if (m_EnemyManager != null)
                m_EnemyManager.OnEnemyCountChanged += UpdateEnemyText;
        }

        void Start()
        {
            if (m_EnemyManager != null)
                UpdateEnemyText(m_EnemyManager.NumberOfEnemiesRemaining, m_EnemyManager.NumberOfEnemiesTotal);
        }

        void OnDestroy()
        {
            if (m_EnemyManager != null)
                m_EnemyManager.OnEnemyCountChanged -= UpdateEnemyText;
        }

        void UpdateEnemyText(int remaining, int total)
        {
            if (EnemiesText != null)
                EnemiesText.text = remaining + "/" + total;
        }
    }
}
