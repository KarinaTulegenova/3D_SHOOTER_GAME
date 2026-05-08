using UnityEngine;
using UnityEngine.UI;
using Unity.FPS.AI;

namespace Unity.FPS.Game
{
    public class ScoreManager : MonoBehaviour
    {
        [Header("Score")]
        public int PointsPerEnemy = 1;
        public bool ShowEnemyCounter = true;
        public bool ShowCameraHint = true;
        public GameObject ScoreObject;
        public Text ScoreText;

        [Header("Debug")]
        [SerializeField] int m_CurrentScore;

        public int Score { get; private set; }

        EnemyManager m_EnemyManager;

        void Awake()
        {
            FindScoreTextIfNeeded();
            FindEnemyManagerIfNeeded();
            UpdateScoreText();
        }

        void OnEnable()
        {
            EventManager.AddListener<EnemyKillEvent>(OnEnemyKilled);
            FindScoreTextIfNeeded();
            FindEnemyManagerIfNeeded();
            if (m_EnemyManager != null)
                m_EnemyManager.OnEnemyCountChanged += OnEnemyCountChanged;
            UpdateScoreText();
        }

        void OnDisable()
        {
            EventManager.RemoveListener<EnemyKillEvent>(OnEnemyKilled);
            if (m_EnemyManager != null)
                m_EnemyManager.OnEnemyCountChanged -= OnEnemyCountChanged;
        }

        void OnEnemyKilled(EnemyKillEvent evt)
        {
            Score += PointsPerEnemy;
            m_CurrentScore = Score;
            UpdateScoreText();
        }

        void OnEnemyCountChanged(int remaining, int total)
        {
            UpdateScoreText();
        }

        public void ResetScore()
        {
            Score = 0;
            m_CurrentScore = Score;
            UpdateScoreText();
        }

        void FindScoreTextIfNeeded()
        {
            if (ScoreText != null)
                return;

            ScoreText = null;

            if (ScoreObject == null)
            {
                GameObject foundObject = GameObject.Find("Score");
                if (foundObject != null)
                    ScoreObject = foundObject;
            }

            if (ScoreObject == null)
                return;

            ScoreText = ScoreObject.GetComponent<Text>();
        }

        void UpdateScoreText()
        {
            FindScoreTextIfNeeded();

            string scoreValue = "Score: " + Score;
            if (ShowEnemyCounter)
            {
                FindEnemyManagerIfNeeded();
                if (m_EnemyManager != null)
                {
                    scoreValue += "\nEnemies: " + m_EnemyManager.NumberOfEnemiesRemaining + "/" +
                                  m_EnemyManager.NumberOfEnemiesTotal;
                }
            }

            if (ShowCameraHint)
                scoreValue += "\nV: Camera";

            if (ScoreText != null)
                ScoreText.text = scoreValue;
        }

        void FindEnemyManagerIfNeeded()
        {
            if (m_EnemyManager != null)
                return;

            m_EnemyManager = FindFirstObjectByType<EnemyManager>();
        }
    }
}
