using UnityEngine;
using System.Linq;

public class PatrolPoint : MonoBehaviour
{
    public float lastVisitTime = -Mathf.Infinity;
    [SerializeField] private float gizmoRadius = 0.5f;

    private void Start()
    {
        if (SwarmIntelligence.Instance != null)
        {
            SwarmIntelligence.Instance.AddPatrolPoint(transform);
        }
    }

    public void Visit()
    {
        lastVisitTime = Time.time;
    }

    private void OnDestroy()
    {
        if (SwarmIntelligence.Instance != null)
        {
            SwarmIntelligence.Instance.RemovePatrolPoint(transform);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, gizmoRadius);

        if (SwarmIntelligence.Instance != null &&
            SwarmIntelligence.Instance.PatrolPoints.Contains(transform))
        {
            Gizmos.DrawIcon(transform.position + Vector3.up * 1.5f, "PatrolPoint.png", true);
        }
    }
}