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
    public float inputTimeoutDuration = 2f;

    [Header("Hit Detection")]
    public float hitDetectionRadius = 2f;
    public bool guaranteeHitOnDash = true;
    public float hitEffectDelay = 0.1f;

    [Header("Positioning Settings")]
    public float minAngleVariation = 45f;
    public float heightVariation = 2f;
    public float groundOffset = 0.5f;

    [Header("Audio Effects")]
    public AudioSource audioSource;
    public AudioClip teleportSFX;
    public AudioClip slashHitSFX;
    public AudioClip dashStartSFX;
    public AudioClip attackStartSFX;
    public AudioClip attackEndSFX;
    public AudioClip promptSFX;
    [Range(0f, 1f)] public float sfxVolume = 1f;

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

    [Header("Enhanced Visual Improvements")]
    public bool enableScreenDistortion = true;
    public float distortionIntensity = 0.3f;
    public bool enableTimeSlowEffect = true;
    public float timeSlowFactor = 0.3f;
    public float timeSlowDuration = 0.5f;
    public bool enableLightningEffects = true;
    public Color lightningColor = Color.blue;
    public int lightningBoltCount = 8;

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

    // Cached components
    private PlayerController playerController;
    private CharacterController characterController;
    private Camera playerCamera;
    private Renderer[] playerRenderers;
    private TrailRenderer attackTrail;
    private Transform cachedTransform;

    // State management
    private bool isPerformingAttack = false;
    private bool canUseAttack = true;
    private float cooldownStartTime;
    private GameObject currentTarget;
    private Vector3 originalPosition;
    private Vector3 originalCameraPosition;
    private int currentHitCount;
    private float lastTeleportAngle = 0f;
    private bool waitingForDashInput = false;
    private bool dashInputReceived = false;

    // Camera following
    private bool isCameraFollowing = false;

    // UI Management
    private GameObject currentPromptObject;
    private Coroutine currentPromptCoroutine;

    // Object pooling for effects
    private readonly Queue<GameObject> slashEffectPool = new Queue<GameObject>();
    private readonly Queue<GameObject> teleportEffectPool = new Queue<GameObject>();
    private readonly Queue<GameObject> particlePool = new Queue<GameObject>();
    private const int EFFECT_POOL_SIZE = 10;

    // Cached materials and shaders
    private static Material slashMaterial;
    private static Material teleportMaterial;
    private static Material lightningMaterial;
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int AlphaProperty = Shader.PropertyToID("_Alpha");

    // Wait instructions (reused)
    private readonly WaitForSeconds preSlashWait;
    private readonly WaitForSeconds postSlashWait;
    private readonly WaitForSeconds teleportFlashWait;

    // Damage system cache
    private readonly Dictionary<System.Type, System.Reflection.MethodInfo> damageMethodCache = new Dictionary<System.Type, System.Reflection.MethodInfo>();

    public OrbitalSlashAttack()
    {
        preSlashWait = new WaitForSeconds(preSlashPause);
        postSlashWait = new WaitForSeconds(postSlashPause);
        teleportFlashWait = new WaitForSeconds(teleportFlashDuration);
    }

    void Awake()
    {
        cachedTransform = transform;
        InitializeComponents();
        InitializeMaterials();
        InitializeAudio();
    }

    void Start()
    {
        playerRenderers = GetComponentsInChildren<Renderer>();
        SetupTrailRenderer();
        InitializeEffectPools();
    }

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        // Start attack
        if (Input.GetKeyDown(orbitalAttackKey) && canUseAttack && !isPerformingAttack)
        {
            var nearestEnemy = FindNearestEnemy();
            if (nearestEnemy != null)
            {
                StartCoroutine(PerformDashSlashAttack(nearestEnemy));
            }
        }

        // Manual dash input during attack
        if (waitingForDashInput && Input.GetKeyDown(dashKey))
        {
            dashInputReceived = true;
            waitingForDashInput = false;
            PlaySFX(dashStartSFX);
        }

        // Cancel attack
        if (isPerformingAttack && Input.GetKeyDown(cancelAttackKey))
        {
            CancelAttack();
        }
    }

    void InitializeComponents()
    {
        playerController = GetComponent<PlayerController>();
        characterController = GetComponent<CharacterController>();
        playerCamera = Camera.main ?? FindObjectOfType<Camera>();

        if (playerCamera != null)
            originalCameraPosition = playerCamera.transform.localPosition;
    }

    void InitializeAudio()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.7f; // 3D sound
            }
        }
    }

    void InitializeMaterials()
    {
        if (slashMaterial == null)
        {
            slashMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                color = Color.red
            };
        }

        if (teleportMaterial == null)
        {
            teleportMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                color = teleportColor1
            };
        }

        if (lightningMaterial == null)
        {
            lightningMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                color = lightningColor
            };
        }
    }

    void InitializeEffectPools()
    {
        for (int i = 0; i < EFFECT_POOL_SIZE; i++)
        {
            if (slashEffectPrefab != null)
            {
                var obj = Instantiate(slashEffectPrefab);
                obj.SetActive(false);
                slashEffectPool.Enqueue(obj);
            }

            if (teleportEffectPrefab != null)
            {
                var obj = Instantiate(teleportEffectPrefab);
                obj.SetActive(false);
                teleportEffectPool.Enqueue(obj);
            }

            // Pre-create particle objects
            var particle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            particle.transform.localScale = Vector3.one * 0.1f;
            Destroy(particle.GetComponent<Collider>());
            particle.SetActive(false);
            particlePool.Enqueue(particle);
        }
    }

    void SetupTrailRenderer()
    {
        var trailObject = new GameObject("AttackTrail");
        trailObject.transform.SetParent(cachedTransform);
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
        var colliders = new Collider[20];
        int count = Physics.OverlapSphereNonAlloc(cachedTransform.position, attackRange, colliders, enemyLayerMask);

        GameObject nearest = null;
        float nearestSqrDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var col = colliders[i];
            if (col.gameObject != gameObject && col.CompareTag("Enemy"))
            {
                float sqrDistance = (col.transform.position - cachedTransform.position).sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = col.gameObject;
                }
            }
        }

        return nearest;
    }

    Vector3 CalculateRandomDashStartPosition(Vector3 enemyCenter)
    {
        float newAngle;
        do
        {
            newAngle = Random.Range(0f, 360f);
        }
        while (Mathf.Abs(Mathf.DeltaAngle(newAngle, lastTeleportAngle)) < minAngleVariation);

        lastTeleportAngle = newAngle;
        float radians = newAngle * Mathf.Deg2Rad;

        var position = enemyCenter;
        position.x += Mathf.Cos(radians) * dashDistance;
        position.z += Mathf.Sin(radians) * dashDistance;
        position.y = enemyCenter.y + groundOffset + Random.Range(-heightVariation * 0.3f, heightVariation);

        return position;
    }

    Vector3 CalculateDashThroughPosition(Vector3 startPos, Vector3 enemyPos)
    {
        var dashDirection = (enemyPos - startPos).normalized;
        var endPosition = enemyPos + dashDirection * dashThroughDistance;
        endPosition.y = startPos.y;
        return endPosition;
    }

    // FIXED: Proper UI cleanup and timeout handling
    IEnumerator WaitForDashInput()
    {
        waitingForDashInput = true;
        dashInputReceived = false;

        PlaySFX(promptSFX);

        if (showDashPrompt)
        {
            currentPromptCoroutine = StartCoroutine(ShowDashPrompt());
        }

        float timer = 0f;

        while (timer < inputTimeoutDuration && !dashInputReceived)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        waitingForDashInput = false;

        // FIXED: Ensure prompt cleanup regardless of outcome
        if (currentPromptCoroutine != null)
        {
            StopCoroutine(currentPromptCoroutine);
            currentPromptCoroutine = null;
        }

        CleanupPromptUI();

        if (!dashInputReceived)
        {
            Debug.Log($"No dash input received within {inputTimeoutDuration} seconds. Canceling attack.");
            CancelAttack();
            yield break;
        }
    }

    // FIXED: Proper UI management with cleanup
    IEnumerator ShowDashPrompt()
    {
        CleanupPromptUI(); // Ensure no existing prompt

        currentPromptObject = new GameObject("DashPrompt");
        var textMesh = currentPromptObject.AddComponent<TextMesh>();
        textMesh.text = dashPromptText;
        textMesh.fontSize = 20;
        textMesh.color = Color.yellow;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.characterSize = 0.1f;

        currentPromptObject.transform.position = cachedTransform.position + Vector3.up * 2f;

        if (playerCamera != null)
        {
            currentPromptObject.transform.LookAt(playerCamera.transform);
            currentPromptObject.transform.Rotate(0, 180, 0);
        }

        float timer = 0f;
        var originalColor = textMesh.color;

        while (waitingForDashInput && timer < inputTimeoutDuration && currentPromptObject != null)
        {
            timer += Time.deltaTime;

            // Update position and rotation
            if (currentPromptObject != null && playerCamera != null)
            {
                currentPromptObject.transform.position = cachedTransform.position + Vector3.up * 2f;
                currentPromptObject.transform.LookAt(playerCamera.transform);
                currentPromptObject.transform.Rotate(0, 180, 0);

                // Pulsing effect
                float alpha = 0.7f + 0.3f * Mathf.Sin(Time.time * 5f);

                // Warning color as timeout approaches
                if (timer > inputTimeoutDuration * 0.7f)
                {
                    float warningIntensity = (timer - inputTimeoutDuration * 0.7f) / (inputTimeoutDuration * 0.3f);
                    var warningColor = Color.Lerp(Color.yellow, Color.red, warningIntensity);
                    textMesh.color = new Color(warningColor.r, warningColor.g, warningColor.b, alpha);

                    // Show countdown in last 2 seconds
                    if (timer > inputTimeoutDuration - 2f)
                    {
                        float timeLeft = inputTimeoutDuration - timer;
                        textMesh.text = $"{dashPromptText} ({timeLeft:F1}s)";
                    }
                }
                else
                {
                    textMesh.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                }
            }

            yield return null;
        }

        // Fade out
        if (currentPromptObject != null)
        {
            var fadeTimer = 0f;
            var fadeColor = textMesh.color;

            while (fadeTimer < 0.2f && currentPromptObject != null)
            {
                fadeTimer += Time.deltaTime;
                float alpha = 1f - (fadeTimer / 0.2f);
                textMesh.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
                yield return null;
            }
        }

        CleanupPromptUI();
    }

    void CleanupPromptUI()
    {
        if (currentPromptObject != null)
        {
            DestroyImmediate(currentPromptObject);
            currentPromptObject = null;
        }
    }

    IEnumerator DashThroughEnemy(Vector3 startPos, Vector3 endPos, GameObject target)
    {
        float journeyDistance = Vector3.Distance(startPos, endPos);
        float journeyTime = journeyDistance / dashSpeed;

        StartCoroutine(SmoothCameraFollow(startPos));

        // Setup dash trail
        if (attackTrail != null)
        {
            attackTrail.startColor = dashTrailColor;
            attackTrail.endColor = new Color(dashTrailColor.r, dashTrailColor.g, dashTrailColor.b, 0f);
            attackTrail.startWidth = dashTrailWidth;
        }

        // Time slow effect
        if (enableTimeSlowEffect)
        {
            StartCoroutine(TimeSlowEffect());
        }

        float timer = 0f;
        bool hasDealtDamage = false;

        while (timer < journeyTime)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / journeyTime;
            float curveValue = dashCurve.Evaluate(normalizedTime);

            var newPosition = Vector3.Lerp(startPos, endPos, curveValue);

            // Efficient position update
            characterController.enabled = false;
            cachedTransform.position = newPosition;
            characterController.enabled = true;

            // Camera follow
            if (isCameraFollowing)
            {
                UpdateCameraFollowDuringDash(newPosition);
            }

            // Rotation
            var moveDirection = (endPos - startPos).normalized;
            if (moveDirection.sqrMagnitude > 0.001f)
            {
                cachedTransform.rotation = Quaternion.LookRotation(moveDirection);
            }

            // Hit detection
            if (!hasDealtDamage && target != null)
            {
                bool shouldHit = guaranteeHitOnDash ?
                    (normalizedTime >= 0.4f && normalizedTime <= 0.7f) :
                    Vector3.Distance(cachedTransform.position, target.transform.position) < hitDetectionRadius;

                if (shouldHit)
                {
                    hasDealtDamage = true;
                    StartCoroutine(DelayedHitEffect(target, hitEffectDelay));
                }
            }

            // Particle effects
            if (Random.value < 0.3f)
            {
                CreateDashParticle(cachedTransform.position);
            }

            yield return null;
        }

        // Final position
        characterController.enabled = false;
        cachedTransform.position = endPos;
        characterController.enabled = true;

        // Fallback damage
        if (!hasDealtDamage && target != null)
        {
            StartCoroutine(DelayedHitEffect(target, 0f));
        }

        // Reset trail
        if (attackTrail != null)
        {
            attackTrail.startColor = trailColor;
            attackTrail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
            attackTrail.startWidth = trailWidth;
        }
    }

    IEnumerator DelayedHitEffect(GameObject target, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (target != null)
        {
            PerformSlash(target);
        }
    }

    void PerformSlash(GameObject target)
    {
        currentHitCount++;

        CreateSlashEffect(target.transform.position);
        PlaySFX(slashHitSFX);

        if (enableCameraShake && playerCamera != null)
        {
            StartCoroutine(GroundMovingCameraShake());
        }

        if (enableLightningEffects)
        {
            CreateLightningEffect(target.transform.position);
        }

        bool damageDealt = TryDealDamage(target, damagePerHit);
        StartCoroutine(SlashInvisibilityFlash());

        Debug.Log($"Dash slash hit {currentHitCount}/{hitsPerEnemy} on {target.name} - Damage: {damageDealt}");
    }

    // Enhanced damage system with caching
    bool TryDealDamage(GameObject target, float damageAmount)
    {
        var components = target.GetComponents<MonoBehaviour>();

        for (int i = 0; i < components.Length; i++)
        {
            var component = components[i];
            var type = component.GetType();

            // Check cache first
            if (damageMethodCache.TryGetValue(type, out var cachedMethod))
            {
                if (cachedMethod != null)
                {
                    try
                    {
                        cachedMethod.Invoke(component, new object[] { damageAmount });
                        return true;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Cached damage method failed: {e.Message}");
                        damageMethodCache[type] = null; // Invalidate cache
                    }
                }
                continue;
            }

            // Find and cache damage method
            var method = type.GetMethod("TakeDamage") ??
                        type.GetMethod("Damage") ??
                        type.GetMethod("ReceiveDamage");

            damageMethodCache[type] = method;

            if (method != null)
            {
                try
                {
                    method.Invoke(component, new object[] { damageAmount });
                    return true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Damage method failed: {e.Message}");
                }
            }
        }

        // Fallback: SendMessage
        try
        {
            target.SendMessage("TakeDamage", damageAmount, SendMessageOptions.DontRequireReceiver);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"SendMessage failed: {e.Message}");
        }

        return false;
    }

    // NEW: Time slow visual effect
    IEnumerator TimeSlowEffect()
    {
        Time.timeScale = timeSlowFactor;
        yield return new WaitForSecondsRealtime(timeSlowDuration);

        // Gradually return to normal speed
        float timer = 0f;
        float returnDuration = 0.2f;

        while (timer < returnDuration)
        {
            timer += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(timeSlowFactor, 1f, timer / returnDuration);
            yield return null;
        }

        Time.timeScale = 1f;
    }

    // NEW: Lightning effects
    void CreateLightningEffect(Vector3 position)
    {
        for (int i = 0; i < lightningBoltCount; i++)
        {
            var lightning = new GameObject("Lightning");
            lightning.transform.position = position;

            var lr = lightning.AddComponent<LineRenderer>();
            lr.material = lightningMaterial;
            lr.startColor = lightningColor;
            lr.endColor = Color.white;
            lr.startWidth = 0.05f;
            lr.endWidth = 0.02f;
            lr.positionCount = 8;

            // Create jagged lightning path
            var start = position + Vector3.up * 2f;
            var end = position + Random.insideUnitSphere * 3f;

            for (int j = 0; j < 8; j++)
            {
                float t = j / 7f;
                var point = Vector3.Lerp(start, end, t);
                point += Random.insideUnitSphere * 0.5f * (1f - t);
                lr.SetPosition(j, point);
            }

            StartCoroutine(AnimateLightning(lightning, lr));
        }
    }

    IEnumerator AnimateLightning(GameObject lightning, LineRenderer lr)
    {
        float timer = 0f;
        float duration = 0.1f;
        var originalColor = lr.startColor;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float flicker = Random.value;
            lr.startColor = originalColor * flicker;
            lr.endColor = Color.white * flicker;
            yield return null;
        }

        Destroy(lightning);
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
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            if (playerRenderers[i] != null)
            {
                playerRenderers[i].enabled = visible;
            }
        }
    }

    void CreateSlashEffect(Vector3 position)
    {
        if (slashEffectPrefab != null && slashEffectPool.Count > 0)
        {
            var effect = slashEffectPool.Dequeue();
            effect.transform.position = position;
            effect.transform.rotation = cachedTransform.rotation;
            effect.SetActive(true);
            StartCoroutine(ReturnEffectToPool(effect, slashEffectPool, slashEffectDuration));
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
            var slashLine = new GameObject("DashSlashEffect");
            slashLine.transform.position = position;

            var lr = slashLine.AddComponent<LineRenderer>();
            lr.material = slashMaterial;
            lr.startColor = Color.red;
            lr.endColor = Color.yellow;
            lr.startWidth = 0.2f;
            lr.endWidth = 0.1f;
            lr.positionCount = 2;

            var slashDirection = Quaternion.Euler(0, Random.Range(-30f, 30f), 0) * cachedTransform.forward;
            var slashStart = position + slashDirection * 1.5f + Vector3.up * Random.Range(0.2f, 1f);
            var slashEnd = position - slashDirection * 1.5f + Vector3.up * Random.Range(0.2f, 1f);

            lr.SetPosition(0, slashStart);
            lr.SetPosition(1, slashEnd);

            StartCoroutine(AnimateSlashEffect(slashLine, lr, i * 0.05f));
        }
    }

    IEnumerator AnimateSlashEffect(GameObject slashObject, LineRenderer lr, float delay)
    {
        yield return new WaitForSeconds(delay);

        float timer = 0f;
        var originalStartColor = lr.startColor;
        var originalEndColor = lr.endColor;

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

    IEnumerator ReturnEffectToPool(GameObject effect, Queue<GameObject> pool, float delay)
    {
        yield return new WaitForSeconds(delay);
        effect.SetActive(false);
        pool.Enqueue(effect);
    }

    // [Camera and teleport methods remain similar but optimized]
    IEnumerator SmoothCameraFollow(Vector3 targetPosition)
    {
        if (!enableCameraFollow || playerCamera == null) yield break;

        var startPosition = playerCamera.transform.position;
        var directionToPlayer = (targetPosition - currentTarget.transform.position).normalized;
        var idealCameraPosition = targetPosition - directionToPlayer * cameraFollowDistance;
        idealCameraPosition.y = targetPosition.y + cameraHeightOffset;

        float timer = 0f;
        float duration = 0.4f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / duration;
            float curveValue = cameraFollowCurve.Evaluate(normalizedTime);

            var currentCameraPos = Vector3.Lerp(
            playerCamera.transform.position,
            idealCameraPosition,
            cameraFollowSpeed * Time.deltaTime
        );

            playerCamera.transform.position = currentCameraPos;

            var lookDirection = (playerPosition - playerCamera.transform.position).normalized;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                var targetRotation = Quaternion.LookRotation(lookDirection);
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
            }
        }

        void StopCameraFollow()
        {
            isCameraFollowing = false;

            if (playerCamera != null)
            {
                StartCoroutine(ReturnCameraToPlayer());
            }
        }

        IEnumerator ReturnCameraToPlayer()
        {
            if (playerCamera == null) yield break;

            var startPosition = playerCamera.transform.position;
            var startRotation = playerCamera.transform.rotation;
            var targetPosition = cachedTransform.position + originalCameraPosition;
            var targetRotation = Quaternion.identity;

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
            if (particlePool.Count > 0)
            {
                var particle = particlePool.Dequeue();
                particle.transform.position = position + Random.insideUnitSphere * 0.5f;
                particle.transform.localScale = Vector3.one * Random.Range(0.05f, 0.15f);
                particle.transform.rotation = Random.rotation;
                particle.SetActive(true);

                var renderer = particle.GetComponent<Renderer>();
                renderer.material.color = Color.Lerp(dashTrailColor, Color.white, Random.value);

                StartCoroutine(AnimateDashParticle(particle));
            }
        }

        IEnumerator AnimateDashParticle(GameObject particle)
        {
            var startScale = particle.transform.localScale;
            var velocity = Random.insideUnitSphere * 2f;
            velocity.y = Mathf.Abs(velocity.y);

            var renderer = particle.GetComponent<Renderer>();
            var startColor = renderer.material.color;

            float timer = 0f;
            float duration = 0.4f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float normalizedTime = timer / duration;

                particle.transform.position += velocity * Time.deltaTime;
                particle.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, normalizedTime);

                float alpha = 1f - normalizedTime;
                var color = startColor;
                color.a = alpha;
                renderer.material.SetColor(ColorProperty, color);

                velocity *= 0.98f;
                yield return null;
            }

            particle.SetActive(false);
            particlePool.Enqueue(particle);
        }

        void CreateEnhancedTeleportEffect(Vector3 position, bool isDeparture)
        {
            PlaySFX(teleportSFX);

            if (teleportEffectPrefab != null && teleportEffectPool.Count > 0)
            {
                var effect = teleportEffectPool.Dequeue();
                effect.transform.position = position;
                effect.SetActive(true);
                StartCoroutine(ReturnEffectToPool(effect, teleportEffectPool, 1f));
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
                if (particlePool.Count > 0)
                {
                    var particle = particlePool.Dequeue();

                    float angle = (i / (float)teleportParticleCount) * 360f * Mathf.Deg2Rad;
                    var direction = new Vector3(Mathf.Cos(angle), Random.Range(-0.2f, 1f), Mathf.Sin(angle)).normalized;

                    particle.transform.position = position + direction * Random.Range(0.1f, 0.3f);
                    particle.transform.localScale = Vector3.one * Random.Range(0.04f, 0.12f);
                    particle.SetActive(true);

                    var renderer = particle.GetComponent<Renderer>();
                    var particleColor = Color.Lerp(teleportColor1, teleportColor2, Random.value);
                    renderer.material.SetColor(ColorProperty, particleColor);

                    StartCoroutine(AnimateEnhancedTeleportParticle(particle, direction, isDeparture));
                }
            }

            CreateGroundImpactEffect(position, isDeparture);
        }

        void CreateGroundImpactEffect(Vector3 position, bool isDeparture)
        {
            var ring = new GameObject("GroundRing");
            ring.transform.position = new Vector3(position.x, position.y - 0.5f, position.z);

            var lr = ring.AddComponent<LineRenderer>();
            lr.material = teleportMaterial;
            lr.startColor = isDeparture ? teleportColor2 : teleportColor1;
            lr.endColor = lr.startColor;
            lr.startWidth = 0.1f;
            lr.endWidth = 0.1f;
            lr.useWorldSpace = false;
            lr.loop = true;

            const int segments = 20;
            lr.positionCount = segments;
            const float radius = 0.2f;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * 2f * Mathf.PI / segments;
                var point = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                lr.SetPosition(i, point);
            }

            StartCoroutine(AnimateGroundRing(ring, lr, isDeparture));
        }

        IEnumerator AnimateGroundRing(GameObject ring, LineRenderer lr, bool isDeparture)
        {
            var startColor = lr.startColor;
            float timer = 0f;
            const float duration = 0.5f;
            const float startRadius = 0.2f;
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
                    var point = new Vector3(Mathf.Cos(angle) * currentRadius, 0, Mathf.Sin(angle) * currentRadius);
                    lr.SetPosition(i, point);
                }

                float alpha = 1f - normalizedTime;
                var color = startColor;
                color.a = alpha;
                lr.startColor = color;
                lr.endColor = color;

                yield return null;
            }

            Destroy(ring);
        }

        IEnumerator AnimateEnhancedTeleportParticle(GameObject particle, Vector3 direction, bool isDeparture)
        {
            var startPos = particle.transform.position;
            var startScale = particle.transform.localScale;
            float timer = 0f;
            float duration = isDeparture ? 0.4f : 0.3f;

            var renderer = particle.GetComponent<Renderer>();
            var originalColor = renderer.material.color;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float normalizedTime = timer / duration;

                if (isDeparture)
                {
                    var movement = direction * normalizedTime * teleportEffectRadius * 1.5f;
                    movement.y += normalizedTime * normalizedTime * 4f;
                    particle.transform.position = startPos + movement;
                    particle.transform.Rotate(Vector3.up * Time.deltaTime * 720f);
                }
                else
                {
                    var movement = direction * (1f - normalizedTime) * teleportEffectRadius;
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
                var color = originalColor;
                color.a = alpha;
                renderer.material.SetColor(ColorProperty, color);

                yield return null;
            }

            particle.SetActive(false);
            particlePool.Enqueue(particle);
        }

        IEnumerator GroundMovingCameraShake()
        {
            if (playerCamera == null) yield break;

            var originalPosition = playerCamera.transform.localPosition;
            float timer = 0f;

            while (timer < earthquakeDuration)
            {
                timer += Time.deltaTime;
                float normalizedTime = timer / earthquakeDuration;

                float curveIntensity = earthquakeCurve.Evaluate(normalizedTime);
                float currentIntensity = earthquakeIntensity * curveIntensity;

                var shakeOffset = Vector3.zero;

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
            StopAllCoroutines();

            isPerformingAttack = false;
            waitingForDashInput = false;
            dashInputReceived = false;
            currentTarget = null;

            CleanupPromptUI();
            StopCameraFollow();

            if (playerController != null)
                playerController.enabled = true;

            if (attackTrail != null)
                attackTrail.enabled = false;

            SetPlayerVisibility(true);
            Time.timeScale = 1f; // Reset time scale

            StartCoroutine(InstantTeleport(originalPosition));
            StartCoroutine(AttackCooldown());

            Debug.Log("Orbital Slash Attack cancelled!");
        }

        void EndAttack()
        {
            isPerformingAttack = false;
            currentTarget = null;

            CleanupPromptUI();
            StopCameraFollow();

            if (playerController != null)
                playerController.enabled = true;

            if (attackTrail != null)
                attackTrail.enabled = false;

            PlaySFX(attackEndSFX);
            StartCoroutine(AttackCooldown());

            Debug.Log("Dash Slash Attack completed!");
        }

        IEnumerator AttackCooldown()
        {
            canUseAttack = false;
            cooldownStartTime = Time.time;

            yield return new WaitForSeconds(cooldownDuration);

            canUseAttack = true;
            Debug.Log("Dash slash attack ready!");
        }

        // MAIN ATTACK COROUTINE
        IEnumerator PerformDashSlashAttack(GameObject target)
        {
            isPerformingAttack = true;
            canUseAttack = false;
            currentTarget = target;
            originalPosition = cachedTransform.position;
            currentHitCount = 0;

            PlaySFX(attackStartSFX);

            StartCameraFollow();

            if (playerController != null)
                playerController.enabled = false;

            if (attackTrail != null)
                attackTrail.enabled = true;

            // Perform manual dash-and-slice attacks
            for (int i = 0; i < hitsPerEnemy; i++)
            {
                if (currentTarget == null) break;

                var enemyCenter = currentTarget.transform.position;

                // 1. Teleport to random position
                var dashStartPosition = CalculateRandomDashStartPosition(enemyCenter);
                yield return StartCoroutine(InstantTeleport(dashStartPosition));

                if (isCameraFollowing)
                {
                    StartCoroutine(SmoothCameraFollow(dashStartPosition));
                }

                // 2. Brief pause
                yield return preSlashWait;

                // 3. Wait for manual dash input
                yield return StartCoroutine(WaitForDashInput());

                // If cancelled during input wait, exit
                if (!isPerformingAttack) yield break;

                // 4. Dash through enemy
                var dashEndPosition = CalculateDashThroughPosition(dashStartPosition, enemyCenter);
                yield return StartCoroutine(DashThroughEnemy(dashStartPosition, dashEndPosition, currentTarget));

                // 5. Brief pause after slash
                yield return postSlashWait;

                // 6. Teleport effect between hits
                if (i < hitsPerEnemy - 1)
                {
                    CreateEnhancedTeleportEffect(cachedTransform.position, true);
                    SetPlayerVisibility(false);
                    yield return new WaitForSeconds(0.1f);
                    SetPlayerVisibility(true);
                }
            }

            yield return new WaitForSeconds(0.3f);

            // Return to safe position
            var finalPosition = currentTarget != null ?
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
            CreateEnhancedTeleportEffect(cachedTransform.position, true);
            SetPlayerVisibility(false);

            characterController.enabled = false;
            cachedTransform.position = targetPosition;
            characterController.enabled = true;

            yield return new WaitForSeconds(0.12f);

            SetPlayerVisibility(true);
            CreateEnhancedTeleportEffect(targetPosition, false);
        }

        void PlaySFX(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, sfxVolume);
            }
        }

        // Visual debugging
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(cachedTransform.position, attackRange);

            if (currentTarget != null)
            {
                var center = currentTarget.transform.position;

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(center, dashDistance);

                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(center, hitDetectionRadius);

                for (int i = 0; i < 8; i++)
                {
                    float angle = i * 45f * Mathf.Deg2Rad;
                    var pos = center;
                    pos.x += Mathf.Cos(angle) * dashDistance;
                    pos.z += Mathf.Sin(angle) * dashDistance;
                    pos.y += groundOffset;

                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireCube(pos, Vector3.one * 0.3f);

                    var dashEnd = center + (pos - center).normalized * -dashThroughDistance;
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(pos, dashEnd);
                }
            }
        }

    // Public API
    public bool IsAttackOnCooldown() => !canUseAttack;
    public bool IsPerformingAttack() => isPerformingAttack;
    public bool IsWaitingForDashInput() => waitingForDashInput;
    public float GetCooldownTimeRemaining() => canUseAttack ? 0f : Mathf.Max(0f, cooldownDuration - (Time.time - cooldownStartTime));
    public float GetCooldownPercentage() => canUseAttack ? 0f : (Time.time - cooldownStartTime) / cooldownDuration;

    public void ForceDash()
    {
        if (waitingForDashInput)
        {
            dashInputReceived = true;
            waitingForDashInput = false;
            PlaySFX(dashStartSFX);
        }
    }
}
Vector3.Lerp(startPosition, idealCameraPosition, curveValue);
playerCamera.transform.position = currentCameraPos;

var lookDirection = (targetPosition - playerCamera.transform.position).normalized;
if (lookDirection.sqrMagnitude > 0.001f)
{
    var targetRotation = Quaternion.LookRotation(lookDirection);
    playerCamera.transform.rotation = Quaternion.Lerp(playerCamera.transform.rotation, targetRotation, curveValue);
}

yield return null;
        }
    }

    void UpdateCameraFollowDuringDash(Vector3 playerPosition)
{
    if (!enableCameraFollow || playerCamera == null || currentTarget == null) return;

    var directionToPlayer = (playerPosition - currentTarget.transform.position).normalized;
    var idealCameraPosition = playerPosition - directionToPlayer * cameraFollowDistance;
    idealCameraPosition.y = playerPosition.y + cameraHeightOffset;

    var currentCameraPos =