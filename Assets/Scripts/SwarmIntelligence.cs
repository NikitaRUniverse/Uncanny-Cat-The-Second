using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum SpawnFormation { Circle, Grid, Line, Random }

public class SwarmIntelligence : MonoBehaviour
{
    public static SwarmIntelligence Instance;
    public Transform playerTransform;

    [Header("Agent Prefabs")]
    public GameObject workerPrefab;
    public GameObject scoutPrefab;
    public GameObject defenderPrefab;

    [Header("Spawn Settings")]
    public List<Transform> spawnPoints = new List<Transform>();
    public float spawnRadius = 2f;
    public SpawnFormation formationType = SpawnFormation.Circle;
    public float gridSpacing = 2f;
    public Vector3 lineDirection = Vector3.forward;

    [Header("Patrol Points")]
    [SerializeField] private List<Transform> _patrolPoints = new List<Transform>();
    public IReadOnlyList<Transform> PatrolPoints => _patrolPoints.AsReadOnly();

    private List<SwarmAgent> activeAgents = new List<SwarmAgent>();
    private Dictionary<Transform, SwarmAgent> pointAssignments = new Dictionary<Transform, SwarmAgent>();
    private bool isPlayerThreatActive = false;
    private Vector3 lastThreatPosition;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            FindPlayer();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
    }

    private void Start()
    {
        ValidatePointLists();
        SpawnAgentsUniformly();
    }

    private void Update()
    {
        UpdateThreatStatus();
    }

    private void UpdateThreatStatus()
    {
        bool threatDetected = false;
        foreach (var agent in activeAgents)
        {
            if (agent.CurrentState == AgentState.Chasing || agent.CurrentState == AgentState.Shooting)
            {
                threatDetected = true;
                lastThreatPosition = playerTransform != null ? playerTransform.position : agent.transform.position;
                break;
            }
        }
        isPlayerThreatActive = threatDetected;
    }

    private void ValidatePointLists()
    {
        _patrolPoints.RemoveAll(point => point == null);
        spawnPoints.RemoveAll(point => point == null);
    }

    private void SpawnAgentsUniformly()
    {
        if (spawnPoints.Count == 0) return;

        foreach (Transform spawnPoint in spawnPoints)
        {
            SpawnAgentAtPosition(GetSpawnPosition(spawnPoint.position, 0, 3), workerPrefab);
            SpawnAgentAtPosition(GetSpawnPosition(spawnPoint.position, 1, 3), scoutPrefab);
            SpawnAgentAtPosition(GetSpawnPosition(spawnPoint.position, 2, 3), defenderPrefab);
        }
    }

    private Vector3 GetSpawnPosition(Vector3 center, int index, int total)
    {
        Vector3 position = Vector3.zero;

        switch (formationType)
        {
            case SpawnFormation.Grid:
                int perRow = Mathf.CeilToInt(Mathf.Sqrt(total));
                int row = index / perRow;
                int col = index % perRow;
                position = center + new Vector3(
                    (col - perRow / 2f) * gridSpacing,
                    0,
                    (row - perRow / 2f) * gridSpacing);
                break;

            case SpawnFormation.Line:
                position = center + lineDirection.normalized * index * gridSpacing;
                break;

            case SpawnFormation.Random:
                position = center + Random.insideUnitSphere * spawnRadius;
                break;

            default: // Circle
                float angle = index * Mathf.PI * 2f / total;
                position = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * spawnRadius;
                break;
        }

        return GetNavMeshPosition(position);
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

    private void SpawnAgentAtPosition(Vector3 position, GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("Missing agent prefab!");
            return;
        }

        GameObject agentObj = Instantiate(prefab, position, Quaternion.identity);
        SwarmAgent agent = agentObj.GetComponent<SwarmAgent>();

        if (agent == null)
        {
            Debug.LogError("Prefab missing SwarmAgent component!", prefab);
            return;
        }

        activeAgents.Add(agent);
    }

    public Transform GetDestinationForAgent(SwarmAgent agent)
    {
        if (_patrolPoints.Count == 0) return null;

        // If threat is active and agent is patrolling, respond to threat
        if (isPlayerThreatActive && agent.CurrentState == AgentState.Patrolling)
        {
            float distanceToThreat = Vector3.Distance(agent.transform.position, lastThreatPosition);

            if (Random.value < agent.threatResponseWeight ||
                distanceToThreat < agent.detectionRadius * 0.5f)
            {
                if (distanceToThreat > agent.patrolRadius)
                {
                    return GetPointTowardThreat(agent.transform.position, lastThreatPosition);
                }
                else
                {
                    GameObject tempPoint = new GameObject("TempThreatPoint");
                    tempPoint.transform.position = lastThreatPosition;
                    return tempPoint.transform;
                }
            }
        }

        switch (agent.role)
        {
            case AgentRole.Worker:
                return GetOptimalPatrolPoint(agent);
            case AgentRole.Scout:
                return GetFarthestPatrolPoint(agent.transform.position);
            case AgentRole.Defender:
                return GetDefenderDestination(agent);
            default:
                return GetRandomPatrolPoint();
        }
    }

    private Transform GetOptimalPatrolPoint(SwarmAgent agent)
    {
        Transform bestPoint = null;
        float bestScore = -Mathf.Infinity;

        foreach (Transform point in _patrolPoints)
        {
            if (pointAssignments.ContainsKey(point)) continue;

            PatrolPoint pp = point.GetComponent<PatrolPoint>();
            float timeSinceVisit = Time.time - pp.lastVisitTime;
            float distanceScore = 1f / (1f + Vector3.Distance(agent.transform.position, point.position));

            float score = (timeSinceVisit * agent.visitRecencyWeight) +
                         (distanceScore * (1f - agent.visitRecencyWeight));

            if (score > bestScore)
            {
                bestScore = score;
                bestPoint = point;
            }
        }

        if (bestPoint != null) pointAssignments[bestPoint] = agent;
        return bestPoint ?? GetRandomPatrolPoint();
    }

    private Transform GetFarthestPatrolPoint(Vector3 currentPos)
    {
        Transform farthest = null;
        float maxDistance = 0f;

        foreach (Transform point in _patrolPoints)
        {
            float distance = Vector3.Distance(currentPos, point.position);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                farthest = point;
            }
        }

        return farthest ?? GetRandomPatrolPoint();
    }

    private Transform GetDefenderDestination(SwarmAgent agent)
    {
        List<Transform> validPoints = _patrolPoints.FindAll(p =>
            Vector3.Distance(p.position, agent.transform.position) <= agent.patrolRadius);

        if (validPoints.Count == 0) return GetRandomPatrolPoint();

        Transform nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (Transform point in validPoints)
        {
            float distance = Vector3.Distance(agent.transform.position, point.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = point;
            }
        }

        return nearest ?? validPoints[Random.Range(0, validPoints.Count)];
    }

    private Transform GetPointTowardThreat(Vector3 currentPos, Vector3 threatPos)
    {
        Vector3 direction = (threatPos - currentPos).normalized;
        float searchRadius = Mathf.Min(Vector3.Distance(currentPos, threatPos), 10f);

        Transform bestPoint = null;
        float bestScore = -Mathf.Infinity;

        foreach (Transform point in _patrolPoints)
        {
            Vector3 toPoint = point.position - currentPos;
            float dot = Vector3.Dot(direction, toPoint.normalized);
            float distance = toPoint.magnitude;

            float score = dot * 0.7f + (1f / (1f + distance)) * 0.3f;

            if (score > bestScore)
            {
                bestScore = score;
                bestPoint = point;
            }
        }

        return bestPoint ?? GetRandomPatrolPoint();
    }

    public Transform GetRandomPatrolPoint()
    {
        if (_patrolPoints.Count == 0) return null;
        return _patrolPoints[Random.Range(0, _patrolPoints.Count)];
    }

    public Transform FindHidingSpot(Vector3 fromPosition, Vector3 threatPosition)
    {
        Transform bestSpot = null;
        float bestScore = -Mathf.Infinity;

        foreach (Transform point in _patrolPoints)
        {
            Vector3 dirToThreat = threatPosition - point.position;
            if (!Physics.Raycast(point.position, dirToThreat.normalized, dirToThreat.magnitude,
                LayerMask.GetMask("Obstacles"))) // Use your obstacle layer
            {
                continue;
            }

            float distanceScore = 1f / (1f + Vector3.Distance(fromPosition, point.position));
            float coverScore = Vector3.Dot(dirToThreat.normalized, (point.position - fromPosition).normalized);

            float totalScore = distanceScore * 0.7f + coverScore * 0.3f;
            if (totalScore > bestScore)
            {
                bestScore = totalScore;
                bestSpot = point;
            }
        }

        return bestSpot ?? GetRandomPatrolPoint();
    }

    public void RegisterAgent(SwarmAgent agent)
    {
        if (!activeAgents.Contains(agent))
        {
            activeAgents.Add(agent);
        }
    }

    public void UnregisterAgent(SwarmAgent agent)
    {
        if (activeAgents.Contains(agent))
        {
            activeAgents.Remove(agent);
            ReleaseAllPoints(agent);
        }
    }

    private void ReleaseAllPoints(SwarmAgent agent)
    {
        List<Transform> toRemove = new List<Transform>();
        foreach (var assignment in pointAssignments)
        {
            if (assignment.Value == agent)
            {
                toRemove.Add(assignment.Key);
            }
        }

        foreach (var point in toRemove)
        {
            pointAssignments.Remove(point);
        }
    }

    public void AssignPoint(Transform point, SwarmAgent agent)
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
            // Also remove from point assignments if it exists there
            if (pointAssignments.ContainsKey(point))
            {
                pointAssignments.Remove(point);
            }
        }
    }
}