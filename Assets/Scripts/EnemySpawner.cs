using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.FPS.AI
{
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn")]
        public List<GameObject> EnemyPrefabs = new List<GameObject>();
        public List<Transform> SpawnPoints = new List<Transform>();
        public Transform SpawnedEnemiesParent;

        [Header("Rules")]
        public bool SpawnOnStart = true;
        public int SpawnCount = 3;
        public bool UseRandomEnemy = true;
        public bool UseRandomSpawnPoint = false;

        [Header("NavMesh")]
        public bool SnapSpawnPointToNavMesh = true;
        public float NavMeshSearchRadius = 5f;

        void Start()
        {
            CollectChildSpawnPointsIfNeeded();

            if (SpawnOnStart)
                SpawnEnemies();
        }

        public void SpawnEnemies()
        {
            CollectChildSpawnPointsIfNeeded();

            if (EnemyPrefabs.Count == 0 || SpawnPoints.Count == 0)
            {
                Debug.LogWarning("EnemySpawner needs at least one enemy prefab and one spawn point.", this);
                return;
            }

            for (int i = 0; i < SpawnCount; i++)
            {
                GameObject prefab = GetEnemyPrefab(i);
                Transform spawnPoint = GetSpawnPoint(i);

                if (prefab == null || spawnPoint == null)
                    continue;

                Vector3 spawnPosition = spawnPoint.position;
                if (SnapSpawnPointToNavMesh && !TryGetNavMeshPosition(prefab, spawnPosition, out spawnPosition))
                {
                    Debug.LogWarning($"No matching NavMesh found near spawn point '{spawnPoint.name}'. Enemy was not spawned.", spawnPoint);
                    continue;
                }

                GameObject spawnedEnemy = InstantiateInactive(prefab, spawnPosition, spawnPoint.rotation);

                if (SpawnedEnemiesParent != null)
                    spawnedEnemy.transform.SetParent(SpawnedEnemiesParent, true);

                spawnedEnemy.SetActive(true);
            }
        }

        GameObject InstantiateInactive(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            bool wasActive = prefab.activeSelf;

            try
            {
                if (wasActive)
                    prefab.SetActive(false);

                GameObject spawnedEnemy = Instantiate(prefab, position, rotation);
                spawnedEnemy.transform.position = position;
                spawnedEnemy.transform.rotation = rotation;

                return spawnedEnemy;
            }
            finally
            {
                if (wasActive)
                    prefab.SetActive(true);
            }
        }

        bool TryGetNavMeshPosition(GameObject prefab, Vector3 position, out Vector3 navMeshPosition)
        {
            NavMeshAgent prefabAgent = prefab.GetComponentInChildren<NavMeshAgent>(true);

            if (prefabAgent != null)
            {
                NavMeshQueryFilter filter = new NavMeshQueryFilter
                {
                    agentTypeID = prefabAgent.agentTypeID,
                    areaMask = NavMesh.AllAreas
                };

                if (NavMesh.SamplePosition(position, out NavMeshHit typedHit, NavMeshSearchRadius, filter))
                {
                    navMeshPosition = typedHit.position;
                    return true;
                }

                navMeshPosition = position;
                return false;
            }

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, NavMeshSearchRadius, NavMesh.AllAreas))
            {
                navMeshPosition = hit.position;
                return true;
            }

            navMeshPosition = position;
            return false;
        }

        GameObject GetEnemyPrefab(int index)
        {
            if (UseRandomEnemy)
                return EnemyPrefabs[Random.Range(0, EnemyPrefabs.Count)];

            return EnemyPrefabs[index % EnemyPrefabs.Count];
        }

        Transform GetSpawnPoint(int index)
        {
            if (UseRandomSpawnPoint)
                return SpawnPoints[Random.Range(0, SpawnPoints.Count)];

            return SpawnPoints[index % SpawnPoints.Count];
        }

        void CollectChildSpawnPointsIfNeeded()
        {
            if (SpawnPoints.Count > 0)
                return;

            foreach (Transform child in transform)
                SpawnPoints.Add(child);
        }
    }
}
