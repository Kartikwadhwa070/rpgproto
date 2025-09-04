using UnityEngine;
using System.Collections;

[RequireComponent(typeof(BasicEnemyAI))]
public class EnemyRangedAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    public bool canAttack = true;
    public float attackRange = 8f;
    public float attackCooldown = 2f;
    public float attackDamage = 10f;
    public int burstCount = 1;
    public float burstDelay = 0.2f;

    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 10f;
    public float projectileLifetime = 5f;
    public bool useGravity = false;
    public LayerMask projectileLayerMask = -1;

    [Header("Launch Position")]
    public Transform shootPoint;
    public Vector3 shootOffset = new Vector3(0, 1.5f, 0.5f);

    [Header("Prediction Settings")]
    public bool usePrediction = false;
    public float predictionTime = 0.5f;

    [Header("Spread Settings")]
    public float spreadAngle = 0f;
    public bool randomSpread = true;

    [Header("Audio & Effects")]
    public AudioClip shootSound;
    public GameObject muzzleFlashEffect;
    public float muzzleFlashDuration = 0.1f;

    [Header("Debug")]
    public bool showAttackRange = true;
    public bool showDebugRays = false;

    // Components
    private BasicEnemyAI enemyAI;
    private AudioSource audioSource;
    private Transform player;

    // Attack state
    private float lastAttackTime;
    private bool isAttacking = false;
    private Coroutine currentAttackRoutine;

    void Awake()
    {
        enemyAI = GetComponent<BasicEnemyAI>();
        audioSource = GetComponent<AudioSource>();

        // Create AudioSource if it doesn't exist
        if (audioSource == null && shootSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
        }
    }

    void Start()
    {
        FindPlayer();
        SetupShootPoint();

        // Create default projectile if none assigned
        if (projectilePrefab == null)
        {
            Debug.LogWarning($"{gameObject.name}: No projectile prefab assigned! Please assign a projectile prefab in the inspector.");
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
            Debug.LogWarning($"{gameObject.name}: Player not found for ranged attack!");
        }
    }

    void SetupShootPoint()
    {
        // Create shoot point if not assigned
        if (shootPoint == null)
        {
            GameObject shootPointObj = new GameObject("ShootPoint");
            shootPointObj.transform.SetParent(transform);
            shootPointObj.transform.localPosition = shootOffset;
            shootPoint = shootPointObj.transform;
        }
    }

    void Update()
    {
        if (!canAttack || player == null || enemyAI == null) return;

        // Don't attack while launched, recovering, or knocked back
        if (enemyAI.IsLaunched() || enemyAI.IsRecovering() || !enemyAI.CanBeHit()) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        bool playerInRange = distanceToPlayer <= attackRange;
        bool canSeePlayer = HasLineOfSightToPlayer();
        bool cooldownReady = Time.time >= lastAttackTime + attackCooldown;

        if (playerInRange && canSeePlayer && cooldownReady && !isAttacking)
        {
            StartAttack();
        }

        if (showDebugRays && player != null)
        {
            Debug.DrawLine(shootPoint.position, player.position,
                          canSeePlayer ? Color.green : Color.red);
        }
    }

    bool HasLineOfSightToPlayer()
    {
        if (player == null || shootPoint == null) return false;

        Vector3 directionToPlayer = (GetPredictedPlayerPosition() - shootPoint.position).normalized;
        float distanceToPlayer = Vector3.Distance(shootPoint.position, player.position);

        RaycastHit hit;
        if (Physics.Raycast(shootPoint.position, directionToPlayer, out hit, distanceToPlayer, projectileLayerMask))
        {
            // Check if we hit the player or something else
            return hit.collider.CompareTag("Player") || hit.collider.GetComponent<PlayerController>() != null;
        }

        return true; // No obstacles found
    }

    Vector3 GetPredictedPlayerPosition()
    {
        if (!usePrediction || player == null) return player.position;

        // Try to get player velocity for prediction
        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            return player.position + (playerRb.linearVelocity * predictionTime);
        }

        // Fallback to basic prediction
        return player.position;
    }

    void StartAttack()
    {
        if (currentAttackRoutine != null)
        {
            StopCoroutine(currentAttackRoutine);
        }

        currentAttackRoutine = StartCoroutine(AttackRoutine());
    }

    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        lastAttackTime = Time.time;

        // Look at player before attacking
        yield return StartCoroutine(AimAtPlayer());

        // Fire burst
        for (int i = 0; i < burstCount; i++)
        {
            if (player == null || !HasLineOfSightToPlayer()) break;

            FireProjectile();

            if (i < burstCount - 1) // Don't wait after the last shot
            {
                yield return new WaitForSeconds(burstDelay);
            }
        }

        isAttacking = false;
        currentAttackRoutine = null;
    }

    IEnumerator AimAtPlayer()
    {
        if (player == null) yield break;

        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        directionToPlayer.y = 0; // Keep enemy upright

        Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
        float aimTime = 0.3f;
        float elapsed = 0f;

        while (elapsed < aimTime)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, elapsed / aimTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetRotation;
    }

    void FireProjectile()
    {
        if (projectilePrefab == null || shootPoint == null || player == null) return;

        // Calculate direction with spread
        Vector3 targetPosition = GetPredictedPlayerPosition();
        Vector3 direction = (targetPosition - shootPoint.position).normalized;

        // Apply spread
        if (spreadAngle > 0f)
        {
            if (randomSpread)
            {
                float randomX = Random.Range(-spreadAngle, spreadAngle);
                float randomY = Random.Range(-spreadAngle, spreadAngle);
                direction = Quaternion.Euler(randomX, randomY, 0) * direction;
            }
            else
            {
                // Could implement pattern-based spread here
                direction = Quaternion.Euler(Random.Range(-spreadAngle, spreadAngle), Random.Range(-spreadAngle, spreadAngle), 0) * direction;
            }
        }

        // Create projectile
        GameObject projectile = Instantiate(projectilePrefab, shootPoint.position, Quaternion.LookRotation(direction));

        // Setup projectile physics
        Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
        if (projectileRb == null)
        {
            projectileRb = projectile.AddComponent<Rigidbody>();
        }

        projectileRb.useGravity = useGravity;
        projectileRb.linearVelocity = direction * projectileSpeed;

        // Add projectile component if it doesn't exist
        EnemyProjectile projectileScript = projectile.GetComponent<EnemyProjectile>();
        if (projectileScript == null)
        {
            projectileScript = projectile.AddComponent<EnemyProjectile>();
        }

        // Configure projectile
        projectileScript.damage = attackDamage;
        projectileScript.lifetime = projectileLifetime;
        projectileScript.shooter = this.gameObject;

        // Play sound effect
        if (audioSource != null && shootSound != null)
        {
            audioSource.PlayOneShot(shootSound);
        }

        // Show muzzle flash
        if (muzzleFlashEffect != null)
        {
            StartCoroutine(ShowMuzzleFlash());
        }

        if (showDebugRays)
        {
            Debug.Log($"{gameObject.name} fired projectile towards {targetPosition}");
        }
    }

    IEnumerator ShowMuzzleFlash()
    {
        GameObject flash = Instantiate(muzzleFlashEffect, shootPoint.position, shootPoint.rotation);
        flash.transform.SetParent(shootPoint);

        yield return new WaitForSeconds(muzzleFlashDuration);

        if (flash != null)
        {
            Destroy(flash);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showAttackRange) return;

        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Draw shoot point
        if (shootPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(shootPoint.position, 0.1f);
        }
        else
        {
            // Show where shoot point would be
            Vector3 shootPos = transform.position + shootOffset;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(shootPos, 0.1f);
        }

        // Draw line to player if in range
        if (Application.isPlaying && player != null)
        {
            float distance = Vector3.Distance(transform.position, player.position);
            if (distance <= attackRange)
            {
                Gizmos.color = HasLineOfSightToPlayer() ? Color.green : Color.red;
                if (shootPoint != null)
                {
                    Gizmos.DrawLine(shootPoint.position, player.position);
                }
            }
        }
    }

    // Public methods for external control
    public void SetCanAttack(bool canAttack) => this.canAttack = canAttack;
    public bool IsAttacking() => isAttacking;
    public float GetAttackRange() => attackRange;
    public void ForceAttack() => StartAttack();

    // Method to override projectile settings at runtime
    public void SetProjectileSettings(float speed, float lifetime, float damage)
    {
        projectileSpeed = speed;
        projectileLifetime = lifetime;
        attackDamage = damage;
    }

    void OnValidate()
    {
        attackRange = Mathf.Clamp(attackRange, 1f, 50f);
        attackCooldown = Mathf.Clamp(attackCooldown, 0.1f, 10f);
        attackDamage = Mathf.Clamp(attackDamage, 0f, 1000f);
        burstCount = Mathf.Clamp(burstCount, 1, 10);
        burstDelay = Mathf.Clamp(burstDelay, 0.05f, 2f);
        projectileSpeed = Mathf.Clamp(projectileSpeed, 1f, 100f);
        projectileLifetime = Mathf.Clamp(projectileLifetime, 0.5f, 30f);
        predictionTime = Mathf.Clamp(predictionTime, 0f, 3f);
        spreadAngle = Mathf.Clamp(spreadAngle, 0f, 45f);
    }
}