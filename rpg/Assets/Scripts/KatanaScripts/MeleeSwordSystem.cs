using UnityEngine;
using System.Collections;

public class MeleeSwordSystem : MonoBehaviour
{
    [Header("Sword Model")]
    public Transform swordModel; // Assign your katana model here

    [Header("Combat Settings")]
    public float comboTimeWindow = 1.5f; // Time window to continue combo
    public float attackCooldown = 0.1f; // Minimum time between attacks
    public LayerMask enemyLayer = -1;
    public string enemyTag = "Enemy";

    [Header("Attack Properties")]
    public float attackRange = 2f;
    public float attackAngle = 60f; // Attack cone angle
    public float baseDamage = 25f;
    public float launchForce = 15f;
    public float launchUpwardForce = 10f;
    public float launchDuration = 1.5f;

    [Header("References")]
    public Camera playerCamera;
    public Transform attackOrigin; // Where attacks originate from

    private SwordAnimationController animationController;
    private SwordComboSystem comboSystem;
    private SwordAttackDetection attackDetection;

    // State tracking
    private bool isAttacking = false;
    private float lastAttackTime;

    // Public properties
    public bool IsAttacking => isAttacking;
    public Transform SwordModel => swordModel;
    public Camera PlayerCamera => playerCamera;
    public Transform AttackOrigin => attackOrigin;

    void Start()
    {
        InitializeReferences();
        InitializeComponents();
    }

    void Update()
    {
        HandleInput();
    }

    void InitializeReferences()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (attackOrigin == null)
            attackOrigin = playerCamera.transform;

        if (swordModel == null)
        {
            Debug.LogWarning("Sword model not assigned! Please assign a sword model to the MeleeSwordSystem.");
        }
    }

    void InitializeComponents()
    {
        // Initialize animation controller
        animationController = gameObject.AddComponent<SwordAnimationController>();
        animationController.Initialize(this);

        // Initialize combo system
        comboSystem = gameObject.AddComponent<SwordComboSystem>();
        comboSystem.Initialize(this, comboTimeWindow);

        // Initialize attack detection
        attackDetection = gameObject.AddComponent<SwordAttackDetection>();
        attackDetection.Initialize(this, attackRange, attackAngle, enemyLayer, enemyTag);
    }

    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0)) // Left click
        {
            TryAttack();
        }
    }

    public void TryAttack()
    {
        // Check if we can attack (cooldown and not currently attacking)
        if (Time.time - lastAttackTime < attackCooldown) return;
        if (isAttacking && !comboSystem.CanContinueCombo()) return;

        PerformAttack();
    }

    void PerformAttack()
    {
        int comboIndex = comboSystem.GetNextComboAttack();

        // Start attack
        isAttacking = true;
        lastAttackTime = Time.time;

        // Perform the attack animation and effects
        StartCoroutine(ExecuteAttack(comboIndex));
    }

    IEnumerator ExecuteAttack(int comboIndex)
    {
        // Get attack data
        var attackData = comboSystem.GetAttackData(comboIndex);

        // Start animation
        animationController.PlayAttackAnimation(comboIndex, attackData.animationDuration);

        // Wait for attack to connect (usually partway through animation)
        yield return new WaitForSeconds(attackData.damageDelay);

        // Perform attack detection and damage
        var hitEnemies = attackDetection.DetectEnemies();
        foreach (var enemy in hitEnemies)
        {
            ApplyAttackEffects(enemy, comboIndex, attackData);
        }

        // Wait for animation to complete
        yield return new WaitForSeconds(attackData.animationDuration - attackData.damageDelay);

        // Check if combo continues or ends
        if (!comboSystem.IsWaitingForNextAttack())
        {
            isAttacking = false;
        }
    }

    void ApplyAttackEffects(Collider enemy, int comboIndex, AttackData attackData)
    {
        // Apply damage
        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(attackData.damage);
        }

        // Apply special effects based on combo index
        if (comboIndex == 3) // 4th attack - launcher
        {
            LaunchEnemy(enemy, attackData);
        }
        else
        {
            // Apply knockback for other attacks
            ApplyKnockback(enemy, attackData.knockbackForce);
        }

        Debug.Log($"Hit {enemy.name} with combo attack {comboIndex + 1}!");
    }

    void LaunchEnemy(Collider enemy, AttackData attackData)
    {
        Rigidbody enemyRb = enemy.GetComponent<Rigidbody>();
        if (enemyRb != null)
        {
            // Launch enemy upward and forward
            Vector3 launchDirection = (attackOrigin.forward + Vector3.up * 0.7f).normalized;
            Vector3 launchVelocity = launchDirection * launchForce + Vector3.up * launchUpwardForce;

            enemyRb.linearVelocity = launchVelocity;

            // Start coroutine to handle air time
            StartCoroutine(HandleLaunchedEnemy(enemyRb));
        }
    }

    IEnumerator HandleLaunchedEnemy(Rigidbody enemyRb)
    {
        // Disable enemy control while in air
        var enemyController = enemyRb.GetComponent<MonoBehaviour>();
        if (enemyController != null)
        {
            enemyController.enabled = false;
        }

        yield return new WaitForSeconds(launchDuration);

        // Re-enable enemy control
        if (enemyController != null)
        {
            enemyController.enabled = true;
        }
    }

    void ApplyKnockback(Collider enemy, float force)
    {
        Rigidbody enemyRb = enemy.GetComponent<Rigidbody>();
        if (enemyRb != null)
        {
            Vector3 knockbackDirection = (enemy.transform.position - attackOrigin.position).normalized;
            enemyRb.AddForce(knockbackDirection * force, ForceMode.Impulse);
        }
    }

    // Public methods for combo system
    public void EndAttack()
    {
        isAttacking = false;
    }

    public AttackData GetBaseAttackData()
    {
        return new AttackData
        {
            damage = baseDamage,
            knockbackForce = 5f,
            animationDuration = 0.6f,
            damageDelay = 0.3f
        };
    }
}

[System.Serializable]
public class AttackData
{
    public float damage;
    public float knockbackForce;
    public float animationDuration;
    public float damageDelay; // Time into animation when damage occurs
}