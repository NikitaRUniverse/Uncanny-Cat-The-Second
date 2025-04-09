using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SwarmAgent : MonoBehaviour
{
    [SerializeField] private AgentRole myRole;
    private AgentConfiguration myConfig;
    private NavMeshAgent agent;
    private Transform currentDestination;
    private float waitTimer = 0f;
    private bool isWaiting = false;
    private Vector3 spawnPosition;

    public void Initialize(AgentRole role, Vector3 spawnPos)
    {
        myRole = role;
        spawnPosition = spawnPos;
        myConfig = SwarmIntelligence.Instance.GetConfigForRole(role);

        agent = GetComponent<NavMeshAgent>();
        agent.speed = myConfig.speed;
        agent.stoppingDistance = myConfig.stoppingDistance;
        agent.angularSpeed = myConfig.angularSpeed * 360f;

        GetComponent<Renderer>().material.color = myConfig.debugColor;

        SetNewDestination();
    }

    private void Update()
    {
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                SetNewDestination();
            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            StartWaiting();
        }
    }

    private void StartWaiting()
    {
        isWaiting = true;
        waitTimer = myConfig.waitTimeAtPoint;

        if (currentDestination != null)
        {
            currentDestination.GetComponent<PatrolPoint>().Visit();
            if (myConfig.avoidRecentlyVisited)
            {
                SwarmIntelligence.Instance.ReleasePoint(currentDestination);
            }
        }
    }

    public void SetNewDestination()
    {
        // Occasionally pick random destination regardless of role
        if (Random.value < myConfig.randomnessFactor)
        {
            currentDestination = SwarmIntelligence.Instance.GetRandomPatrolPoint();
        }
        else
        {
            currentDestination = SwarmIntelligence.Instance.GetRoleAppropriateDestination(
                myRole,
                transform.position,
                spawnPosition);
        }

        if (currentDestination != null)
        {
            if (myConfig.avoidRecentlyVisited)
            {
                SwarmIntelligence.Instance.AssignPoint(currentDestination, gameObject);
            }
            agent.SetDestination(currentDestination.position);
        }
    }

    private void OnDrawGizmos()
    {
        if (currentDestination != null)
        {
            Gizmos.color = myConfig.debugColor;
            Gizmos.DrawLine(transform.position, currentDestination.position);
        }
    }
}