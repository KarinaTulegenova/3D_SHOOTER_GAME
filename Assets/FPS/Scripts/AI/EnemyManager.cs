using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.AI
{
    public class EnemyManager : MonoBehaviour
    {
        public List<EnemyController> Enemies { get; private set; }
        public int NumberOfEnemiesTotal { get; private set; }
        public int NumberOfEnemiesRemaining => (Enemies?.Count ?? 0) + (m_RegisteredEnemyObjects?.Count ?? 0);
        public UnityAction<int, int> OnEnemyCountChanged;

        HashSet<GameObject> m_RegisteredEnemyObjects;

        void Awake()
        {
            Enemies = new List<EnemyController>();
            m_RegisteredEnemyObjects = new HashSet<GameObject>();
        }

        public void RegisterEnemy(EnemyController enemy)
        {
            if (enemy == null)
                return;

            EnsureCollections();
            if (Enemies.Contains(enemy))
                return;

            Enemies.Add(enemy);

            NumberOfEnemiesTotal++;
            NotifyEnemyCountChanged();
        }

        public void RegisterEnemyObject(GameObject enemy)
        {
            EnsureCollections();
            if (enemy == null || !m_RegisteredEnemyObjects.Add(enemy))
                return;

            NumberOfEnemiesTotal++;
            NotifyEnemyCountChanged();
        }

        public void UnregisterEnemy(EnemyController enemyKilled)
        {
            EnsureCollections();
            if (enemyKilled == null || !Enemies.Contains(enemyKilled))
                return;

            int enemiesRemainingNotification = NumberOfEnemiesRemaining - 1;

            EnemyKillEvent evt = Events.EnemyKillEvent;
            evt.Enemy = enemyKilled.gameObject;
            evt.RemainingEnemyCount = enemiesRemainingNotification;
            EventManager.Broadcast(evt);

            // removes the enemy from the list, so that we can keep track of how many are left on the map
            Enemies.Remove(enemyKilled);
            NotifyEnemyCountChanged();
        }

        public void UnregisterEnemyObject(GameObject enemyKilled)
        {
            EnsureCollections();
            if (enemyKilled == null || !m_RegisteredEnemyObjects.Contains(enemyKilled))
                return;

            int enemiesRemainingNotification = NumberOfEnemiesRemaining - 1;

            EnemyKillEvent evt = Events.EnemyKillEvent;
            evt.Enemy = enemyKilled;
            evt.RemainingEnemyCount = enemiesRemainingNotification;
            EventManager.Broadcast(evt);

            m_RegisteredEnemyObjects.Remove(enemyKilled);
            NotifyEnemyCountChanged();
        }

        void NotifyEnemyCountChanged()
        {
            OnEnemyCountChanged?.Invoke(NumberOfEnemiesRemaining, NumberOfEnemiesTotal);
        }

        void EnsureCollections()
        {
            if (Enemies == null)
                Enemies = new List<EnemyController>();

            if (m_RegisteredEnemyObjects == null)
                m_RegisteredEnemyObjects = new HashSet<GameObject>();
        }
    }
}
