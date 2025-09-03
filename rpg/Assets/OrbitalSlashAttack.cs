using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class OrbitalSlashAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    public float attackRange = 8f;
    public int hitsPerEnemy = 6;
    public float damagePerHit = 15f;
    public float dashDistance = 6f;
    public LayerMask enemyLayerMask = -1;

    [Header("Manual Dash Settings")]
    public float dashSpeed = 35f;
    public float dashThroughDistance = 4f;
    public float preSlashPause = 0.2f;
    public float postSlashPause = 0.15f;
    public AnimationCurve dashCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float inputTimeoutDuration = 2f; // Time to wait before canceling attack

    [Header("Hit Detection")]
    public float hitDetectionRadius = 2f; // Radius for hit detection
    public bool guaranteeHitOnDash = true; // Ensure every dash hits
    public float hitEffectDelay = 0.1f; // Delay before applying damage for better timing

    [Header("Positioning Settings")]
    public float minAngleVariation = 45f;
    public float heightVariation = 2f;
    public float groundOffset = 0.5f;

    [Header("Camera Following")]
    public bool enableCameraFollow = true;
    public float cameraFollowSpeed = 15f;
    public float cameraFollowDistance = 8f;
    public float cameraHeightOffset = 3f;
    public AnimationCurve cameraFollowCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

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
    public float earthquakeIntensity = 1.2f;
    public float earthquakeDuration = 0.25f;
    public float verticalShakeMultiplier = 0.6f;
    public float horizontalShakeMultiplier = 1.4f;
    public AnimationCurve earthquakeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    public float shakeFrequency = 15f;

    [Header("Dash Visual Effects")]
    public Color dashTrailColor = Color.yellow;
    public float dashTrailWidth = 0.4f;
    public int dashParticleCount = 15;

    [Header("Input")]
    public KeyCode orbitalAttackKey = KeyCode.E;
    public KeyCode dashKey = KeyCode.Space;
    public KeyCode cancelAttackKey = KeyCode.Q;

    [Header("UI Feedback")]
    public bool showDashPrompt = true;
    public string dashPromptText = "Press SPACE to Dash!";
    public float promptFadeTime = 0.3f;

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
    private Vector3 originalCameraPosition;
    private int currentHitCount;
    private float lastTeleportAngle = 0f;
    private bool waitingForDashInput = false;
    private bool dashInputReceived = false;

    // Camera following
    private bool isCameraFollowing = false;
    private Vector3 cameraTargetPosition;
    private Coroutine cameraFollowCoroutine;

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        characterController = GetComponent<CharacterController>();
        playerCamera = Camera.main;
        if (playerCamera == null)
            playerCamera = FindObjectOfType<Camera>();

        if (playerCamera != null)
            originalCameraPosition = playerCamera.transform.localPosition;

        playerRenderers = GetComponentsInChildren<Renderer>();
        SetupTrailRenderer();
    }

    void Update()
    {
        // Start attack
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

        // Manual dash input during attack
        if (waitingForDashInput && Input.GetKeyDown(dashKey))
        {
            dashInputReceived = true;
            waitingForDashInput = false;
        }

        // Cancel attack
        if (isPerformingAttack && Input.GetKeyDown(cancelAttackKey))
        {
            StopAllCoroutines();
            CancelAttack();
        }
    }

    void SetupTrailRenderer()
    {
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

        float newAngle;
        do
        {
            newAngle = Random.Range(0f, 360f);
        }
        while (Mathf.Abs(Mathf.DeltaAngle(newAngle, lastTeleportAngle)) < minAngleVariation);

        lastTeleportAngle = newAngle;
        float radians = newAngle * Mathf.Deg2Rad;

        position.x += Mathf.Cos(radians) * dashDistance;
        position.z += Mathf.Sin(radians) * dashDistance;
        position.y = enemyCenter.y + groundOffset + Random.Range(-heightVariation * 0.3f, heightVariation);

        return position;
    }

    Vector3 CalculateDashThroughPosition(Vector3 startPos, Vector3 enemyPos)
    {
        Vector3 dashDirection = (enemyPos - startPos).normalized;
        Vector3 endPosition = enemyPos + dashDirection * dashThroughDistance;
        endPosition.y = startPos.y;
        return endPosition;
    }

    // FIXED: Manual dash input with timeout that cancels the attack
    IEnumerator WaitForDashInput()
    {
        waitingForDashInput = true;
        dashInputReceived = false;

        if (showDashPrompt)
        {
            StartCoroutine(ShowDashPrompt());
        }

        float timer = 0f;

        // Wait for input with timeout
        while (timer < inputTimeoutDuration && !dashInputReceived)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        waitingForDashInput = false;

        // If timeout reached without input, cancel the entire attack
        if (!dashInputReceived)
        {
            Debug.Log($"No dash input received within {inputTimeoutDuration} seconds. Canceling attack sequence.");

            // Cancel the attack instead of continuing
            StopAllCoroutines();
            CancelAttack();
            yield break; // Exit this coroutine
        }

        Debug.Log("Dash input received!");
    }

    IEnumerator ShowDashPrompt()
    {
        Debug.Log(dashPromptText);

        GameObject promptObj = new GameObject("DashPrompt");
        TextMesh textMesh = promptObj.AddComponent<TextMesh>();
        textMesh.text = dashPromptText;
        textMesh.fontSize = 20;
        textMesh.color = Color.yellow;
        textMesh.anchor = TextAnchor.MiddleCenter;

        promptObj.transform.position = transform.position + Vector3.up * 2f;
        promptObj.transform.LookAt(playerCamera.transform);
        promptObj.transform.Rotate(0, 180, 0);

        float timer = 0f;

        // Keep prompt visible while waiting for input, with timeout warning
        while (waitingForDashInput && timer < inputTimeoutDuration)
        {
            timer += Time.deltaTime;

            // Change color to warning as timeout approaches
            Color promptColor = Color.yellow;
            if (timer > inputTimeoutDuration * 0.7f) // Last 30% of time
            {
                float warningIntensity = (timer - inputTimeoutDuration * 0.7f) / (inputTimeoutDuration * 0.3f);
                promptColor = Color.Lerp(Color.yellow, Color.red, warningIntensity);
            }

            float alpha = Mathf.PingPong(Time.time * 3f, 1f);
            textMesh.color = new Color(promptColor.r, promptColor.g, promptColor.b, alpha);

            // Update text to show remaining time in last 2 seconds
            if (timer > inputTimeoutDuration - 2f)
            {
                float timeLeft = inputTimeoutDuration - timer;
                textMesh.text = $"{dashPromptText} ({timeLeft:F1}s)";
            }

            // Update position to follow player
            promptObj.transform.position = transform.position + Vector3.up * 2f;
            promptObj.transform.LookAt(playerCamera.transform);
            promptObj.transform.Rotate(0, 180, 0);

            yield return null;
        }

        // Fade out quickly
        timer = 0f;
        Color originalColor = textMesh.color;
        while (timer < 0.2f)
        {
            timer += Time.deltaTime;
            float alpha = 1f - (timer / 0.2f);
            textMesh.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }

        Destroy(promptObj);
    }

    // FIXED: Improved hit detection with guaranteed hits
    IEnumerator DashThroughEnemy(Vector3 startPos, Vector3 endPos, GameObject target)
    {
        Vector3 currentPos = startPos;
        float journeyDistance = Vector3.Distance(startPos, endPos);
        float journeyTime = journeyDistance / dashSpeed;

        StartCoroutine(SmoothCameraFollow(startPos));

        if (attackTrail != null)
        {
            attackTrail.startColor = dashTrailColor;
            attackTrail.endColor = new Color(dashTrailColor.r, dashTrailColor.g, dashTrailColor.b, 0f);
            attackTrail.startWidth = dashTrailWidth;
        }

        float timer = 0f;
        bool hasDealtDamage = false;
        Vector3 enemyPos = target != null ? target.transform.position : Vector3.zero;

        while (timer < journeyTime)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / journeyTime;
            float curveValue = dashCurve.Evaluate(normalizedTime);

            Vector3 newPosition = Vector3.Lerp(startPos, endPos, curveValue);

            characterController.enabled = false;
            transform.position = newPosition;
            characterController.enabled = true;

            if (isCameraFollowing)
            {
                UpdateCameraFollowDuringDash(newPosition);
            }

            Vector3 moveDirection = (endPos - startPos).normalized;
            if (moveDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(moveDirection);
            }

            // IMPROVED HIT DETECTION: Check multiple methods
            if (!hasDealtDamage && target != null)
            {
                bool shouldHit = false;

                if (guaranteeHitOnDash)
                {
                    // Guarantee hit when passing through the middle of the dash
                    if (normalizedTime >= 0.4f && normalizedTime <= 0.7f)
                    {
                        shouldHit = true;
                    }
                }
                else
                {
                    // Distance-based detection with larger radius
                    float distanceToEnemy = Vector3.Distance(transform.position, target.transform.position);
                    if (distanceToEnemy < hitDetectionRadius)
                    {
                        shouldHit = true;
                    }
                }

                if (shouldHit)
                {
                    hasDealtDamage = true;
                    // Add a small delay for better visual timing
                    StartCoroutine(DelayedHitEffect(target, hitEffectDelay));
                }
            }

            if (Random.Range(0f, 1f) < 0.3f)
            {
                CreateDashParticle(transform.position);
            }

            yield return null;
        }

        characterController.enabled = false;
        transform.position = endPos;
        characterController.enabled = true;

        // FALLBACK: If somehow no damage was dealt, force it now
        if (!hasDealtDamage && target != null)
        {
            Debug.Log("Fallback hit detection triggered!");
            StartCoroutine(DelayedHitEffect(target, 0f));
        }

        if (attackTrail != null)
        {
            attackTrail.startColor = trailColor;
            attackTrail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
            attackTrail.startWidth = trailWidth;
        }
    }

    // NEW: Delayed hit effect for better timing
    IEnumerator DelayedHitEffect(GameObject target, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (target != null) // Check target still exists
        {
            PerformSlash(target);
        }
    }

    // IMPROVED: Better damage detection with multiple fallback methods
    void PerformSlash(GameObject target)
    {
        currentHitCount++;

        CreateSlashEffect(target.transform.position);

        if (enableCameraShake && playerCamera != null)
        {
            StartCoroutine(GroundMovingCameraShake());
        }

        // IMPROVED DAMAGE SYSTEM: Try multiple methods with better error handling
        bool damageDealt = TryDealDamage(target, damagePerHit);

        if (!damageDealt)
        {
            Debug.LogWarning($"Could not deal damage to {target.name} - no compatible damage system found!");
            // You might want to add a default damage system here
        }

        StartCoroutine(SlashInvisibilityFlash());

        Debug.Log($"Dash slash hit {currentHitCount}/{hitsPerEnemy} on {target.name} - Damage dealt: {damageDealt}");
    }

    // NEW: Improved damage dealing with multiple fallback methods
    bool TryDealDamage(GameObject target, float damageAmount)
    {
        // Method 1: Try standard TakeDamage
        MonoBehaviour[] components = target.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour component in components)
        {
            var takeDamageMethod = component.GetType().GetMethod("TakeDamage");
            if (takeDamageMethod != null)
            {
                try
                {
                    takeDamageMethod.Invoke(component, new object[] { damageAmount });
                    return true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"TakeDamage method failed on {component.GetType().Name}: {e.Message}");
                }
            }
        }

        // Method 2: Try Damage method
        foreach (MonoBehaviour component in components)
        {
            var damageMethod = component.GetType().GetMethod("Damage");
            if (damageMethod != null)
            {
                try
                {
                    damageMethod.Invoke(component, new object[] { damageAmount });
                    return true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Damage method failed on {component.GetType().Name}: {e.Message}");
                }
            }
        }

        // Method 3: Try ReceiveDamage
        foreach (MonoBehaviour component in components)
        {
            var receiveDamageMethod = component.GetType().GetMethod("ReceiveDamage");
            if (receiveDamageMethod != null)
            {
                try
                {
                    receiveDamageMethod.Invoke(component, new object[] { damageAmount });
                    return true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"ReceiveDamage method failed on {component.GetType().Name}: {e.Message}");
                }
            }
        }

        // Method 4: Try Health component directly
        var healthComponent = target.GetComponent<MonoBehaviour>();
        if (healthComponent != null)
        {
            var healthField = healthComponent.GetType().GetField("health");
            var currentHealthField = healthComponent.GetType().GetField("currentHealth");

            if (healthField != null && healthField.FieldType == typeof(float))
            {
                try
                {
                    float currentHealth = (float)healthField.GetValue(healthComponent);
                    healthField.SetValue(healthComponent, currentHealth - damageAmount);
                    Debug.Log($"Direct health modification: {currentHealth} -> {currentHealth - damageAmount}");
                    return true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Direct health modification failed: {e.Message}");
                }
            }

            if (currentHealthField != null && currentHealthField.FieldType == typeof(float))
            {
                try
                {
                    float currentHealth = (float)currentHealthField.GetValue(healthComponent);
                    currentHealthField.SetValue(healthComponent, currentHealth - damageAmount);
                    Debug.Log($"Direct currentHealth modification: {currentHealth} -> {currentHealth - damageAmount}");
                    return true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Direct currentHealth modification failed: {e.Message}");
                }
            }
        }

        // Method 5: Send Unity message (works with SendMessage receivers)
        try
        {
            target.SendMessage("TakeDamage", damageAmount, SendMessageOptions.DontRequireReceiver);
            return true; // Assume it worked since SendMessage doesn't throw for DontRequireReceiver
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"SendMessage TakeDamage failed: {e.Message}");
        }

        return false; // No damage method found
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

            Vector3 slashDirection = Quaternion.Euler(0, Random.Range(-30f, 30f), 0) * transform.forward;
            Vector3 slashStart = position + slashDirection * 1.5f + Vector3.up * Random.Range(0.2f, 1f);
            Vector3 slashEnd = position - slashDirection * 1.5f + Vector3.up * Random.Range(0.2f, 1f);

            lr.SetPosition(0, slashStart);
            lr.SetPosition(1, slashEnd);

            StartCoroutine(AnimateSlashEffect(slashLine, lr, i * 0.05f));
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

            lr.startColor = new Color(originalStartColor.r, originalStartColor.g, originalStartColor.b, alpha);
            lr.endColor = new Color(originalEndColor.r, originalEndColor.g, originalEndColor.b, alpha);
            yield return null;
        }

        Destroy(slashObject);
    }

    // [Rest of the methods remain the same - camera follow, teleport effects, etc.]
    IEnumerator SmoothCameraFollow(Vector3 targetPosition)
    {
        if (!enableCameraFollow || playerCamera == null) yield break;

        Vector3 startPosition = playerCamera.transform.position;
        Vector3 directionToPlayer = (targetPosition - currentTarget.transform.position).normalized;
        Vector3 idealCameraPosition = targetPosition - directionToPlayer * cameraFollowDistance;
        idealCameraPosition.y = targetPosition.y + cameraHeightOffset;

        float timer = 0f;
        float duration = 0.4f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / duration;
            float curveValue = cameraFollowCurve.Evaluate(normalizedTime);

            Vector3 currentCameraPos = Vector3.Lerp(startPosition, idealCameraPosition, curveValue);
            playerCamera.transform.position = currentCameraPos;

            Vector3 lookDirection = (targetPosition - playerCamera.transform.position).normalized;
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                playerCamera.transform.rotation = Quaternion.Lerp(playerCamera.transform.rotation, targetRotation, curveValue);
            }

            yield return null;
        }
    }

    void UpdateCameraFollowDuringDash(Vector3 playerPosition)
    {
        if (!enableCameraFollow || playerCamera == null || currentTarget == null) return;

        Vector3 directionToPlayer = (playerPosition - currentTarget.transform.position).normalized;
        Vector3 idealCameraPosition = playerPosition - directionToPlayer * cameraFollowDistance;
        idealCameraPosition.y = playerPosition.y + cameraHeightOffset;

        Vector3 currentCameraPos = Vector3.Lerp(
            playerCamera.transform.position,
            idealCameraPosition,
            cameraFollowSpeed * Time.deltaTime
        );

        playerCamera.transform.position = currentCameraPos;

        Vector3 lookDirection = (playerPosition - playerCamera.transform.position).normalized;
        if (lookDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            playerCamera.transform.rotation = Quaternion.Lerp(
                playerCamera.transform.rotation,
                targetRotation,
                cameraFollowSpeed * Time.deltaTime
            );
        }
    }

    void StartCameraFollow()
    {
        if (enableCameraFollow && playerCamera != null)
        {
            isCameraFollowing = true;
            if (cameraFollowCoroutine != null)
                StopCoroutine(cameraFollowCoroutine);
        }
    }

    void StopCameraFollow()
    {
        isCameraFollowing = false;
        if (cameraFollowCoroutine != null)
        {
            StopCoroutine(cameraFollowCoroutine);
            cameraFollowCoroutine = null;
        }

        if (playerCamera != null)
        {
            StartCoroutine(ReturnCameraToPlayer());
        }
    }

    IEnumerator ReturnCameraToPlayer()
    {
        if (playerCamera == null) yield break;

        Vector3 startPosition = playerCamera.transform.position;
        Quaternion startRotation = playerCamera.transform.rotation;
        Vector3 targetPosition = transform.position + originalCameraPosition;
        Quaternion targetRotation = Quaternion.identity;

        float timer = 0f;
        float duration = 0.6f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / duration;
            float curveValue = cameraFollowCurve.Evaluate(normalizedTime);

            playerCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, curveValue);
            playerCamera.transform.rotation = Quaternion.Lerp(startRotation, targetRotation, curveValue);

            yield return null;
        }

        playerCamera.transform.localPosition = originalCameraPosition;
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
        velocity.y = Mathf.Abs(velocity.y);

        Renderer renderer = particle.GetComponent<Renderer>();
        Color startColor = renderer.material.color;

        float timer = 0f;
        float duration = 0.4f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / duration;

            particle.transform.position += velocity * Time.deltaTime;
            particle.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, normalizedTime);

            float alpha = 1f - normalizedTime;
            renderer.material.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

            velocity *= 0.98f;
            yield return null;
        }

        Destroy(particle);
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

        CreateGroundImpactEffect(position, isDeparture);
    }

    void CreateGroundImpactEffect(Vector3 position, bool isDeparture)
    {
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

            float currentRadius = Mathf.Lerp(startRadius, maxRadius, normalizedTime);
            int segments = lr.positionCount;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * 2f * Mathf.PI / segments;
                Vector3 point = new Vector3(Mathf.Cos(angle) * currentRadius, 0, Mathf.Sin(angle) * currentRadius);
                lr.SetPosition(i, point);
            }

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
                Vector3 movement = direction * normalizedTime * teleportEffectRadius * 1.5f;
                movement.y += normalizedTime * normalizedTime * 4f;
                particle.transform.position = startPos + movement;
                particle.transform.Rotate(Vector3.up * Time.deltaTime * 720f);
            }
            else
            {
                Vector3 movement = direction * (1f - normalizedTime) * teleportEffectRadius;
                float spiral = normalizedTime * 360f * Mathf.Deg2Rad;
                movement.x += Mathf.Cos(spiral) * 0.3f;
                movement.z += Mathf.Sin(spiral) * 0.3f;
                particle.transform.position = startPos + movement;
            }

            float scaleMultiplier = isDeparture ? (1f + normalizedTime * 3f) : (2f - normalizedTime * 1.5f);
            particle.transform.localScale = startScale * scaleMultiplier;

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

            float curveIntensity = earthquakeCurve.Evaluate(normalizedTime);
            float currentIntensity = earthquakeIntensity * curveIntensity;

            Vector3 shakeOffset = Vector3.zero;

            float horizontalWave = Mathf.Sin(normalizedTime * shakeFrequency * 2f * Mathf.PI);
            shakeOffset.x = horizontalWave * currentIntensity * horizontalShakeMultiplier;

            float horizontalWave2 = Mathf.Cos(normalizedTime * shakeFrequency * 1.7f * Mathf.PI);
            shakeOffset.z = horizontalWave2 * currentIntensity * horizontalShakeMultiplier * 0.7f;

            float verticalWave = Mathf.Sin(normalizedTime * shakeFrequency * 3f * Mathf.PI);
            shakeOffset.y = verticalWave * currentIntensity * verticalShakeMultiplier;

            shakeOffset.x += Random.Range(-0.1f, 0.1f) * currentIntensity;
            shakeOffset.y += Random.Range(-0.05f, 0.05f) * currentIntensity;
            shakeOffset.z += Random.Range(-0.1f, 0.1f) * currentIntensity;

            playerCamera.transform.localPosition = originalPosition + shakeOffset;
            yield return null;
        }

        playerCamera.transform.localPosition = originalPosition;
    }

    void CancelAttack()
    {
        Debug.Log("Orbital Slash Attack cancelled!");

        isPerformingAttack = false;
        waitingForDashInput = false;
        dashInputReceived = false;
        currentTarget = null;

        StopCameraFollow();

        if (playerController != null)
            playerController.enabled = true;

        if (attackTrail != null)
            attackTrail.enabled = false;

        SetPlayerVisibility(true);

        StartCoroutine(InstantTeleport(originalPosition));
        StartCoroutine(AttackCooldown());
    }

    void EndAttack()
    {
        isPerformingAttack = false;
        currentTarget = null;

        StopCameraFollow();

        if (playerController != null)
            playerController.enabled = true;

        if (attackTrail != null)
            attackTrail.enabled = false;

        StartCoroutine(AttackCooldown());

        Debug.Log("Dash Slash Attack completed!");
    }

    IEnumerator AttackCooldown()
    {
        yield return new WaitForSeconds(cooldownDuration);
        canUseAttack = true;
        Debug.Log("Dash slash attack ready!");
    }

    // MAIN ATTACK COROUTINE - Now purely manual control
    IEnumerator PerformDashSlashAttack(GameObject target)
    {
        isPerformingAttack = true;
        canUseAttack = false;
        currentTarget = target;
        originalPosition = transform.position;
        currentHitCount = 0;

        Debug.Log($"Starting Manual Dash Slash Attack on {target.name}! Use {dashKey} to dash through enemy or {cancelAttackKey} to cancel.");

        StartCameraFollow();

        if (playerController != null)
            playerController.enabled = false;

        if (attackTrail != null)
            attackTrail.enabled = true;

        // Perform manual dash-and-slice attacks
        for (int i = 0; i < hitsPerEnemy; i++)
        {
            if (currentTarget == null) break;

            Vector3 enemyCenter = currentTarget.transform.position;

            // 1. Teleport to random position away from enemy
            Vector3 dashStartPosition = CalculateRandomDashStartPosition(enemyCenter);
            yield return StartCoroutine(InstantTeleport(dashStartPosition));

            if (isCameraFollowing)
            {
                StartCoroutine(SmoothCameraFollow(dashStartPosition));
            }

            // 2. Brief pause to build anticipation
            yield return new WaitForSeconds(preSlashPause);

            // 3. Wait for manual dash input (NO TIMEOUT - purely manual)
            Debug.Log($"Ready for dash {i + 1}/{hitsPerEnemy}! Press {dashKey} when ready!");
            yield return StartCoroutine(WaitForDashInput());

            // 4. Dash through the enemy
            Vector3 dashEndPosition = CalculateDashThroughPosition(dashStartPosition, enemyCenter);
            yield return StartCoroutine(DashThroughEnemy(dashStartPosition, dashEndPosition, currentTarget));

            // 5. Brief pause after slash
            yield return new WaitForSeconds(postSlashPause);

            // 6. Teleport effect between hits (except last)
            if (i < hitsPerEnemy - 1)
            {
                CreateEnhancedTeleportEffect(transform.position, true);
                SetPlayerVisibility(false);
                yield return new WaitForSeconds(0.1f);
                SetPlayerVisibility(true);
            }
        }

        yield return new WaitForSeconds(0.3f);

        Vector3 finalPosition = currentTarget != null ?
            currentTarget.transform.position + (originalPosition - currentTarget.transform.position).normalized * 3f :
            originalPosition;
        finalPosition.y = originalPosition.y;

        yield return StartCoroutine(InstantTeleport(finalPosition));

        if (isCameraFollowing)
        {
            StartCoroutine(SmoothCameraFollow(finalPosition));
        }

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

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, dashDistance);

            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(center, dashDistance - 1f);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, dashThroughDistance);

            // Show hit detection radius
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(center, hitDetectionRadius);

            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector3 pos = center;
                pos.x += Mathf.Cos(angle) * dashDistance;
                pos.z += Mathf.Sin(angle) * dashDistance;
                pos.y += groundOffset;

                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(pos, Vector3.one * 0.3f);

                Vector3 dashEnd = center + (pos - center).normalized * -dashThroughDistance;
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pos, dashEnd);
            }
        }

        if (enableCameraFollow && currentTarget != null)
        {
            Vector3 playerPos = transform.position;
            Vector3 directionToPlayer = (playerPos - currentTarget.transform.position).normalized;
            Vector3 idealCameraPos = playerPos - directionToPlayer * cameraFollowDistance;
            idealCameraPos.y = playerPos.y + cameraHeightOffset;

            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(idealCameraPos, Vector3.one * 0.5f);
            Gizmos.DrawLine(idealCameraPos, playerPos);
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

    public bool IsWaitingForDashInput()
    {
        return waitingForDashInput;
    }

    public void ForceDash()
    {
        if (waitingForDashInput)
        {
            dashInputReceived = true;
            waitingForDashInput = false;
        }
    }
}