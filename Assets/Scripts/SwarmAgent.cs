using UnityEngine;
using UnityEngine.AI;

public enum AgentState { Patrolling, Chasing, Shooting, Hiding, Dead }
public enum AgentRole { Worker, Scout, Defender }

[System.Serializable]
public class FuzzyInput
{
    public string name;
    public AnimationCurve curve;
    public float weight = 1.0f;
}

[RequireComponent(typeof(NavMeshAgent))] 
[RequireComponent(typeof(Rigidbody))]
public class SwarmAgent : MonoBehaviour
{
    [Header("Role Settings")]
    public AgentRole role;
    public Color debugColor = Color.white;

    [Header("Movement Settings")]
    [Range(1f, 10f)] public float speed = 3.5f;
    [Range(0.1f, 5f)] public float stoppingDistance = 0.5f;
    [Range(1f, 20f)] public float rotationSpeed = 10f;

    [Header("Detection Settings")]
    public float detectionRadius = 10f;
    [Range(0f, 360f)] public float fieldOfView = 90f;
    public LayerMask obstacleLayers;

    [Header("Patrol Settings")]
    public float patrolRadius = 10f;
    [Range(0f, 1f)] public float visitRecencyWeight = 0.7f;
    [Range(0f, 1f)] public float threatResponseWeight = 0.5f;

    [Header("Combat Settings")]
    public float shootingRange = 5f;
    public float maxFear = 100f;
    public float fearReductionRate = 5f;
    public float fearDamageWeight = 1f;
    public float fearDistanceWeight = 0.5f;

    [Header("Fuzzy Logic")]
    public FuzzyInput distanceInput;
    public FuzzyInput angleInput;
    public float shootingThreshold = 0.5f;

    [Header("Runtime Info")]
    [SerializeField] private AgentState _currentState = AgentState.Patrolling;
    public AgentState CurrentState => _currentState;
    public float currentFear = 0f;
    public Transform lastKnownPlayerPosition;

    private NavMeshAgent _agent;
    private Rigidbody _rb;
    private Transform _currentDestination;
    private float _stateTimer = 0f;
    private Quaternion _targetRotation;
    private float _fireCooldown = 0f;
    private bool _hasLineOfSightToPlayer = false;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _rb = GetComponent<Rigidbody>();
        InitializeAgent();
    }

    private void InitializeAgent()
    {
        GetComponent<Renderer>().material.color = debugColor;
        _agent.speed = speed;
        _agent.stoppingDistance = stoppingDistance;
        _agent.angularSpeed = 0; // We'll handle rotation manually
        _agent.updateRotation = false; // Disable NavMeshAgent rotation control
        _targetRotation = transform.rotation;

        // Configure rigidbody constraints
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private void FixedUpdate()
    {
        if (_currentState == AgentState.Dead) return;

        CheckPlayerDetection();
        UpdateRotation();

        // Process fear updates first to ensure immediate state transitions
        switch (_currentState)
        {
            case AgentState.Chasing:
            case AgentState.Shooting:
            case AgentState.Hiding:
                UpdateFearFromDistance();
                break;
        }

        UpdateFearReduction();
        CheckFearThreshold(); // New method to handle fleeing

        // State updates
        switch (_currentState)
        {
            case AgentState.Patrolling:
                UpdatePatrolState();
                break;
            case AgentState.Chasing:
                UpdateChaseState();
                break;
            case AgentState.Shooting:
                UpdateShootingState();
                break;
            case AgentState.Hiding:
                UpdateHidingState();
                break;
        }
    }

    private void UpdateRotation()
    {
        Vector3 lookDirection;

        if (_hasLineOfSightToPlayer && lastKnownPlayerPosition != null)
        {
            // Face the player when we have line of sight
            lookDirection = (lastKnownPlayerPosition.position - transform.position).normalized;
        }
        else
        {
            // Face movement direction otherwise
            lookDirection = _agent.velocity.magnitude > 0.1f ?
                _agent.velocity.normalized :
                transform.forward;
        }

        // Only rotate around Y axis
        if (lookDirection != Vector3.zero)
        {
            lookDirection.y = 0;
            _targetRotation = Quaternion.LookRotation(lookDirection);

            // Apply the rotation
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                _targetRotation,
                rotationSpeed * Time.fixedDeltaTime
            );
        }
    }

    private void UpdatePatrolState()
    {
        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
        {
            RequestNewDestination();
        }
    }

    public void RequestNewDestination()
    {
        if (SwarmIntelligence.Instance != null)
        {
            _currentDestination = SwarmIntelligence.Instance.GetDestinationForAgent(this);
            if (_currentDestination != null)
            {
                _agent.SetDestination(_currentDestination.position);
            }
        }
    }

    private void UpdateChaseState()
    {
        if (lastKnownPlayerPosition == null)
        {
            EnterState(AgentState.Patrolling);
            return;
        }

        float distance = Vector3.Distance(transform.position, lastKnownPlayerPosition.position);

        if (distance > detectionRadius * 1.5f)
        {
            EnterState(AgentState.Patrolling);
            return;
        }

        if (distance > shootingRange)
        {
            _agent.SetDestination(lastKnownPlayerPosition.position);
        }
        else
        {
            _agent.ResetPath();
            EvaluateShooting();
        }
    }

    private void EvaluateShooting()
    {
        if (_fireCooldown > 0)
        {
            _fireCooldown -= Time.fixedDeltaTime;
            return;
        }

        if (lastKnownPlayerPosition == null) return;

        Vector3 toPlayer = lastKnownPlayerPosition.position - transform.position;
        float distance = toPlayer.magnitude;
        float angle = Vector3.Angle(transform.forward, toPlayer);

        float normalizedDistance = Mathf.Clamp01(distance / shootingRange);
        float normalizedAngle = Mathf.Clamp01(angle / 180f);

        float distanceScore = distanceInput.curve.Evaluate(normalizedDistance) * distanceInput.weight;
        float angleScore = angleInput.curve.Evaluate(normalizedAngle) * angleInput.weight;

        float totalScore = (distanceScore + angleScore) / (distanceInput.weight + angleInput.weight);

        if (totalScore >= shootingThreshold)
        {
            EnterState(AgentState.Shooting);
        }
    }

    private void UpdateShootingState()
    {
        Fire();

        if (_stateTimer <= 0)
        {
            if (currentFear >= maxFear)
            {
                EnterState(AgentState.Hiding);
            }
            else
            {
                EnterState(AgentState.Chasing);
            }
        }
        else
        {
            _stateTimer -= Time.fixedDeltaTime;
        }
    }

    private void Fire()
    {
        // Debug.Log($"{name} firing at player!");
        transform.GetChild(0).GetComponent<WeaponSystem>().weapons[transform.GetChild(0).GetComponent<WeaponSystem>().weaponIndex].GetComponent<Weapon>().RemoteFire();
    }

    private void UpdateHidingState()
    {
        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
        {
            currentFear = 0;
            EnterState(AgentState.Patrolling);
        }
    }

    private void CheckPlayerDetection()
    {
        _hasLineOfSightToPlayer = false;

        if (SwarmIntelligence.Instance.playerTransform == null) return;

        Vector3 toPlayer = SwarmIntelligence.Instance.playerTransform.position - transform.position;
        float distance = toPlayer.magnitude;

        if (distance <= detectionRadius)
        {
            if (!Physics.Raycast(transform.position, toPlayer.normalized, distance, obstacleLayers))
            {
                float angle = Vector3.Angle(transform.forward, toPlayer);
                if (angle <= fieldOfView * 0.5f)
                {
                    _hasLineOfSightToPlayer = true;
                    lastKnownPlayerPosition = SwarmIntelligence.Instance.playerTransform;
                    if (_currentState == AgentState.Patrolling || _currentState == AgentState.Hiding)
                    {
                        EnterState(AgentState.Chasing);
                    }
                }
            }
        }
    }

    public void EnterState(AgentState newState)
    {
        _currentState = newState;
        _stateTimer = 0f;

        switch (newState)
        {
            case AgentState.Patrolling:
                _agent.isStopped = false;
                RequestNewDestination();
                break;

            case AgentState.Chasing:
                _agent.isStopped = false;
                if (lastKnownPlayerPosition != null)
                {
                    _agent.SetDestination(lastKnownPlayerPosition.position);
                }
                break;

            case AgentState.Shooting:
                _agent.isStopped = true;
                _stateTimer = 2f;
                break;

            case AgentState.Hiding:
                _agent.isStopped = false;
                if (SwarmIntelligence.Instance.playerTransform != null)
                {
                    _currentDestination = SwarmIntelligence.Instance.FindHidingSpot(
                        transform.position,
                        SwarmIntelligence.Instance.playerTransform.position);
                    _agent.SetDestination(_currentDestination.position);
                }
                break;

            case AgentState.Dead:
                _agent.isStopped = true;
                transform.Rotate(90, 0, 0);
                enabled = false;
                break;
        }
    }

    private void UpdateFearFromDistance()
    {
        if (SwarmIntelligence.Instance.playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position,
            SwarmIntelligence.Instance.playerTransform.position);
        float normalizedDistance = Mathf.Clamp01(distanceToPlayer / detectionRadius);
        float distanceFear = fearDistanceWeight * (1 - normalizedDistance);

        // Apply fear with max cap of maxFear * 1.2
        currentFear = Mathf.Min(currentFear + distanceFear * Time.fixedDeltaTime, maxFear * 1.2f);
    }

    private void UpdateFearReduction()
    {
        currentFear = Mathf.Max(0, currentFear - fearReductionRate * Time.fixedDeltaTime);
    }

    private void CheckFearThreshold()
    {
        if (currentFear >= maxFear && _currentState != AgentState.Hiding)
        {
            // Only trigger hiding if we're not already hiding
            EnterState(AgentState.Hiding);
            Debug.Log($"{name} is fleeing due to fear! Fear level: {currentFear}");
        }
    }

    public void ReceiveDamage(float damage)
    {
        if (_currentState == AgentState.Dead) return;

        float damageFear = fearDamageWeight * damage;
        currentFear = Mathf.Min(currentFear + damageFear, maxFear * 1.2f);

        Debug.Log($"{name} took {damage} damage. Current fear: {currentFear}");

        // Immediate check for fleeing after damage
        CheckFearThreshold();
    }

    public void Die()
    {
        EnterState(AgentState.Dead);
        SwarmIntelligence.Instance.UnregisterAgent(this);
    }

    private void OnDrawGizmos()
    {
        if (_currentDestination != null)
        {
            Gizmos.color = debugColor;
            Gizmos.DrawLine(transform.position, _currentDestination.position);
        }

        // Draw line of sight indicator
        if (_hasLineOfSightToPlayer && lastKnownPlayerPosition != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, lastKnownPlayerPosition.position);
        }
    }
}