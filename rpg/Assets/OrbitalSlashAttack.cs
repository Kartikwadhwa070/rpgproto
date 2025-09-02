using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class OrbitalSlashAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    public float attackRange = 8f;
    public int hitsPerEnemy = 6;
    public float damagePerHit = 15f;
    public float dashDistance = 6f; // Distance to teleport away from enemy
    public LayerMask enemyLayerMask = -1;

    [Header("Dash Settings")]
    public float dashSpeed = 35f; // Speed of the dash through enemy
    public float dashThroughDistance = 4f; // How far past the enemy to dash
    public float preSlashPause = 0.2f; // Brief pause before dashing
    public float postSlashPause = 0.15f; // Pause after slash before next teleport
    public AnimationCurve dashCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Positioning Settings")]
    public float minAngleVariation = 45f; // Minimum angle change between positions
    public float heightVariation = 2f; // Vertical position randomness
    public float groundOffset = 0.5f; // Height above ground for teleport positions

    [Header("Visual Effects")]
    public GameObject slashEffectPrefab;
    public GameObject teleportEffectPrefab;
    public float slashEffectDuration = 0.3f;
    public Color trailColor = Color.red;
    public float trailWidth = 0.3f;

    [Header("Enhanced Teleportation Effects")]
    public int teleportParticleCount = 25;
    public float teleportEffectRadius = 2f;
    public Color teleportColor1 = Color.cyan;
    public Color teleportColor2 = Color.white;
    public float teleportFlashDuration = 0.2f;

    [Header("Ground-Shaking Camera Effects")]
    public bool enableCameraShake = true;
    public float earthquakeIntensity = 1.2f; // Intense ground-moving shake
    public float earthquakeDuration = 0.25f; // Longer shake duration
    public float verticalShakeMultiplier = 0.6f; // Strong vertical component
    public float horizontalShakeMultiplier = 1.4f; // Even stronger horizontal
    public AnimationCurve earthquakeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    public float shakeFrequency = 15f; // Frequency of shake oscillation

    [Header("Dash Visual Effects")]
    public Color dashTrailColor = Color.yellow;
    public float dashTrailWidth = 0.4f;
    public int dashParticleCount = 15;

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
    private float lastTeleportAngle = 0f;

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
                StartCoroutine(PerformDashSlashAttack(nearestEnemy));
            }
            else
            {
                Debug.Log("No enemies in range for dash slash attack!");
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
        attackTrail.time = 0.8f;
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

    Vector3 CalculateRandomDashStartPosition(Vector3 enemyCenter)
    {
        Vector3 position = enemyCenter;

        // Generate a new angle that's significantly different from the last one
        float newAngle;
        do
        {
            newAngle = Random.Range(0f, 360f);
        }
        while (Mathf.Abs(Mathf.DeltaAngle(newAngle, lastTeleportAngle)) < minAngleVariation);

        lastTeleportAngle = newAngle;
        float radians = newAngle * Mathf.Deg2Rad;

        // Position at dash distance from enemy
        position.x += Mathf.Cos(radians) * dashDistance;
        position.z += Mathf.Sin(radians) * dashDistance;

        // Add height variation but keep it reasonable for ground-based movement
        position.y = enemyCenter.y + groundOffset + Random.Range(-heightVariation * 0.3f, heightVariation);

        return position;
    }

    Vector3 CalculateDashThroughPosition(Vector3 startPos, Vector3 enemyPos)
    {
        // Calculate direction from start to enemy
        Vector3 dashDirection = (enemyPos - startPos).normalized;

        // Extend past the enemy
        Vector3 endPosition = enemyPos + dashDirection * dashThroughDistance;
        endPosition.y = startPos.y; // Keep same height as start position

        return endPosition;
    }

    IEnumerator DashThroughEnemy(Vector3 startPos, Vector3 endPos, GameObject target)
    {
        Vector3 currentPos = startPos;
        float journeyDistance = Vector3.Distance(startPos, endPos);
        float journeyTime = journeyDistance / dashSpeed;

        // Change trail color for dash
        if (attackTrail != null)
        {
            attackTrail.startColor = dashTrailColor;
            attackTrail.endColor = new Color(dashTrailColor.r, dashTrailColor.g, dashTrailColor.b, 0f);
            attackTrail.startWidth = dashTrailWidth;
        }

        float timer = 0f;
        bool hasHitEnemy = false;

        while (timer < journeyTime)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / journeyTime;
            float curveValue = dashCurve.Evaluate(normalizedTime);

            Vector3 newPosition = Vector3.Lerp(startPos, endPos, curveValue);

            characterController.enabled = false;
            transform.position = newPosition;
            characterController.enabled = true;

            // Face movement direction
            Vector3 moveDirection = (endPos - startPos).normalized;
            if (moveDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(moveDirection);
            }

            // Check if we're close to the enemy (hit detection)
            if (!hasHitEnemy && target != null)
            {
                float distanceToEnemy = Vector3.Distance(transform.position, target.transform.position);
                if (distanceToEnemy < 1.5f) // Hit threshold
                {
                    hasHitEnemy = true;
                    PerformSlash(target);
                }
            }

            // Create dash particles along the path
            if (Random.Range(0f, 1f) < 0.3f) // 30% chance each frame
            {
                CreateDashParticle(transform.position);
            }

            yield return null;
        }

        // Ensure final position
        characterController.enabled = false;
        transform.position = endPos;
        characterController.enabled = true;

        // Reset trail color
        if (attackTrail != null)
        {
            attackTrail.startColor = trailColor;
            attackTrail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
            attackTrail.startWidth = trailWidth;
        }
    }

    void CreateDashParticle(Vector3 position)
    {
        GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        particle.transform.position = position + Random.insideUnitSphere * 0.5f;
        particle.transform.localScale = Vector3.one * Random.Range(0.05f, 0.15f);
        particle.transform.rotation = Random.rotation;

        Renderer renderer = particle.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = Color.Lerp(dashTrailColor, Color.white, Random.Range(0f, 1f));

        Destroy(particle.GetComponent<Collider>());
        StartCoroutine(AnimateDashParticle(particle));
    }

    IEnumerator AnimateDashParticle(GameObject particle)
    {
        Vector3 startScale = particle.transform.localScale;
        Vector3 velocity = Random.insideUnitSphere * 2f;
        velocity.y = Mathf.Abs(velocity.y); // Always move upward

        Renderer renderer = particle.GetComponent<Renderer>();
        Color startColor = renderer.material.color;

        float timer = 0f;
        float duration = 0.4f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / duration;

            // Move and scale
            particle.transform.position += velocity * Time.deltaTime;
            particle.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, normalizedTime);

            // Fade out
            float alpha = 1f - normalizedTime;
            renderer.material.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

            // Slow down over time
            velocity *= 0.98f;

            yield return null;
        }

        Destroy(particle);
    }

    void PerformSlash(GameObject target)
    {
        currentHitCount++;

        // Create slash effect
        CreateSlashEffect(target.transform.position);

        // Intense ground-shaking camera shake
        if (enableCameraShake && playerCamera != null)
        {
            StartCoroutine(GroundMovingCameraShake());
        }

        // Deal damage
        DealDamageToTarget(target, damagePerHit);

        // Brief invisibility flash
        StartCoroutine(SlashInvisibilityFlash());

        Debug.Log($"Dash slash hit {currentHitCount}/{hitsPerEnemy} on {target.name}");
    }

    IEnumerator SlashInvisibilityFlash()
    {
        SetPlayerVisibility(false);
        yield return new WaitForSeconds(0.03f);
        SetPlayerVisibility(true);
        yield return new WaitForSeconds(0.02f);
        SetPlayerVisibility(false);
        yield return new WaitForSeconds(0.02f);
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
            CreateEnhancedSlashEffect(position);
        }
    }

    void CreateEnhancedSlashEffect(Vector3 position)
    {
        // Create multiple slash lines for more impact
        for (int i = 0; i < 3; i++)
        {
            GameObject slashLine = new GameObject("DashSlashEffect");
            slashLine.transform.position = position;

            LineRenderer lr = slashLine.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = Color.red;
            lr.endColor = Color.yellow;
            lr.startWidth = 0.2f;
            lr.endWidth = 0.1f;
            lr.positionCount = 2;

            // Create slashes in different directions
            Vector3 slashDirection = Quaternion.Euler(0, Random.Range(-30f, 30f), 0) * transform.forward;
            Vector3 slashStart = position + slashDirection * 1.5f + Vector3.up * Random.Range(0.2f, 1f);
            Vector3 slashEnd = position - slashDirection * 1.5f + Vector3.up * Random.Range(0.2f, 1f);

            lr.SetPosition(0, slashStart);
            lr.SetPosition(1, slashEnd);

            StartCoroutine(AnimateSlashEffect(slashLine, lr, i * 0.05f)); // Stagger the animations
        }
    }

    IEnumerator AnimateSlashEffect(GameObject slashObject, LineRenderer lr, float delay)
    {
        yield return new WaitForSeconds(delay);

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

    void CreateEnhancedTeleportEffect(Vector3 position, bool isDeparture)
    {
        if (teleportEffectPrefab != null)
        {
            GameObject effect = Instantiate(teleportEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 1f);
        }
        else
        {
            CreateAdvancedTeleportEffect(position, isDeparture);
        }
    }

    void CreateAdvancedTeleportEffect(Vector3 position, bool isDeparture)
    {
        // Create more dramatic teleport effect
        for (int i = 0; i < teleportParticleCount; i++)
        {
            GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            float angle = (i / (float)teleportParticleCount) * 360f * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Random.Range(-0.2f, 1f), Mathf.Sin(angle)).normalized;

            particle.transform.position = position + direction * Random.Range(0.1f, 0.3f);
            particle.transform.localScale = Vector3.one * Random.Range(0.04f, 0.12f);

            Renderer renderer = particle.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));

            Color particleColor = Color.Lerp(teleportColor1, teleportColor2, Random.Range(0f, 1f));
            renderer.material.color = particleColor;

            Destroy(particle.GetComponent<Collider>());
            StartCoroutine(AnimateEnhancedTeleportParticle(particle, direction, isDeparture));
        }

        // Add ground impact effect
        CreateGroundImpactEffect(position, isDeparture);
    }

    void CreateGroundImpactEffect(Vector3 position, bool isDeparture)
    {
        // Create ground ring effect
        GameObject ring = new GameObject("GroundRing");
        ring.transform.position = new Vector3(position.x, position.y - 0.5f, position.z);

        LineRenderer lr = ring.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = isDeparture ? teleportColor2 : teleportColor1;
        lr.endColor = lr.startColor;
        lr.startWidth = 0.1f;
        lr.endWidth = 0.1f;
        lr.useWorldSpace = false;
        lr.loop = true;

        // Create circle points
        int segments = 20;
        lr.positionCount = segments;
        float radius = 0.2f;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            Vector3 point = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            lr.SetPosition(i, point);
        }

        StartCoroutine(AnimateGroundRing(ring, lr, isDeparture));
    }

    IEnumerator AnimateGroundRing(GameObject ring, LineRenderer lr, bool isDeparture)
    {
        Color startColor = lr.startColor;
        float timer = 0f;
        float duration = 0.5f;
        float startRadius = 0.2f;
        float maxRadius = isDeparture ? 2f : 1.5f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / duration;

            // Expand ring
            float currentRadius = Mathf.Lerp(startRadius, maxRadius, normalizedTime);
            int segments = lr.positionCount;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * 2f * Mathf.PI / segments;
                Vector3 point = new Vector3(Mathf.Cos(angle) * currentRadius, 0, Mathf.Sin(angle) * currentRadius);
                lr.SetPosition(i, point);
            }

            // Fade out
            float alpha = 1f - normalizedTime;
            lr.startColor = new Color(startColor.r, startColor.g, startColor.b, alpha);
            lr.endColor = lr.startColor;

            yield return null;
        }

        Destroy(ring);
    }

    IEnumerator AnimateEnhancedTeleportParticle(GameObject particle, Vector3 direction, bool isDeparture)
    {
        Vector3 startPos = particle.transform.position;
        Vector3 startScale = particle.transform.localScale;
        float timer = 0f;
        float duration = isDeparture ? 0.4f : 0.3f;

        Renderer renderer = particle.GetComponent<Renderer>();
        Color originalColor = renderer.material.color;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / duration;

            if (isDeparture)
            {
                // Particles explode outward and upward
                Vector3 movement = direction * normalizedTime * teleportEffectRadius * 1.5f;
                movement.y += normalizedTime * normalizedTime * 4f;
                particle.transform.position = startPos + movement;

                // Spin particles
                particle.transform.Rotate(Vector3.up * Time.deltaTime * 720f);
            }
            else
            {
                // Particles swirl inward
                Vector3 movement = direction * (1f - normalizedTime) * teleportEffectRadius;
                float spiral = normalizedTime * 360f * Mathf.Deg2Rad;
                movement.x += Mathf.Cos(spiral) * 0.3f;
                movement.z += Mathf.Sin(spiral) * 0.3f;
                particle.transform.position = startPos + movement;
            }

            // Enhanced scaling
            float scaleMultiplier = isDeparture ? (1f + normalizedTime * 3f) : (2f - normalizedTime * 1.5f);
            particle.transform.localScale = startScale * scaleMultiplier;

            // Flickering fade with more intensity
            float baseAlpha = 1f - normalizedTime;
            float flicker = Mathf.Sin(normalizedTime * 30f) * 0.4f + 0.6f;
            float alpha = baseAlpha * flicker;
            renderer.material.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

            yield return null;
        }

        Destroy(particle);
    }

    IEnumerator GroundMovingCameraShake()
    {
        if (playerCamera == null) yield break;

        Vector3 originalPosition = playerCamera.transform.localPosition;
        float timer = 0f;

        while (timer < earthquakeDuration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / earthquakeDuration;

            // Use curve to control shake intensity over time
            float curveIntensity = earthquakeCurve.Evaluate(normalizedTime);
            float currentIntensity = earthquakeIntensity * curveIntensity;

            // Create ground-moving shake with oscillating patterns
            Vector3 shakeOffset = Vector3.zero;

            // Primary horizontal "ground moving" shake
            float horizontalWave = Mathf.Sin(normalizedTime * shakeFrequency * 2f * Mathf.PI);
            shakeOffset.x = horizontalWave * currentIntensity * horizontalShakeMultiplier;

            // Secondary horizontal shake (perpendicular)
            float horizontalWave2 = Mathf.Cos(normalizedTime * shakeFrequency * 1.7f * Mathf.PI);
            shakeOffset.z = horizontalWave2 * currentIntensity * horizontalShakeMultiplier * 0.7f;

            // Vertical "ground rumble" shake
            float verticalWave = Mathf.Sin(normalizedTime * shakeFrequency * 3f * Mathf.PI);
            shakeOffset.y = verticalWave * currentIntensity * verticalShakeMultiplier;

            // Add random micro-tremors for realism
            shakeOffset.x += Random.Range(-0.1f, 0.1f) * currentIntensity;
            shakeOffset.y += Random.Range(-0.05f, 0.05f) * currentIntensity;
            shakeOffset.z += Random.Range(-0.1f, 0.1f) * currentIntensity;

            playerCamera.transform.localPosition = originalPosition + shakeOffset;
            yield return null;
        }

        playerCamera.transform.localPosition = originalPosition;
    }

    void DealDamageToTarget(GameObject target, float damageAmount)
    {
        Debug.Log($"Dash slash hit! Dealt {damageAmount} damage to {target.name}!");

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

        Debug.Log("Dash Slash Attack completed!");
    }

    IEnumerator AttackCooldown()
    {
        yield return new WaitForSeconds(cooldownDuration);
        canUseAttack = true;
        Debug.Log("Dash slash attack ready!");
    }

    // Main attack coroutine - now with dash-and-slice system
    IEnumerator PerformDashSlashAttack(GameObject target)
    {
        isPerformingAttack = true;
        canUseAttack = false;
        currentTarget = target;
        originalPosition = transform.position;
        currentHitCount = 0;

        Debug.Log($"Starting Enhanced Dash Slash Attack on {target.name}!");

        // Disable player movement
        if (playerController != null)
            playerController.enabled = false;

        // Enable trail effect
        if (attackTrail != null)
            attackTrail.enabled = true;

        // Perform dash-and-slice attacks
        for (int i = 0; i < hitsPerEnemy; i++)
        {
            if (currentTarget == null) break;

            Vector3 enemyCenter = currentTarget.transform.position;

            // 1. Teleport to random position away from enemy
            Vector3 dashStartPosition = CalculateRandomDashStartPosition(enemyCenter);
            yield return StartCoroutine(InstantTeleport(dashStartPosition));

            // 2. Brief pause to build anticipation
            yield return new WaitForSeconds(preSlashPause);

            // 3. Dash through the enemy
            Vector3 dashEndPosition = CalculateDashThroughPosition(dashStartPosition, enemyCenter);
            yield return StartCoroutine(DashThroughEnemy(dashStartPosition, dashEndPosition, currentTarget));

            // 4. Brief pause after slash
            yield return new WaitForSeconds(postSlashPause);

            // 5. Disappear with teleport effect (if not the last hit)
            if (i < hitsPerEnemy - 1)
            {
                CreateEnhancedTeleportEffect(transform.position, true);
                SetPlayerVisibility(false);
                yield return new WaitForSeconds(0.1f);
                SetPlayerVisibility(true);
            }
        }

        // Final dramatic pause
        yield return new WaitForSeconds(0.3f);

        // Return to ground near enemy with final teleport effect
        Vector3 finalPosition = currentTarget != null ?
            currentTarget.transform.position + (originalPosition - currentTarget.transform.position).normalized * 3f :
            originalPosition;
        finalPosition.y = originalPosition.y; // Keep original ground level

        yield return StartCoroutine(InstantTeleport(finalPosition));

        EndAttack();
    }

    IEnumerator InstantTeleport(Vector3 targetPosition)
    {
        CreateEnhancedTeleportEffect(transform.position, true);
        SetPlayerVisibility(false);

        characterController.enabled = false;
        transform.position = targetPosition;
        characterController.enabled = true;

        yield return new WaitForSeconds(0.12f);

        SetPlayerVisibility(true);
        CreateEnhancedTeleportEffect(targetPosition, false);
    }

    // Visual debugging
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (currentTarget != null)
        {
            Vector3 center = currentTarget.transform.position;

            // Draw dash range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, dashDistance);

            // Draw minimum dash range
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(center, dashDistance - 1f);

            // Draw dash through distance
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, dashThroughDistance);

            // Show potential teleport positions around enemy
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector3 pos = center;
                pos.x += Mathf.Cos(angle) * dashDistance;
                pos.z += Mathf.Sin(angle) * dashDistance;
                pos.y += groundOffset;

                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(pos, Vector3.one * 0.3f);

                // Draw dash line
                Vector3 dashEnd = center + (pos - center).normalized * -dashThroughDistance;
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pos, dashEnd);
            }
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