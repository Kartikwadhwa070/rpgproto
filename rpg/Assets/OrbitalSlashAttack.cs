using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class OrbitalSlashAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    public float attackRange = 8f;
    public int hitsPerEnemy = 6;
    public float damagePerHit = 15f;
    public float orbitalRadius = 3f;
    public LayerMask enemyLayerMask = -1;

    [Header("Movement Settings")]
    public float teleportSpeed = 20f; // How fast we move between positions
    public float hitPauseDuration = 0.1f; // Pause after each hit
    public float orbitHeight = 1f; // Height above enemy center
    public AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Visual Effects")]
    public GameObject slashEffectPrefab;
    public GameObject teleportEffectPrefab;
    public float slashEffectDuration = 0.3f;
    public Color trailColor = Color.red;
    public float trailWidth = 0.2f;

    [Header("Camera Effects")]
    public bool enableCameraShake = true;
    public float cameraShakeIntensity = 0.3f;
    public float cameraShakeDuration = 0.08f;

    [Header("Input")]
    public KeyCode orbitalAttackKey = KeyCode.E;

    [Header("Cooldown")]
    public float cooldownDuration = 8f;

    // Private variables
    private PlayerController playerController;
    private CharacterController characterController;
    private Camera playerCamera;
    private bool isPerformingAttack = false;
    private bool canUseAttack = true;
    private Renderer[] playerRenderers;
    private TrailRenderer attackTrail;

    // Attack state
    private GameObject currentTarget;
    private Vector3 originalPosition;
    private int currentHitCount;
    private float currentAngle;

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        characterController = GetComponent<CharacterController>();
        playerCamera = Camera.main;
        if (playerCamera == null)
            playerCamera = FindObjectOfType<Camera>();

        playerRenderers = GetComponentsInChildren<Renderer>();
        SetupTrailRenderer();
    }

    void Update()
    {
        if (Input.GetKeyDown(orbitalAttackKey) && canUseAttack && !isPerformingAttack)
        {
            GameObject nearestEnemy = FindNearestEnemy();
            if (nearestEnemy != null)
            {
                StartCoroutine(PerformOrbitalAttack(nearestEnemy));
            }
            else
            {
                Debug.Log("No enemies in range for orbital attack!");
            }
        }
    }

    void SetupTrailRenderer()
    {
        // Create a child object for the trail
        GameObject trailObject = new GameObject("AttackTrail");
        trailObject.transform.SetParent(transform);
        trailObject.transform.localPosition = Vector3.zero;

        attackTrail = trailObject.AddComponent<TrailRenderer>();
        attackTrail.material = new Material(Shader.Find("Sprites/Default"));
        attackTrail.startColor = trailColor;
        attackTrail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
        attackTrail.startWidth = trailWidth;
        attackTrail.endWidth = 0f;
        attackTrail.time = 0.5f;
        attackTrail.enabled = false;
    }

    GameObject FindNearestEnemy()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, attackRange, enemyLayerMask);
        GameObject nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider col in colliders)
        {
            if (col.gameObject != gameObject && col.CompareTag("Enemy"))
            {
                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = col.gameObject;
                }
            }
        }

        return nearest;
    }

    Vector3 CalculateOrbitalPosition(Vector3 center, float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        Vector3 position = center;
        position.x += Mathf.Cos(radians) * orbitalRadius;
        position.z += Mathf.Sin(radians) * orbitalRadius;

        // Add some vertical variation for more dynamic movement
        float verticalOffset = Mathf.Sin(radians * 2f) * 0.5f;
        position.y += verticalOffset;

        return position;
    }

    IEnumerator TeleportToPosition(Vector3 targetPosition)
    {
        Vector3 startPosition = transform.position;
        float journeyDistance = Vector3.Distance(startPosition, targetPosition);
        float journeyTime = journeyDistance / teleportSpeed;

        CreateTeleportEffect(startPosition);

        float timer = 0f;
        while (timer < journeyTime)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / journeyTime;

            Vector3 currentPosition = Vector3.Lerp(startPosition, targetPosition, normalizedTime);

            characterController.enabled = false;
            transform.position = currentPosition;
            characterController.enabled = true;

            yield return null;
        }

        characterController.enabled = false;
        transform.position = targetPosition;
        characterController.enabled = true;

        CreateTeleportEffect(targetPosition);
    }

    void PerformSlash(GameObject target)
    {
        currentHitCount++;

        // Create slash effect
        CreateSlashEffect(target.transform.position);

        // Camera shake
        if (enableCameraShake && playerCamera != null)
        {
            StartCoroutine(CameraShake());
        }

        // Deal damage
        DealDamageToTarget(target, damagePerHit);

        // Brief invisibility flash during slash
        StartCoroutine(InvisibilityFlash());

        Debug.Log($"Orbital hit {currentHitCount}/{hitsPerEnemy} on {target.name}");
    }

    IEnumerator InvisibilityFlash()
    {
        SetPlayerVisibility(false);
        yield return new WaitForSeconds(0.03f);
        SetPlayerVisibility(true);
    }

    void SetPlayerVisibility(bool visible)
    {
        foreach (Renderer renderer in playerRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }

    void CreateSlashEffect(Vector3 position)
    {
        if (slashEffectPrefab != null)
        {
            GameObject effect = Instantiate(slashEffectPrefab, position, transform.rotation);
            Destroy(effect, slashEffectDuration);
        }
        else
        {
            CreateSimpleSlashEffect(position);
        }
    }

    void CreateSimpleSlashEffect(Vector3 position)
    {
        GameObject slashLine = new GameObject("OrbitalSlashEffect");
        slashLine.transform.position = position;

        LineRenderer lr = slashLine.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.red;
        lr.endColor = Color.yellow;
        lr.startWidth = 0.15f;
        lr.endWidth = 0.05f;
        lr.positionCount = 2;

        // Create a curved slash around the enemy
        Vector3 slashStart = position + transform.right * 1.5f + Vector3.up * 0.5f;
        Vector3 slashEnd = position - transform.right * 1.5f + Vector3.up * 0.5f;

        lr.SetPosition(0, slashStart);
        lr.SetPosition(1, slashEnd);

        StartCoroutine(AnimateSlashEffect(slashLine, lr));
    }

    IEnumerator AnimateSlashEffect(GameObject slashObject, LineRenderer lr)
    {
        float timer = 0f;
        Color originalStartColor = lr.startColor;
        Color originalEndColor = lr.endColor;

        while (timer < slashEffectDuration)
        {
            timer += Time.deltaTime;
            float alpha = 1f - (timer / slashEffectDuration);

            Color newStartColor = new Color(originalStartColor.r, originalStartColor.g, originalStartColor.b, alpha);
            Color newEndColor = new Color(originalEndColor.r, originalEndColor.g, originalEndColor.b, alpha);

            lr.startColor = newStartColor;
            lr.endColor = newEndColor;
            yield return null;
        }

        Destroy(slashObject);
    }

    void CreateTeleportEffect(Vector3 position)
    {
        if (teleportEffectPrefab != null)
        {
            GameObject effect = Instantiate(teleportEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 1f);
        }
        else
        {
            CreateSimpleTeleportEffect(position);
        }
    }

    void CreateSimpleTeleportEffect(Vector3 position)
    {
        // Create expanding ring effect
        for (int i = 0; i < 12; i++)
        {
            GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            float angle = i * 30f * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            particle.transform.position = position + direction * 0.2f;
            particle.transform.localScale = Vector3.one * 0.05f;

            Renderer renderer = particle.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.material.color = Color.blue;

            Destroy(particle.GetComponent<Collider>());
            StartCoroutine(AnimateTeleportParticle(particle, direction));
        }
    }

    IEnumerator AnimateTeleportParticle(GameObject particle, Vector3 direction)
    {
        Vector3 startPos = particle.transform.position;
        Vector3 startScale = particle.transform.localScale;
        float timer = 0f;
        float duration = 0.4f;

        Renderer renderer = particle.GetComponent<Renderer>();
        Color originalColor = renderer.material.color;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / duration;

            // Move outward
            particle.transform.position = startPos + direction * normalizedTime * 2f;

            // Scale up then down
            float scale = Mathf.Sin(normalizedTime * Mathf.PI) * 0.3f;
            particle.transform.localScale = startScale + Vector3.one * scale;

            // Fade out
            float alpha = 1f - normalizedTime;
            renderer.material.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

            yield return null;
        }

        Destroy(particle);
    }

    IEnumerator CameraShake()
    {
        if (playerCamera == null) yield break;

        Vector3 originalPosition = playerCamera.transform.localPosition;
        float timer = 0f;

        while (timer < cameraShakeDuration)
        {
            timer += Time.deltaTime;

            Vector3 randomOffset = Random.insideUnitSphere * cameraShakeIntensity;
            randomOffset.z = 0;

            playerCamera.transform.localPosition = originalPosition + randomOffset;
            yield return null;
        }

        playerCamera.transform.localPosition = originalPosition;
    }

    void DealDamageToTarget(GameObject target, float damageAmount)
    {
        // Try to find EnemyHP component (you'll add this later)
        // EnemyHP enemyHP = target.GetComponent<EnemyHP>();
        // if (enemyHP != null)
        // {
        //     enemyHP.TakeDamage(damageAmount);
        // }

        Debug.Log($"Orbital hit! Dealt {damageAmount} damage to {target.name}!");

        // Try common damage methods
        MonoBehaviour[] components = target.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour component in components)
        {
            var takeDamageMethod = component.GetType().GetMethod("TakeDamage");
            if (takeDamageMethod != null)
            {
                takeDamageMethod.Invoke(component, new object[] { damageAmount });
                break;
            }

            var damageMethod = component.GetType().GetMethod("Damage");
            if (damageMethod != null)
            {
                damageMethod.Invoke(component, new object[] { damageAmount });
                break;
            }
        }
    }

    void EndAttack()
    {
        isPerformingAttack = false;
        currentTarget = null;

        // Re-enable player movement
        if (playerController != null)
            playerController.enabled = true;

        // Disable trail
        if (attackTrail != null)
            attackTrail.enabled = false;

        // Start cooldown
        StartCoroutine(AttackCooldown());

        Debug.Log("Orbital Slash Attack completed!");
    }

    IEnumerator AttackCooldown()
    {
        yield return new WaitForSeconds(cooldownDuration);
        canUseAttack = true;
        Debug.Log("Orbital attack ready!");
    }

    // Main attack coroutine
    IEnumerator PerformOrbitalAttack(GameObject target)
    {
        isPerformingAttack = true;
        canUseAttack = false;
        currentTarget = target;
        originalPosition = transform.position;
        currentHitCount = 0;

        Debug.Log($"Starting Orbital Slash Attack on {target.name}!");

        // Disable player movement
        if (playerController != null)
            playerController.enabled = false;

        // Enable trail effect
        if (attackTrail != null)
            attackTrail.enabled = true;

        Vector3 targetCenter = target.transform.position + Vector3.up * orbitHeight;

        // Initial teleport to starting position
        float startAngle = Random.Range(0f, 360f);
        Vector3 startPosition = CalculateOrbitalPosition(targetCenter, startAngle);
        yield return StartCoroutine(InstantTeleport(startPosition));

        // Perform orbital slashes
        for (int i = 0; i < hitsPerEnemy; i++)
        {
            if (currentTarget == null) break;

            // Recalculate center in case enemy moved
            targetCenter = currentTarget.transform.position + Vector3.up * orbitHeight;

            // Calculate next orbital position
            float angleIncrement = (360f / hitsPerEnemy);
            float targetAngle = startAngle + (i * angleIncrement);

            // Add some randomness to make it more dynamic
            targetAngle += Random.Range(-15f, 15f);

            Vector3 nextPosition = CalculateOrbitalPosition(targetCenter, targetAngle);

            // Move to next position
            yield return StartCoroutine(MoveToOrbitalPosition(nextPosition, targetCenter));

            // Perform slash
            PerformSlash(currentTarget);

            // Brief pause
            yield return new WaitForSeconds(hitPauseDuration);
        }

        // Final dramatic pause
        yield return new WaitForSeconds(0.2f);

        // Return to ground near enemy
        Vector3 finalPosition = currentTarget != null ?
            currentTarget.transform.position + (originalPosition - currentTarget.transform.position).normalized * 3f :
            originalPosition;
        finalPosition.y = originalPosition.y; // Keep original ground level

        yield return StartCoroutine(TeleportToPosition(finalPosition));

        EndAttack();
    }

    IEnumerator InstantTeleport(Vector3 targetPosition)
    {
        CreateTeleportEffect(transform.position);
        SetPlayerVisibility(false);

        characterController.enabled = false;
        transform.position = targetPosition;
        characterController.enabled = true;

        yield return new WaitForSeconds(0.1f);

        SetPlayerVisibility(true);
        CreateTeleportEffect(targetPosition);
    }

    IEnumerator MoveToOrbitalPosition(Vector3 targetPosition, Vector3 lookAtTarget)
    {
        Vector3 startPosition = transform.position;
        float journeyDistance = Vector3.Distance(startPosition, targetPosition);
        float journeyTime = journeyDistance / teleportSpeed;

        float timer = 0f;
        while (timer < journeyTime)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / journeyTime;
            float curveValue = speedCurve.Evaluate(normalizedTime);

            Vector3 currentPosition = Vector3.Lerp(startPosition, targetPosition, curveValue);

            characterController.enabled = false;
            transform.position = currentPosition;
            characterController.enabled = true;

            // Always face the target while orbiting
            if (currentTarget != null)
            {
                Vector3 lookDirection = (lookAtTarget - transform.position).normalized;
                lookDirection.y = 0;
                if (lookDirection != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(lookDirection);
                }
            }

            yield return null;
        }

        // Ensure final position
        characterController.enabled = false;
        transform.position = targetPosition;
        characterController.enabled = true;
    }

    // Visual debugging
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (currentTarget != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 center = currentTarget.transform.position + Vector3.up * orbitHeight;

            // Draw orbital path
            Vector3 previousPos = CalculateOrbitalPosition(center, 0f);
            for (int i = 1; i <= 36; i++)
            {
                float angle = i * 10f;
                Vector3 nextPos = CalculateOrbitalPosition(center, angle);
                Gizmos.DrawLine(previousPos, nextPos);
                previousPos = nextPos;
            }

            // Draw orbital radius
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, orbitalRadius);
        }
    }

    // Public methods
    public bool IsAttackOnCooldown()
    {
        return !canUseAttack;
    }

    public bool IsPerformingAttack()
    {
        return isPerformingAttack;
    }
}