using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum AgentRole { Scout, Defender, Worker }
public enum SpawnFormation { Circle, Grid, Line, Random}

[System.Serializable]
public class AgentConfiguration
{
    [Header("Role Identity")]
    public AgentRole role;
    public Color debugColor = Color.white;

    [Header("Movement Settings")]
    [Range(1f, 10f)] public float speed = 3.5f;
    [Range(0.1f, 5f)] public float stoppingDistance = 0.5f;
    [Range(0f, 1f)] public float angularSpeed = 120f;

    [Header("Patrol Behavior")]
    [Tooltip("Maximum distance from spawn this agent will patrol")]
    public float patrolRadius = 10f;

    [Tooltip("Chance to ignore role behavior and pick randomly")]
    [Range(0f, 1f)] public float randomnessFactor = 0.2f;

    [Tooltip("Seconds to wait at each point")]
    [Range(0f, 10f)] public float waitTimeAtPoint = 1f;

    [Tooltip("Should avoid points recently visited by others?")]
    public bool avoidRecentlyVisited = true;

    [Tooltip("How important visit recency is vs distance (0-1)")]
    [Range(0f, 1f)] public float visitRecencyWeight = 0.7f;
}

public class SwarmIntelligence : MonoBehaviour
{
    public static SwarmIntelligence Instance;

    [Header("Agent Settings")]
    public GameObject agentPrefab;
    public List<AgentConfiguration> roleConfigurations = new List<AgentConfiguration>();

    [Header("Spawn Settings")]
    public List<Transform> spawnPoints = new List<Transform>();
    public float spawnRadius = 2f;
    public int agentsPerSpawnPoint = 3;
    public SpawnFormation formationType = SpawnFormation.Circle;
    public float gridSpacing = 2f;
    public Vector3 lineDirection = Vector3.forward;

    [Header("Patrol Points")]
    [SerializeField] private List<Transform> _patrolPoints = new List<Transform>();
    public IReadOnlyList<Transform> PatrolPoints => _patrolPoints.AsReadOnly();

    private List<GameObject> activeAgents = new List<GameObject>();
    private Dictionary<Transform, GameObject> pointAssignments = new Dictionary<Transform, GameObject>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeDefaultConfigurations();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeDefaultConfigurations()
    {
        if (roleConfigurations.Count == 0)
        {
            roleConfigurations = new List<AgentConfiguration>
            {
                new AgentConfiguration {
                    role = AgentRole.Worker,
                    debugColor = Color.blue,
                    speed = 3.5f,
                    patrolRadius = 15f,
                    visitRecencyWeight = 0.7f
                },
                new AgentConfiguration {
                    role = AgentRole.Scout,
                    debugColor = Color.green,
                    speed = 5f,
                    patrolRadius = 30f,
                    randomnessFactor = 0.3f
                },
                new AgentConfiguration {
                    role = AgentRole.Defender,
                    debugColor = Color.red,
                    speed = 4f,
                    patrolRadius = 8f,
                    avoidRecentlyVisited = false
                }
            };
        }
    }

    private void Start()
    {
        ValidatePointLists();
        SpawnAgentsUniformly();
    }

    private void ValidatePointLists()
    {
        // Remove null entries
        _patrolPoints.RemoveAll(point => point == null);
        spawnPoints.RemoveAll(point => point == null);

        // Check for duplicates
        foreach (var spawnPoint in spawnPoints)
        {
            if (_patrolPoints.Contains(spawnPoint))
            {
                Debug.LogWarning($"Spawn point {spawnPoint.name} is also a patrol point", spawnPoint);
            }
        }
    }

    public void AddPatrolPoint(Transform point)
    {
        if (!_patrolPoints.Contains(point))
        {
            _patrolPoints.Add(point);
        }
    }

    public void RemovePatrolPoint(Transform point)
    {
        if (_patrolPoints.Contains(point))
        {
            _patrolPoints.Remove(point);
            ReleasePoint(point);
        }
    }

    public AgentConfiguration GetConfigForRole(AgentRole role)
    {
        return roleConfigurations.Find(c => c.role == role);
    }

    private void SpawnAgentsUniformly()
    {
        if (agentPrefab == null || spawnPoints.Count == 0) return;

        foreach (Transform spawnPoint in spawnPoints)
        {
            for (int i = 0; i < agentsPerSpawnPoint; i++)
            {
                Vector3 spawnPos = GetSpawnPosition(spawnPoint.position, i, agentsPerSpawnPoint);
                AgentRole role = (AgentRole)(i % 3);
                SpawnAgentAtPosition(spawnPos, role, spawnPoint.position);
            }
        }
    }

    private Vector3 GetSpawnPosition(Vector3 center, int index, int total)
    {
        switch (formationType)
        {
            case SpawnFormation.Grid:
                int perRow = Mathf.CeilToInt(Mathf.Sqrt(total));
                int row = index / perRow;
                int col = index % perRow;
                return GetNavMeshPosition(center + new Vector3(
                    (col - perRow / 2f) * gridSpacing,
                    0,
                    (row - perRow / 2f) * gridSpacing));

            case SpawnFormation.Line:
                return GetNavMeshPosition(center + lineDirection.normalized * index * gridSpacing);

            case SpawnFormation.Random:
                return GetNavMeshPosition(center + Random.insideUnitSphere * spawnRadius);

            default: // Circle
                float angle = index * Mathf.PI * 2f / total;
                return GetNavMeshPosition(center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * spawnRadius);
        }
    }

    private Vector3 GetNavMeshPosition(Vector3 position)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(position, out hit, spawnRadius * 2, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return position;
    }

    private void SpawnAgentAtPosition(Vector3 position, AgentRole role, Vector3 spawnPos)
    {
        GameObject agent = Instantiate(agentPrefab, position, Quaternion.identity);
        var swarmAgent = agent.GetComponent<SwarmAgent>();
        swarmAgent.Initialize(role, spawnPos);
        activeAgents.Add(agent);
    }

    public Transform GetRoleAppropriateDestination(AgentRole role, Vector3 currentPos, Vector3 spawnPos)
    {
        if (_patrolPoints.Count == 0) return null;

        switch (role)
        {
            case AgentRole.Worker:
                return GetOptimalPatrolPoint(currentPos, spawnPos);

            case AgentRole.Scout:
                return GetFarthestPatrolPoint(currentPos, spawnPos);

            case AgentRole.Defender:
                return GetDefenderDestination(currentPos, spawnPos);

            default:
                return GetRandomPatrolPoint();
        }
    }

    private Transform GetOptimalPatrolPoint(Vector3 currentPos, Vector3 spawnPos)
    {
        AgentConfiguration config = GetConfigForRole(AgentRole.Worker);
        Transform bestPoint = null;
        float bestScore = -Mathf.Infinity;

        foreach (Transform point in _patrolPoints)
        {
            // Skip if point is assigned to someone else
            if (config.avoidRecentlyVisited && pointAssignments.ContainsKey(point))
                continue;

            // Skip if beyond patrol radius
            if (Vector3.Distance(point.position, spawnPos) > config.patrolRadius)
                continue;

            PatrolPoint pp = point.GetComponent<PatrolPoint>();
            float timeSinceVisit = Time.time - pp.lastVisitTime;
            float distanceScore = 1f / (1f + Vector3.Distance(currentPos, point.position));

            float score = (timeSinceVisit * config.visitRecencyWeight) +
                         (distanceScore * (1f - config.visitRecencyWeight));

            if (score > bestScore)
            {
                bestScore = score;
                bestPoint = point;
            }
        }

        return bestPoint ?? GetRandomPatrolPoint();
    }

    private Transform GetFarthestPatrolPoint(Vector3 currentPos, Vector3 spawnPos)
    {
        AgentConfiguration config = GetConfigForRole(AgentRole.Scout);
        List<Transform> validPoints = _patrolPoints.FindAll(p =>
            Vector3.Distance(p.position, spawnPos) <= config.patrolRadius);

        if (validPoints.Count == 0) return GetRandomPatrolPoint();

        Transform farthest = null;
        float maxDistance = 0f;

        foreach (Transform point in validPoints)
        {
            float distance = Vector3.Distance(currentPos, point.position);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                farthest = point;
            }
        }

        return farthest ?? validPoints[Random.Range(0, validPoints.Count)];
    }

    private Transform GetDefenderDestination(Vector3 currentPos, Vector3 spawnPos)
    {
        AgentConfiguration config = GetConfigForRole(AgentRole.Defender);
        List<Transform> validPoints = _patrolPoints.FindAll(p =>
            Vector3.Distance(p.position, spawnPos) <= config.patrolRadius);

        if (validPoints.Count == 0) return GetRandomPatrolPoint();

        // Find nearest valid point that isn't current destination
        Transform nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (Transform point in validPoints)
        {
            float distance = Vector3.Distance(currentPos, point.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = point;
            }
        }

        return nearest ?? validPoints[Random.Range(0, validPoints.Count)];
    }

    public Transform GetRandomPatrolPoint()
    {
        if (_patrolPoints.Count == 0) return null;
        return _patrolPoints[Random.Range(0, _patrolPoints.Count)];
    }

    public void AssignPoint(Transform point, GameObject agent)
    {
        if (pointAssignments.ContainsKey(point))
        {
            pointAssignments[point] = agent;
        }
        else
        {
            pointAssignments.Add(point, agent);
        }
    }

    public void ReleasePoint(Transform point)
    {
        if (pointAssignments.ContainsKey(point))
        {
            pointAssignments.Remove(point);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        foreach (Transform point in spawnPoints)
        {
            if (point != null)
            {
                Gizmos.DrawWireSphere(point.position, 0.5f);
            }
        }
    }
}