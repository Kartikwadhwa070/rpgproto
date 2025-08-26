using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class BasicEnemyAI : MonoBehaviour
{
    [Header("Movement Settings")]
    public float followSpeed = 3f;
    public float rotationSpeed = 5f;
    public float stoppingDistance = 2f;
    public float maxFollowDistance = 15f;

    [Header("Combat State")]
    public bool canBeHit = true;
    public float invulnerabilityTime = 0.2f;

    [Header("Launch Settings")]
    public float airControlResistance = 0.95f;
    public float groundCheckDistance = 0.1f;
    public LayerMask groundMask = 1;
    public float landingRecoveryTime = 0.5f;

    [Header("Knockback Settings")]
    public float knockbackRecoveryTime = 0.3f;
    public float maxKnockbackVelocity = 10f;

    [Header("Debug")]
    public bool showDebugInfo = false;

    // Components
    private NavMeshAgent agent;
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
    private EnemyHP enemyHP;
    private Transform player;

    // State tracking
    private bool isLaunched = false;
    private bool isGrounded = true;
    private bool isRecovering = false;
    private bool isKnockedBack = false;
    private Vector3 lastGroundedPosition;
    private Vector3 knockbackVelocity;

    void Awake()
    {
        // Initialize components in Awake to ensure proper setup order
        InitializeComponents();
    }

    void Start()
    {
        SetupPhysics();
        FindPlayer();

        // Force position update after one frame
        Invoke(nameof(ForcePositionUpdate), 0.1f);
    }

    void InitializeComponents()
    {
        // Get required components
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        enemyHP = GetComponent<EnemyHP>();

        // Create EnemyHP if it doesn't exist
        if (enemyHP == null)
        {
            enemyHP = gameObject.AddComponent<EnemyHP>();
        }

        // CRITICAL: Set up collider FIRST before any other physics setup
        if (capsuleCollider != null)
        {
            // Don't modify collider in code - let the developer set it up manually
            // This prevents conflicts with existing setup
            if (capsuleCollider.center == Vector3.zero && capsuleCollider.height == 2f)
            {
                // Only modify if it appears to be default values
                capsuleCollider.center = new Vector3(0, 1f, 0);
                capsuleCollider.height = 2f;
                capsuleCollider.radius = 0.5f;
            }
        }
    }

    void SetupPhysics()
    {
        // Configure NavMeshAgent FIRST
        if (agent != null)
        {
            agent.speed = followSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.acceleration = 8f;
            agent.angularSpeed = 240f;

            // CRITICAL: Set baseOffset to match collider setup
            // This prevents the agent from sinking into the ground
            agent.baseOffset = 0f; // Keep at 0 for proper NavMesh alignment
            agent.height = 2f; // Match collider height
            agent.radius = 0.5f; // Match collider radius
        }

        // Configure rigidbody - KEEP KINEMATIC initially
        if (rb != null)
        {
            rb.isKinematic = true; // Start kinematic, change only during combat
            rb.useGravity = false; // Let NavMeshAgent handle movement initially
            rb.linearDamping = 2f;
            rb.angularDamping = 5f;
            rb.mass = 1f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }
    }

    void ForcePositionUpdate()
    {
        // Ensure we're properly positioned on the NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            if (agent != null && agent.enabled)
            {
                agent.Warp(hit.position); // Use Warp instead of setting position
            }
        }

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} final position: {transform.position}");
        }
    }

    void FindPlayer()
    {
        // Try multiple ways to find the player
        player = FindObjectOfType<PlayerController>()?.transform;

        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (player == null)
        {
            Debug.LogWarning($"{gameObject.name}: Player not found! Make sure PlayerController exists or player has 'Player' tag.");
        }
    }

    void Update()
    {
        CheckGrounded();

        if (isLaunched)
        {
            HandleAirborneState();
        }
        else if (isKnockedBack)
        {
            HandleKnockbackState();
        }
        else if (!isRecovering)
        {
            HandleNormalMovement();
        }

        if (showDebugInfo)
        {
            DisplayDebugInfo();
        }
    }

    void CheckGrounded()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        bool wasGrounded = isGrounded;
        isGrounded = Physics.Raycast(rayStart, Vector3.down, groundCheckDistance + 0.1f, groundMask);

        // If we just landed
        if (!wasGrounded && isGrounded && isLaunched)
        {
            OnLanded();
        }

        if (isGrounded && !isLaunched)
        {
            lastGroundedPosition = transform.position;
        }
    }

    void HandleNormalMovement()
    {
        if (player == null || agent == null || !agent.enabled) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Only follow if within max distance
        if (distanceToPlayer <= maxFollowDistance)
        {
            agent.SetDestination(player.position);

            // Turn towards the player when close
            if (distanceToPlayer <= stoppingDistance * 2f)
            {
                Vector3 directionToPlayer = (player.position - transform.position).normalized;
                directionToPlayer.y = 0;

                if (directionToPlayer != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }
        }
        else
        {
            agent.ResetPath();
        }
    }

    void HandleAirborneState()
    {
        // Switch to physics mode for airborne state
        if (agent.enabled)
        {
            agent.enabled = false;
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        // Apply air resistance
        rb.linearVelocity *= airControlResistance;

        // Limit horizontal movement while airborne
        Vector3 velocity = rb.linearVelocity;
        velocity.x = Mathf.Clamp(velocity.x, -8f, 8f);
        velocity.z = Mathf.Clamp(velocity.z, -8f, 8f);
        rb.linearVelocity = velocity;
    }

    void HandleKnockbackState()
    {
        // Apply knockback velocity with decay
        knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * 3f);

        if (knockbackVelocity.magnitude < 0.5f)
        {
            isKnockedBack = false;
            knockbackVelocity = Vector3.zero;
        }
    }

    void OnLanded()
    {
        isLaunched = false;
        StartCoroutine(RecoveryRoutine(landingRecoveryTime));

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} landed!");
        }
    }

    public void ApplyKnockback(Vector3 direction, float force)
    {
        if (!canBeHit) return;

        force = Mathf.Clamp(force, 0f, maxKnockbackVelocity);

        // Switch to physics mode
        if (agent.enabled)
        {
            agent.enabled = false;
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        // Apply knockback
        Vector3 knockbackForce = direction.normalized * force;
        rb.linearVelocity = new Vector3(knockbackForce.x, rb.linearVelocity.y, knockbackForce.z);

        knockbackVelocity = knockbackForce;
        isKnockedBack = true;

        StartCoroutine(RecoveryRoutine(knockbackRecoveryTime));
        StartCoroutine(InvulnerabilityRoutine());
    }

    public void ApplyLaunch(Vector3 launchVelocity)
    {
        if (!canBeHit) return;

        // Switch to physics mode
        if (agent.enabled)
        {
            agent.enabled = false;
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        rb.linearVelocity = launchVelocity;
        isLaunched = true;
        isKnockedBack = false;

        StartCoroutine(InvulnerabilityRoutine());

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} launched with velocity: {launchVelocity}");
        }
    }

    IEnumerator RecoveryRoutine(float recoveryTime)
    {
        isRecovering = true;

        yield return new WaitForSeconds(recoveryTime);

        // Switch back to NavMeshAgent mode if grounded
        if (isGrounded && !agent.enabled && !isLaunched)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
            {
                // Switch back to kinematic for NavMeshAgent
                rb.isKinematic = true;
                rb.useGravity = false;

                // Warp to NavMesh position
                transform.position = hit.position;
                agent.enabled = true;
                agent.Warp(hit.position);
            }
            else if (NavMesh.SamplePosition(lastGroundedPosition, out hit, 2f, NavMesh.AllAreas))
            {
                rb.isKinematic = true;
                rb.useGravity = false;

                transform.position = hit.position;
                agent.enabled = true;
                agent.Warp(hit.position);
            }
        }

        isRecovering = false;
    }

    IEnumerator InvulnerabilityRoutine()
    {
        canBeHit = false;
        yield return new WaitForSeconds(invulnerabilityTime);
        canBeHit = true;
    }

    void DisplayDebugInfo()
    {
        Debug.DrawRay(transform.position + Vector3.up * 0.1f, Vector3.down * (groundCheckDistance + 0.1f),
                     isGrounded ? Color.green : Color.red);

        // Show collider bounds
        if (capsuleCollider != null)
        {
            Vector3 center = transform.position + capsuleCollider.center;
            Vector3 top = center + Vector3.up * (capsuleCollider.height * 0.5f);
            Vector3 bottom = center - Vector3.up * (capsuleCollider.height * 0.5f);

            Debug.DrawLine(bottom, top, Color.blue, 0f, false);
            //Debug.DrawWireSphere(center, capsuleCollider.radius, Color.cyan);
        }

        // Display state
        string state = "Normal";
        if (isLaunched) state = "Launched";
        else if (isKnockedBack) state = "Knocked Back";
        else if (isRecovering) state = "Recovering";

        Debug.DrawRay(transform.position + Vector3.up * 2.5f, Vector3.up * 0.5f, Color.yellow);
    }

    // Public methods for external systems
    public bool CanBeHit() => canBeHit;
    public bool IsLaunched() => isLaunched;
    public bool IsGrounded() => isGrounded;
    public bool IsRecovering() => isRecovering;

    void OnValidate()
    {
        followSpeed = Mathf.Clamp(followSpeed, 0.1f, 10f);
        rotationSpeed = Mathf.Clamp(rotationSpeed, 0.1f, 20f);
        stoppingDistance = Mathf.Clamp(stoppingDistance, 0.5f, 5f);
        maxFollowDistance = Mathf.Clamp(maxFollowDistance, 5f, 50f);
    }

    // Manual fix methods for testing
    [ContextMenu("Fix Position Now")]
    void FixPositionNow()
    {
        if (Application.isPlaying)
        {
            ForcePositionUpdate();
        }
    }

    [ContextMenu("Reset to NavMesh")]
    void ResetToNavMesh()
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            if (Application.isPlaying && agent != null)
            {
                agent.Warp(hit.position);
            }
        }
    }
}