using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float damage = 10f;
    public float lifetime = 5f;
    public bool destroyOnHit = true;
    public LayerMask hitLayerMask = -1;

    [Header("Effects")]
    public GameObject hitEffect;
    public GameObject trailEffect;
    public AudioClip hitSound;
    public float hitEffectDuration = 2f;

    [Header("Knockback")]
    public bool applyKnockback = true;
    public float knockbackForce = 5f;
    public Vector3 knockbackDirection = Vector3.zero; // Zero means use projectile direction

    [Header("Homing (Optional)")]
    public bool isHoming = false;
    public float homingStrength = 2f;
    public float homingRange = 10f;
    public Transform homingTarget; // Can be set manually or will find player

    [Header("Debug")]
    public bool showDebugInfo = false;

    // Internal variables
    public GameObject shooter; // Set by the attack script - made public for access
    private Rigidbody rb;
    private Collider projectileCollider;
    private Transform player;
    private bool hasHit = false;
    private Vector3 lastVelocity;
    private AudioSource audioSource;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        projectileCollider = GetComponent<Collider>();
        audioSource = GetComponent<AudioSource>();

        // Create AudioSource if needed and we have a hit sound
        if (audioSource == null && hitSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
        }
    }

    void Start()
    {
        // Set up projectile
        SetupProjectile();

        // Find player for homing
        if (isHoming && homingTarget == null)
        {
            FindPlayer();
        }

        // Create trail effect
        if (trailEffect != null)
        {
            GameObject trail = Instantiate(trailEffect, transform.position, transform.rotation);
            trail.transform.SetParent(transform);
        }

        // Destroy after lifetime
        Destroy(gameObject, lifetime);
    }

    void SetupProjectile()
    {
        // Make sure the projectile doesn't collide with its shooter
        if (shooter != null)
        {
            Collider shooterCollider = shooter.GetComponent<Collider>();
            if (shooterCollider != null && projectileCollider != null)
            {
                Physics.IgnoreCollision(projectileCollider, shooterCollider);
            }
        }

        // Set up rigidbody
        if (rb != null)
        {
            rb.useGravity = false; // Usually controlled by the attack script
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        // Set up collider as trigger for hit detection
        if (projectileCollider != null)
        {
            projectileCollider.isTrigger = true;
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

        if (isHoming && player != null)
        {
            homingTarget = player;
        }
    }

    void FixedUpdate()
    {
        if (hasHit) return;

        // Store velocity for impact calculations
        lastVelocity = rb.linearVelocity;

        // Handle homing behavior
        if (isHoming && homingTarget != null)
        {
            ApplyHoming();
        }

        if (showDebugInfo)
        {
            Debug.DrawRay(transform.position, rb.linearVelocity.normalized * 2f, Color.red);
        }
    }

    void ApplyHoming()
    {
        float distanceToTarget = Vector3.Distance(transform.position, homingTarget.position);

        // Only home if within range
        if (distanceToTarget <= homingRange)
        {
            Vector3 directionToTarget = (homingTarget.position - transform.position).normalized;
            Vector3 currentDirection = rb.linearVelocity.normalized;

            // Blend current direction with target direction
            Vector3 newDirection = Vector3.Slerp(currentDirection, directionToTarget,
                                               homingStrength * Time.fixedDeltaTime).normalized;

            // Maintain current speed but change direction
            float currentSpeed = rb.linearVelocity.magnitude;
            rb.linearVelocity = newDirection * currentSpeed;

            // Rotate projectile to face direction of travel
            transform.rotation = Quaternion.LookRotation(newDirection);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;

        // Don't hit the shooter
        if (other.gameObject == shooter) return;

        // Check if we should hit this object
        if (!ShouldHitTarget(other)) return;

        // Apply damage and effects
        ApplyHit(other);

        if (showDebugInfo)
        {
            Debug.Log($"Projectile hit {other.gameObject.name}");
        }

        if (destroyOnHit)
        {
            DestroyProjectile();
        }
    }

    bool ShouldHitTarget(Collider other)
    {
        // Check layer mask
        if (((1 << other.gameObject.layer) & hitLayerMask) == 0)
        {
            return false;
        }

        // Always hit player
        if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
        {
            return true;
        }

        // Hit other objects like walls, obstacles
        if (other.CompareTag("Wall") || other.CompareTag("Environment") || other.CompareTag("Ground"))
        {
            return true;
        }

        // Don't hit other enemies from the same shooter
        EnemyRangedAttack enemyAttack = other.GetComponent<EnemyRangedAttack>();
        if (enemyAttack != null)
        {
            return false; // Don't hit other enemies
        }

        return true;
    }

    void ApplyHit(Collider target)
    {
        hasHit = true;

        // Apply damage to player
        PlayerController playerController = target.GetComponent<PlayerController>();
        if (playerController != null)
        {
            // Try multiple common health component names
            bool damageApplied = false;

            // Try common health component names
            var health = target.GetComponent("PlayerHealth");
            if (health == null) health = target.GetComponent("Health");
            if (health == null) health = target.GetComponent("PlayerHP");
            if (health == null) health = target.GetComponent("HP");

            if (health != null)
            {
                // Use reflection to call TakeDamage method
                var takeDamageMethod = health.GetType().GetMethod("TakeDamage");
                if (takeDamageMethod != null)
                {
                    takeDamageMethod.Invoke(health, new object[] { damage });
                    damageApplied = true;
                }
            }

            // Try generic IDamageable interface as fallback
            if (!damageApplied)
            {
                var damageable = target.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(damage);
                    damageApplied = true;
                }
            }

            // If no health system found, just log for debugging
            if (!damageApplied)
            {
                Debug.Log($"Hit player for {damage} damage (no health component found)");
            }

            // Apply knockback to player
            if (applyKnockback && knockbackForce > 0f)
            {
                Vector3 knockback = knockbackDirection != Vector3.zero ?
                                  knockbackDirection.normalized :
                                  lastVelocity.normalized;

                ApplyKnockbackToTarget(target, knockback * knockbackForce);
            }
        }

        // Create hit effect
        if (hitEffect != null)
        {
            GameObject effect = Instantiate(hitEffect, transform.position,
                                          Quaternion.LookRotation(-lastVelocity));
            Destroy(effect, hitEffectDuration);
        }

        // Play hit sound
        if (audioSource != null && hitSound != null)
        {
            // Create a temporary audio source since this object might be destroyed
            GameObject tempAudio = new GameObject("ProjectileHitSound");
            tempAudio.transform.position = transform.position;
            AudioSource tempSource = tempAudio.AddComponent<AudioSource>();
            tempSource.clip = hitSound;
            tempSource.spatialBlend = 1f;
            tempSource.Play();
            Destroy(tempAudio, hitSound.length + 0.1f);
        }
    }

    void ApplyKnockbackToTarget(Collider target, Vector3 knockbackVector)
    {
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            targetRb.AddForce(knockbackVector, ForceMode.Impulse);
        }
        else
        {
            // Try to apply knockback through player controller
            PlayerController playerController = target.GetComponent<PlayerController>();
            if (playerController != null)
            {
                // You might need to add a method to PlayerController to handle knockback
                // playerController.ApplyKnockback(knockbackVector);
            }
        }
    }

    void DestroyProjectile()
    {
        // Detach trail effect so it doesn't get destroyed immediately
        Transform trail = transform.Find("TrailEffect") ?? transform.GetChild(0);
        if (trail != null && trail.gameObject.name.Contains("Trail"))
        {
            trail.SetParent(null);
            Destroy(trail.gameObject, 2f); // Let trail fade out
        }

        Destroy(gameObject);
    }

    // Public methods for external control
    public void SetDamage(float newDamage) => damage = newDamage;
    public void SetLifetime(float newLifetime) => lifetime = newLifetime;
    public void SetHomingTarget(Transform target) => homingTarget = target;
    public void EnableHoming(bool enable) => isHoming = enable;

    public void SetKnockback(float force, Vector3 direction)
    {
        knockbackForce = force;
        knockbackDirection = direction;
        applyKnockback = force > 0f;
    }

    void OnDrawGizmosSelected()
    {
        if (isHoming && homingTarget != null)
        {
            // Draw homing range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, homingRange);

            // Draw line to target
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, homingTarget.position);
        }

        // Draw velocity vector
        if (Application.isPlaying && rb != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, rb.linearVelocity.normalized * 2f);
        }
    }

    void OnValidate()
    {
        damage = Mathf.Clamp(damage, 0f, 1000f);
        lifetime = Mathf.Clamp(lifetime, 0.1f, 30f);
        knockbackForce = Mathf.Clamp(knockbackForce, 0f, 50f);
        homingStrength = Mathf.Clamp(homingStrength, 0.1f, 10f);
        homingRange = Mathf.Clamp(homingRange, 1f, 50f);
        hitEffectDuration = Mathf.Clamp(hitEffectDuration, 0.1f, 10f);
    }
}

// Interface for damageable objects (optional)
public interface IDamageable
{
    void TakeDamage(float damage);
}