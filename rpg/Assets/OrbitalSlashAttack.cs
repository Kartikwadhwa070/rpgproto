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

    [Header("Randomization Settings")]
    public float minTeleportDistance = 2f; // Minimum distance between teleport positions
    public float maxRadiusVariation = 1.5f; // How much the radius can vary
    public float heightVariation = 2f; // Vertical position randomness

    [Header("Visual Effects")]
    public GameObject slashEffectPrefab;
    public GameObject teleportEffectPrefab;
    public float slashEffectDuration = 0.3f;
    public Color trailColor = Color.red;
    public float trailWidth = 0.2f;

    [Header("Enhanced Teleportation Effects")]
    public int teleportParticleCount = 20;
    public float teleportEffectRadius = 1.5f;
    public Color teleportColor1 = Color.cyan;
    public Color teleportColor2 = Color.white;
    public float teleportFlashDuration = 0.15f;

    [Header("Intense Camera Effects")]
    public bool enableCameraShake = true;
    public float cameraShakeIntensity = 0.8f; // Increased intensity
    public float cameraShakeDuration = 0.15f; // Longer duration
    public float maxShakeRadius = 1.2f; // Maximum shake distance
    public AnimationCurve shakeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

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
    private List<Vector3> usedPositions = new List<Vector3>();

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

    Vector3 CalculateRandomOrbitalPosition(Vector3 center)
    {
        Vector3 position = center;
        bool validPosition = false;
        int attempts = 0;
        Vector3 candidatePosition = Vector3.zero;

        while (!validPosition && attempts < 20)
        {
            // Random angle instead of incremental
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;

            // Random radius variation
            float randomRadius = orbitalRadius + Random.Range(-maxRadiusVariation, maxRadiusVariation);
            randomRadius = Mathf.Max(randomRadius, 1f); // Ensure minimum radius

            candidatePosition = center;
            candidatePosition.x += Mathf.Cos(randomAngle) * randomRadius;
            candidatePosition.z += Mathf.Sin(randomAngle) * randomRadius;

            // Random vertical variation
            candidatePosition.y += Random.Range(-heightVariation, heightVariation);

            // Check if this position is far enough from previous positions
            validPosition = true;
            foreach (Vector3 usedPos in usedPositions)
            {
                if (Vector3.Distance(candidatePosition, usedPos) < minTeleportDistance)
                {
                    validPosition = false;
                    break;
                }
            }

            attempts++;
        }

        // If we couldn't find a valid position, use the candidate anyway
        usedPositions.Add(candidatePosition);

        // Keep only recent positions to avoid infinite list growth
        if (usedPositions.Count > hitsPerEnemy)
        {
            usedPositions.RemoveAt(0);
        }

        return candidatePosition;
    }

    IEnumerator TeleportToPosition(Vector3 targetPosition)
    {
        Vector3 startPosition = transform.position;
        float journeyDistance = Vector3.Distance(startPosition, targetPosition);
        float journeyTime = journeyDistance / teleportSpeed;

        CreateEnhancedTeleportEffect(startPosition, true); // Departure effect

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

        CreateEnhancedTeleportEffect(targetPosition, false); // Arrival effect
    }

    void PerformSlash(GameObject target)
    {
        currentHitCount++;

        // Create slash effect
        CreateSlashEffect(target.transform.position);

        // Intense camera shake
        if (enableCameraShake && playerCamera != null)
        {
            StartCoroutine(IntenseCameraShake());
        }

        // Deal damage
        DealDamageToTarget(target, damagePerHit);

        // Enhanced invisibility flash with teleport effect
        StartCoroutine(EnhancedInvisibilityFlash());

        Debug.Log($"Orbital hit {currentHitCount}/{hitsPerEnemy} on {target.name}");
    }

    IEnumerator EnhancedInvisibilityFlash()
    {
        // Create brief teleport effect at current position
        CreateEnhancedTeleportEffect(transform.position, false);

        SetPlayerVisibility(false);
        yield return new WaitForSeconds(0.05f);
        SetPlayerVisibility(true);

        // Another brief effect when reappearing
        yield return new WaitForSeconds(0.02f);
        CreateEnhancedTeleportEffect(transform.position, false);
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
        // Create expanding/contracting ring effect with more particles
        for (int i = 0; i < teleportParticleCount; i++)
        {
            GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            float angle = (i / (float)teleportParticleCount) * 360f * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Random.Range(-0.3f, 0.8f), Mathf.Sin(angle)).normalized;

            particle.transform.position = position + direction * 0.1f;
            particle.transform.localScale = Vector3.one * Random.Range(0.03f, 0.08f);

            Renderer renderer = particle.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));

            // Randomize colors
            Color particleColor = Color.Lerp(teleportColor1, teleportColor2, Random.Range(0f, 1f));
            renderer.material.color = particleColor;

            Destroy(particle.GetComponent<Collider>());
            StartCoroutine(AnimateEnhancedTeleportParticle(particle, direction, isDeparture));
        }

        // Add central energy burst
        CreateEnergyBurst(position, isDeparture);
    }

    void CreateEnergyBurst(Vector3 position, bool isDeparture)
    {
        GameObject burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        burst.transform.position = position;
        burst.transform.localScale = Vector3.one * 0.1f;

        Renderer renderer = burst.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = isDeparture ? teleportColor2 : teleportColor1;

        Destroy(burst.GetComponent<Collider>());
        StartCoroutine(AnimateEnergyBurst(burst, isDeparture));
    }

    IEnumerator AnimateEnergyBurst(GameObject burst, bool isDeparture)
    {
        Vector3 startScale = burst.transform.localScale;
        Vector3 targetScale = isDeparture ? Vector3.one * 2f : Vector3.one * 0.5f;

        Renderer renderer = burst.GetComponent<Renderer>();
        Color originalColor = renderer.material.color;

        float timer = 0f;
        float duration = teleportFlashDuration;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / duration;

            // Scale animation
            burst.transform.localScale = Vector3.Lerp(startScale, targetScale, normalizedTime);

            // Fade animation
            float alpha = isDeparture ? (1f - normalizedTime) : Mathf.Sin(normalizedTime * Mathf.PI);
            renderer.material.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

            yield return null;
        }

        Destroy(burst);
    }

    IEnumerator AnimateEnhancedTeleportParticle(GameObject particle, Vector3 direction, bool isDeparture)
    {
        Vector3 startPos = particle.transform.position;
        Vector3 startScale = particle.transform.localScale;
        float timer = 0f;
        float duration = isDeparture ? 0.3f : 0.4f;

        Renderer renderer = particle.GetComponent<Renderer>();
        Color originalColor = renderer.material.color;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / duration;

            // Different movement patterns for departure vs arrival
            if (isDeparture)
            {
                // Particles move outward and upward quickly
                Vector3 movement = direction * normalizedTime * teleportEffectRadius * 2f;
                movement.y += normalizedTime * normalizedTime * 3f; // Accelerating upward
                particle.transform.position = startPos + movement;
            }
            else
            {
                // Particles spiral inward
                Vector3 movement = direction * (1f - normalizedTime) * teleportEffectRadius;
                float spiralY = Mathf.Sin(normalizedTime * Mathf.PI * 4f) * 0.5f;
                movement.y += spiralY;
                particle.transform.position = startPos + movement;
            }

            // Dynamic scaling
            float scaleMultiplier = isDeparture ? (1f + normalizedTime * 2f) : (1f + Mathf.Sin(normalizedTime * Mathf.PI) * 1.5f);
            particle.transform.localScale = startScale * scaleMultiplier;

            // Enhanced fade with flickering
            float baseAlpha = 1f - normalizedTime;
            float flicker = Mathf.Sin(normalizedTime * 20f) * 0.3f + 0.7f;
            float alpha = baseAlpha * flicker;
            renderer.material.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

            yield return null;
        }

        Destroy(particle);
    }

    IEnumerator IntenseCameraShake()
    {
        if (playerCamera == null) yield break;

        Vector3 originalPosition = playerCamera.transform.localPosition;
        float timer = 0f;

        while (timer < cameraShakeDuration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / cameraShakeDuration;

            // Use curve to control shake intensity over time
            float curveIntensity = shakeCurve.Evaluate(normalizedTime);
            float currentIntensity = cameraShakeIntensity * curveIntensity;

            // Create more varied shake patterns
            Vector3 randomOffset = Vector3.zero;

            // Primary shake (strong horizontal)
            randomOffset.x = (Random.Range(-1f, 1f) * currentIntensity);
            randomOffset.y = (Random.Range(-1f, 1f) * currentIntensity * 0.7f);

            // Add secondary micro-shakes for more intensity
            randomOffset.x += Random.Range(-0.1f, 0.1f) * currentIntensity * 3f;
            randomOffset.y += Random.Range(-0.1f, 0.1f) * currentIntensity * 3f;

            // Clamp to maximum radius
            if (randomOffset.magnitude > maxShakeRadius)
            {
                randomOffset = randomOffset.normalized * maxShakeRadius;
            }

            playerCamera.transform.localPosition = originalPosition + randomOffset;
            yield return null;
        }

        playerCamera.transform.localPosition = originalPosition;
    }

    void DealDamageToTarget(GameObject target, float damageAmount)
    {
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
        usedPositions.Clear(); // Clear used positions for next attack

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

    // Main attack coroutine with randomized positioning
    IEnumerator PerformOrbitalAttack(GameObject target)
    {
        isPerformingAttack = true;
        canUseAttack = false;
        currentTarget = target;
        originalPosition = transform.position;
        currentHitCount = 0;
        usedPositions.Clear(); // Reset position tracking

        Debug.Log($"Starting Enhanced Orbital Slash Attack on {target.name}!");

        // Disable player movement
        if (playerController != null)
            playerController.enabled = false;

        // Enable trail effect
        if (attackTrail != null)
            attackTrail.enabled = true;

        Vector3 targetCenter = target.transform.position + Vector3.up * orbitHeight;

        // Initial teleport to random starting position
        Vector3 startPosition = CalculateRandomOrbitalPosition(targetCenter);
        yield return StartCoroutine(InstantTeleport(startPosition));

        // Perform randomized orbital slashes
        for (int i = 0; i < hitsPerEnemy; i++)
        {
            if (currentTarget == null) break;

            // Recalculate center in case enemy moved
            targetCenter = currentTarget.transform.position + Vector3.up * orbitHeight;

            // Calculate next random orbital position
            Vector3 nextPosition = CalculateRandomOrbitalPosition(targetCenter);

            // Move to next position with enhanced effects
            yield return StartCoroutine(MoveToRandomOrbitalPosition(nextPosition, targetCenter));

            // Perform slash
            PerformSlash(currentTarget);

            // Randomized pause duration
            float randomPause = hitPauseDuration + Random.Range(-0.05f, 0.05f);
            yield return new WaitForSeconds(randomPause);
        }

        // Final dramatic pause
        yield return new WaitForSeconds(0.2f);

        // Return to ground near enemy with final teleport effect
        Vector3 finalPosition = currentTarget != null ?
            currentTarget.transform.position + (originalPosition - currentTarget.transform.position).normalized * 3f :
            originalPosition;
        finalPosition.y = originalPosition.y; // Keep original ground level

        yield return StartCoroutine(TeleportToPosition(finalPosition));

        EndAttack();
    }

    IEnumerator InstantTeleport(Vector3 targetPosition)
    {
        CreateEnhancedTeleportEffect(transform.position, true);
        SetPlayerVisibility(false);

        characterController.enabled = false;
        transform.position = targetPosition;
        characterController.enabled = true;

        yield return new WaitForSeconds(0.1f);

        SetPlayerVisibility(true);
        CreateEnhancedTeleportEffect(targetPosition, false);
    }

    IEnumerator MoveToRandomOrbitalPosition(Vector3 targetPosition, Vector3 lookAtTarget)
    {
        Vector3 startPosition = transform.position;
        float journeyDistance = Vector3.Distance(startPosition, targetPosition);
        float journeyTime = journeyDistance / teleportSpeed;

        // Add slight randomness to movement speed
        journeyTime *= Random.Range(0.8f, 1.2f);

        float timer = 0f;
        while (timer < journeyTime)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / journeyTime;
            float curveValue = speedCurve.Evaluate(normalizedTime);

            Vector3 currentPosition = Vector3.Lerp(startPosition, targetPosition, curveValue);

            // Add slight movement wobble for more dynamic feel
            Vector3 wobble = Vector3.zero;
            wobble.x = Mathf.Sin(normalizedTime * 20f) * 0.1f;
            wobble.y = Mathf.Cos(normalizedTime * 15f) * 0.05f;
            currentPosition += wobble;

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

            // Draw orbital range (min and max radius)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, orbitalRadius - maxRadiusVariation);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(center, orbitalRadius + maxRadiusVariation);

            // Draw used positions if in attack
            if (isPerformingAttack)
            {
                Gizmos.color = Color.magenta;
                foreach (Vector3 pos in usedPositions)
                {
                    Gizmos.DrawWireSphere(pos, 0.3f);
                }
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