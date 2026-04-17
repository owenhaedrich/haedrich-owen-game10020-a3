using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using TMPro;

public class SimpleStateMachine : MonoBehaviour
{
    enum State { Wander, Investigate, Forage, ReturnToNest, Attack }

    [Header("Scene References")]
    public Transform player;
    public Transform nest;
    public TextMeshProUGUI stateText;
    public GameObject heldFood;

    [Header("Config")]
    public float wanderDistance = 10f;
    public float waypointThreshold = 0.6f;
    public float playerDistanceThreshold = 1.5f;
    public float foodDistanceThreshold = 1.5f;
    public float rotationSpeed = 1f;
    public float walkSpeed = 2.0f;
    public float runSpeed = 3.0f;

    [Header("Vision Settings")]
    public float viewRadius = 10f;
    [Range(0, 360)]
    public float viewAngle = 60f;

    [Header("Smell Settings")]
    public float smellRadius = 30f;

    State state;
    NavMeshAgent agent;

    Vector3 closestSmellPosition = Vector3.zero;
    Vector3 lastSmellPosition = Vector3.zero;
    bool canSeePlayer;

    // Movement & Interaction
    AntFood currentFoodItem;
    Vector3 wanderTarget = Vector3.zero;
    Vector3 lastFoodSourcePosition = Vector3.zero;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    // Start in the Wander state
    void Start()
    {
        state = State.Wander;
    }

    // Main loop: handle the state machine
    void Update()
    {
        switch (state)
        {
            case State.Wander:
                Wander();
                break;

            case State.Investigate:
                Investigate();
                break;

            case State.Forage:
                Forage();
                break;

            case State.ReturnToNest:
                ReturnToNest();
                break;

            case State.Attack:
                Attack();
                break;
        }

        // Show state on each agent
        if (stateText != null)
        {
            stateText.text = $"{state}";
            stateText.transform.rotation = Camera.main.transform.rotation;
        }
    }

    //Wander to a random point within the wander distance
    void Wander()
    {
        ForgetSmellAndFood();
        UpdateSenses();

        // If the agent has reached the wander target or the target is null, set a new one
        if ((!agent.pathPending && agent.remainingDistance <= waypointThreshold) || wanderTarget == Vector3.zero)
        {
            SetNewWanderTarget();
        }

        // Set walk speed and destination
        agent.speed = walkSpeed;
        agent.SetDestination(wanderTarget);

        // Transitions
        if (closestSmellPosition != Vector3.zero) //if the agent can smell something, investigate
        {
            state = State.Investigate;
        }
    }

    //Investigate the position where the scent was last detected
    void Investigate()
    {
        UpdateSenses();

        agent.speed = walkSpeed;

        // Go to the last position where a smell was detected
        if (lastSmellPosition != Vector3.zero)
        {
            agent.SetDestination(lastSmellPosition);
        }

        // Transitions
        if (canSeePlayer) //if the agent can see the player, attack
        {
            state = State.Attack;
        }
        else if (currentFoodItem != null) //if the agent can see food, forage
        {
            if (IsInViewCone(currentFoodItem.transform))
            {
                state = State.Forage;
            }
        }
        else if (!agent.pathPending && agent.remainingDistance <= waypointThreshold) //if the agent reached its destination but nothing was activated, wander
        {
            state = State.Wander;
        }

    }

    //Forage for food
    void Forage()
    {
        UpdateSenses();

        // Go to food
        agent.speed = walkSpeed;
        agent.SetDestination(lastFoodSourcePosition);

        // Transitions
        if (!agent.pathPending && agent.remainingDistance <= foodDistanceThreshold) // If the agent has reached the food, pick it up
        {
            // Pick up food
            if (currentFoodItem != null)
            {
                lastFoodSourcePosition = currentFoodItem.transform.position;
                currentFoodItem.TakeFood();
                heldFood.SetActive(true);
                state = State.ReturnToNest;
            }
            else // If the food is gone, wander
            {
                state = State.Wander;
            }
        }
        else if (canSeePlayer) //if the agent can see the player, attack
        {
            state = State.Attack;
        }
        else if (Vector3.Distance(lastSmellPosition, lastFoodSourcePosition) > waypointThreshold) //if the agent can smell something new, investigate
        {
            state = State.Investigate;
        }
    }

    //Return to nest
    void ReturnToNest()
    {
        agent.speed = walkSpeed;
        agent.SetDestination(nest.position);

        // If the agent has reached the nest, drop off the food and go back to foraging
        if (!agent.pathPending && agent.remainingDistance <= foodDistanceThreshold)
        {
            // Drop food and go back to foraging
            heldFood.SetActive(false);
            state = State.Forage;
        }
    }

    //Attack the player
    void Attack()
    {
        ForgetSmellAndFood();
        UpdateSenses();

        // Run to the player
        agent.speed = runSpeed;
        agent.SetDestination(player.position);

        // If an agent touches the player, the player dies (restart the scene)
        Vector3 toPlayer = player.position - transform.position;
        float distToPlayer = toPlayer.magnitude;
        if (distToPlayer < playerDistanceThreshold && canSeePlayer) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        // Transition back if lost the player
        if (!canSeePlayer)
        {
            state = State.Investigate;
        }
    }


    // --- HELPER FUNCTIONS ---

    void UpdateSenses()
    {
        canSeePlayer = IsInViewCone(player);

        // Scent detection
        closestSmellPosition = Vector3.zero;
        float closestDist = float.MaxValue;
        currentFoodItem = null;

        Collider[] potentialScents = Physics.OverlapSphere(transform.position, smellRadius);
        foreach (var collider in potentialScents)
        {
            if (collider.TryGetComponent(out ISmellable smellable))
            {
                float dist = Vector3.Distance(transform.position, collider.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestSmellPosition = collider.transform.position;
                    lastSmellPosition = closestSmellPosition;
                    if (smellable.GetScentType() == ScentType.Food)
                    {
                        currentFoodItem = collider.GetComponent<AntFood>();
                        lastFoodSourcePosition = closestSmellPosition;
                    }
                }
            }
        }
    }

    void ForgetSmellAndFood()
    {
        currentFoodItem = null;
        lastFoodSourcePosition = Vector3.zero;
        lastSmellPosition = Vector3.zero;
    }

    void SetNewWanderTarget()
    {
        if (GetRandomPoint(transform.position, wanderDistance, out Vector3 result))
        {
            wanderTarget = result;
        }
    }

    bool GetRandomPoint(Vector3 center, float range, out Vector3 result)
    {
        // Try to find a random point within the wander distance
        for (int i = 0; i < 30; i++)
        {
            Vector3 randomPoint = center + Random.insideUnitSphere * range;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, 1.0f, NavMesh.AllAreas))
            {
                NavMeshPath path = new NavMeshPath();
                // Calculate the path without moving the agent
                if (agent.CalculatePath(randomPoint, path))
                {
                    if (path.status == NavMeshPathStatus.PathComplete)
                    {
                        result = hit.position;
                        return true;
                    }
                }
            }
        }

        result = center;
        return false;
    }

    bool IsInViewCone(Transform target)
    {
        if (target == null) return false;

        Vector3 toTarget = target.position - transform.position;
        float distToTarget = toTarget.magnitude;

        // 1. Distance check
        if (distToTarget > viewRadius) return false;

        // 2. Angle check
        Vector3 dirToTarget = toTarget.normalized;
        float angle = Vector3.Angle(transform.forward, dirToTarget);

        if (angle > viewAngle * 0.5f) return false;

        // 3. Raycast
        if (Physics.Raycast(transform.position, dirToTarget, out RaycastHit hit, viewRadius))
        {
            return hit.transform == target;
        }
        return false;
    }

    // --- GIZMO DRAWING FOR DEBUG ---

    private void OnDrawGizmos()
    {
        // draw the waypoints
        Gizmos.color = Color.red;

        // draw the view cone
        Handles.color = new Color(0f, 1f, 1f, 0.25f);
        if (canSeePlayer) Handles.color = new Color(1f, 0f, 0f, 0.25f);

        Vector3 forward = transform.forward;
        Handles.DrawSolidArc(transform.position, Vector3.up, forward, viewAngle / 2f, viewRadius);
        Handles.DrawSolidArc(transform.position, Vector3.up, forward, -viewAngle / 2f, viewRadius);

        // Draw smell radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, smellRadius);
    }
}