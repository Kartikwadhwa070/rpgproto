using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FloatingSwordSystem : MonoBehaviour
{
    [Header("Sword Settings")]
    public GameObject swordPrefab;
    public int swordCount = 6;
    public float floatDistance = 2f;
    public float floatHeight = 0.5f;

    [Header("Visual Settings")]
    public float rotationSpeed = 30f;
    public float bobSpeed = 2f;
    public float bobAmount = 0.2f;
    public float shootRotationSpeed = 180f; // Rotation speed during flight

    [Header("Shooting Settings")]
    public float shootSpeed = 20f;
    public float shootRange = 100f;
    public float swordReturnDelay = 3f;
    public float shootTiltAngle = 45f; // Angle to tilt sword forward when shooting
    public LayerMask enemyLayer = -1;
    public string enemyTag = "Enemy";

    [Header("Battle Mode Settings")]
    public KeyCode battleModeToggleKey = KeyCode.X;
    public float battleModeTransitionTime = 1f;
    public AnimationCurve battleModeTransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Dramatic Entry Settings")]
    public float entryRiseHeight = 5f; // How high above player to rise
    public float entryRiseSpeed = 8f; // Speed of initial rise
    public float entryPauseTime = 0.5f; // Pause time at peak before duplication
    public float duplicationTime = 1f; // Time for duplication effect
    public float spreadToPositionTime = 1.5f; // Time to spread to final positions
    public AnimationCurve entryRiseCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));
    public AnimationCurve spreadCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));

    [Header("References")]
    public Camera playerCamera;
    public Transform shootOrigin;
    public CameraController cameraController;

    private List<SwordController> swords = new List<SwordController>();
    private float rotationAngle = 0f;
    private bool isBattleMode = false;
    private bool isTransitioning = false;
    private float transitionTimer = 0f;
    private bool isPerformingDramaticEntry = false;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (shootOrigin == null)
            shootOrigin = playerCamera.transform;

        if (cameraController == null)
            cameraController = playerCamera.GetComponent<CameraController>();

        CreateSwords();

        // Start in non-battle mode (swords hidden)
        SetBattleMode(false, true); // immediate = true for initial state
    }

    void Update()
    {
        HandleBattleModeInput();
        HandleTransition();
        if (!isPerformingDramaticEntry)
        {
            UpdateSwordPositions();
        }
        HandleInput();
    }

    void CreateSwords()
    {
        // Start with empty list - swords will be created when needed
        swords.Clear();
    }

    void CreateAllSwords()
    {
        // Only create swords if we don't have the full count
        if (swords.Count >= swordCount) return;

        // Clear existing swords first
        for (int i = 0; i < swords.Count; i++)
        {
            if (swords[i] != null && swords[i].gameObject != null)
            {
                Destroy(swords[i].gameObject);
            }
        }
        swords.Clear();

        // Create all swords at a position above the player to avoid physics conflicts
        Vector3 spawnPosition = transform.position + Vector3.up * (entryRiseHeight + 2f);

        for (int i = 0; i < swordCount; i++)
        {
            GameObject swordObj = Instantiate(swordPrefab, spawnPosition, Quaternion.identity);
            SwordController sword = swordObj.GetComponent<SwordController>();

            if (sword == null)
                sword = swordObj.AddComponent<SwordController>();

            sword.Initialize(this, i);
            swords.Add(sword);
        }
    }

    void HandleBattleModeInput()
    {
        if (Input.GetKeyDown(battleModeToggleKey) && !isTransitioning && !isPerformingDramaticEntry)
        {
            ToggleBattleMode();
        }
    }

    void ToggleBattleMode()
    {
        SetBattleMode(!isBattleMode, false);
        Debug.Log($"Battle Mode: {(isBattleMode ? "ON" : "OFF")}");
    }

    void SetBattleMode(bool enabled, bool immediate = false)
    {
        if (isBattleMode == enabled && !immediate) return;

        isBattleMode = enabled;

        if (immediate)
        {
            // Instant transition
            if (enabled)
            {
                // Create all swords if they don't exist
                CreateAllSwords();
                foreach (SwordController sword in swords)
                {
                    sword.SetBattleMode(enabled, 1f);
                }
            }
            else
            {
                foreach (SwordController sword in swords)
                {
                    sword.SetBattleMode(enabled, 0f);
                }
            }
            isTransitioning = false;
        }
        else
        {
            if (enabled)
            {
                // Entering battle mode - perform dramatic entry
                CreateAllSwords();
                StartCoroutine(PerformDramaticEntry());
            }
            else
            {
                // Exiting battle mode - perform dramatic exit
                StartCoroutine(PerformDramaticExit());
            }
        }
    }

    IEnumerator PerformDramaticEntry()
    {
        isPerformingDramaticEntry = true;

        // Start all swords well above the player to avoid physics conflicts
        Vector3 startPos = transform.position + Vector3.up * (entryRiseHeight + 2f);
        Vector3 riseTarget = transform.position + Vector3.up * entryRiseHeight;

        // Initialize all swords at the high starting position (invisible initially)
        for (int i = 0; i < swords.Count; i++)
        {
            swords[i].SetBattleMode(true, 0f);
            swords[i].SetEntryPosition(startPos);
        }

        // Phase 1: Make first sword visible and descend to rise height
        swords[0].SetBattleMode(true, 1f);

        float descendTimer = 0f;
        float descendDuration = 2f / entryRiseSpeed; // Time to descend from high position to rise target

        while (descendTimer < descendDuration)
        {
            descendTimer += Time.deltaTime;
            float normalizedTime = descendTimer / descendDuration;
            float curveValue = entryRiseCurve.Evaluate(normalizedTime);

            Vector3 currentPos = Vector3.Lerp(startPos, riseTarget, curveValue);

            // Move all swords to this position (only first is visible)
            for (int i = 0; i < swords.Count; i++)
            {
                swords[i].SetEntryPosition(currentPos);
            }

            yield return null;
        }

        // Ensure all swords are at the rise target position
        for (int i = 0; i < swords.Count; i++)
        {
            swords[i].SetEntryPosition(riseTarget);
        }

        // Phase 2: Pause at peak
        yield return new WaitForSeconds(entryPauseTime);

        // Phase 3: Duplication effect (fade in the other swords)
        Vector3 peakPosition = riseTarget;
        float duplicationTimer = 0f;

        while (duplicationTimer < duplicationTime)
        {
            duplicationTimer += Time.deltaTime;
            float normalizedTime = duplicationTimer / duplicationTime;

            for (int i = 1; i < swords.Count; i++)
            {
                // Stagger the appearance of each sword
                float swordDelay = (float)(i - 1) / (swordCount - 1) * 0.5f;
                float swordTime = Mathf.Clamp01((normalizedTime - swordDelay) * 2f); // Speed up individual appearances

                swords[i].SetBattleMode(true, swordTime);

                // Add a slight position offset during duplication for effect
                Vector3 offset = Vector3.up * Mathf.Sin(normalizedTime * Mathf.PI * 3) * 0.1f;
                swords[i].SetEntryPosition(peakPosition + offset);
            }

            yield return null;
        }

        // Ensure all swords are fully visible
        for (int i = 0; i < swords.Count; i++)
        {
            swords[i].SetBattleMode(true, 1f);
            swords[i].SetEntryPosition(peakPosition);
        }

        // Phase 4: Spread to final positions
        float spreadTimer = 0f;

        while (spreadTimer < spreadToPositionTime)
        {
            spreadTimer += Time.deltaTime;
            float normalizedTime = spreadTimer / spreadToPositionTime;
            float curveValue = spreadCurve.Evaluate(normalizedTime);

            // Calculate final positions for each sword
            for (int i = 0; i < swords.Count; i++)
            {
                float angle = (360f / swordCount) * i;
                Vector3 finalOffset = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * floatDistance,
                    floatHeight,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * floatDistance
                );
                Vector3 finalPosition = transform.position + finalOffset;

                // Interpolate from peak position to final position
                Vector3 currentPos = Vector3.Lerp(peakPosition, finalPosition, curveValue);
                swords[i].SetEntryPosition(currentPos);

                // Also set rotation
                Quaternion finalRotation = Quaternion.LookRotation(finalOffset.normalized);
                swords[i].SetEntryRotation(finalRotation);
            }

            yield return null;
        }

        // Phase 5: Switch to normal floating mode
        foreach (SwordController sword in swords)
        {
            sword.SetFloatingMode(true);
        }

        isPerformingDramaticEntry = false;
        isBattleMode = true;
    }

    IEnumerator PerformDramaticExit()
    {
        isPerformingDramaticEntry = true;
        isBattleMode = false;

        // Phase 1: Gather to center position above player
        Vector3 gatherTarget = transform.position + Vector3.up * entryRiseHeight;
        float gatherTimer = 0f;

        // Switch all swords to entry mode
        foreach (SwordController sword in swords)
        {
            sword.SetFloatingMode(false);
        }

        while (gatherTimer < spreadToPositionTime)
        {
            gatherTimer += Time.deltaTime;
            float normalizedTime = gatherTimer / spreadToPositionTime;
            float curveValue = spreadCurve.Evaluate(1f - normalizedTime); // Reverse the curve

            // Move all swords from their current positions to the gather point
            for (int i = 0; i < swords.Count; i++)
            {
                float angle = (360f / swordCount) * i + rotationAngle;
                Vector3 currentFloatOffset = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * floatDistance,
                    floatHeight,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * floatDistance
                );
                Vector3 currentFloatPosition = transform.position + currentFloatOffset;

                // Interpolate from current floating position to gather target
                Vector3 currentPos = Vector3.Lerp(currentFloatPosition, gatherTarget, 1f - curveValue);
                swords[i].SetEntryPosition(currentPos);
            }

            yield return null;
        }

        // Phase 2: Pause at peak
        yield return new WaitForSeconds(entryPauseTime);

        // Phase 3: Fade out all swords except the first (reverse duplication)
        float fadeTimer = 0f;

        while (fadeTimer < duplicationTime)
        {
            fadeTimer += Time.deltaTime;
            float normalizedTime = fadeTimer / duplicationTime;

            for (int i = swords.Count - 1; i >= 1; i--) // Reverse order for fade out
            {
                // Stagger the disappearance of each sword
                float swordDelay = (float)(swords.Count - 1 - i) / (swordCount - 1) * 0.5f;
                float swordTime = Mathf.Clamp01((normalizedTime - swordDelay) * 2f);
                float visibility = 1f - swordTime;

                swords[i].SetBattleMode(false, visibility);

                // Add slight movement during fade
                Vector3 offset = Vector3.up * Mathf.Sin((1f - normalizedTime) * Mathf.PI * 3) * 0.1f;
                swords[i].SetEntryPosition(gatherTarget + offset);
            }

            yield return null;
        }

        // Phase 4: Rise the remaining sword up and away before fading
        Vector3 finalExitPosition = transform.position + Vector3.up * (entryRiseHeight + 2f);
        float riseTimer = 0f;
        float riseDuration = 2f / entryRiseSpeed;

        while (riseTimer < riseDuration)
        {
            riseTimer += Time.deltaTime;
            float normalizedTime = riseTimer / riseDuration;
            float curveValue = entryRiseCurve.Evaluate(normalizedTime);

            Vector3 currentPos = Vector3.Lerp(gatherTarget, finalExitPosition, curveValue);
            swords[0].SetEntryPosition(currentPos);

            yield return null;
        }

        // Phase 5: Fade out the last sword and clean up
        float finalFadeTimer = 0f;
        float finalFadeDuration = 0.5f;

        while (finalFadeTimer < finalFadeDuration)
        {
            finalFadeTimer += Time.deltaTime;
            float visibility = 1f - (finalFadeTimer / finalFadeDuration);
            swords[0].SetBattleMode(false, visibility);

            yield return null;
        }

        // Clean up - destroy all sword objects
        for (int i = 0; i < swords.Count; i++)
        {
            if (swords[i] != null && swords[i].gameObject != null)
            {
                Destroy(swords[i].gameObject);
            }
        }
        swords.Clear();

        isPerformingDramaticEntry = false;
    }

    IEnumerator TransitionToMode()
    {
        while (transitionTimer < battleModeTransitionTime)
        {
            transitionTimer += Time.deltaTime;
            float normalizedTime = transitionTimer / battleModeTransitionTime;
            float curveValue = battleModeTransitionCurve.Evaluate(normalizedTime);

            foreach (SwordController sword in swords)
            {
                float visibility = isBattleMode ? curveValue : (1f - curveValue);
                sword.SetBattleMode(isBattleMode, visibility);
            }

            yield return null;
        }

        // Ensure final state
        foreach (SwordController sword in swords)
        {
            sword.SetBattleMode(isBattleMode, isBattleMode ? 1f : 0f);
        }

        isTransitioning = false;
    }

    void HandleTransition()
    {
        // This is handled by the coroutines, but we keep it for potential future use
    }

    void UpdateSwordPositions()
    {
        // Only update positions if in battle mode or transitioning
        if (!isBattleMode && !isTransitioning) return;

        rotationAngle += rotationSpeed * Time.deltaTime;

        for (int i = 0; i < swords.Count; i++)
        {
            if (swords[i].IsFloating)
            {
                float angle = (360f / swordCount) * i + rotationAngle;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * floatDistance,
                    floatHeight + Mathf.Sin(Time.time * bobSpeed + i) * bobAmount,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * floatDistance
                );

                swords[i].SetFloatingPosition(transform.position + offset);
                swords[i].SetFloatingRotation(Quaternion.LookRotation(offset.normalized));
            }
        }
    }

    void HandleInput()
    {
        // Only allow shooting in battle mode
        if (!isBattleMode || isPerformingDramaticEntry) return;

        if (Input.GetMouseButtonDown(1)) // Right click
        {
            // Check if camera is ready for shooting (cursor locked)
            if (cameraController != null && !cameraController.IsReadyForShooting())
            {
                Debug.Log("Lock cursor first to shoot swords!");
                return;
            }

            ShootSword();
        }
    }

    void ShootSword()
    {
        SwordController availableSword = GetAvailableSword();
        if (availableSword == null) return;

        // Raycast from camera center to find target
        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        RaycastHit hit;

        Vector3 targetPoint;
        if (Physics.Raycast(ray, out hit, shootRange, enemyLayer))
        {
            targetPoint = hit.point;

            // Check if hit object is an enemy
            if (hit.collider.CompareTag(enemyTag))
            {
                // Handle enemy hit (you can expand this)
                Debug.Log("Hit enemy: " + hit.collider.name);
            }
        }
        else
        {
            targetPoint = ray.origin + ray.direction * shootRange;
        }

        // Shoot from sword's current position toward target
        availableSword.ShootFromCurrentPosition(targetPoint, shootSpeed, shootTiltAngle, shootRotationSpeed);
        StartCoroutine(ReturnSwordAfterDelay(availableSword, swordReturnDelay));
    }

    SwordController GetAvailableSword()
    {
        foreach (SwordController sword in swords)
        {
            if (sword.IsFloating)
                return sword;
        }
        return null;
    }

    IEnumerator ReturnSwordAfterDelay(SwordController sword, float delay)
    {
        yield return new WaitForSeconds(delay);
        sword.ReturnToFloat();
    }

    // Public getters
    public bool IsBattleMode => isBattleMode;
    public bool IsTransitioning => isTransitioning;
    public bool IsPerformingDramaticEntry => isPerformingDramaticEntry;
}

public class SwordController : MonoBehaviour
{
    private FloatingSwordSystem swordSystem;
    private int swordIndex;
    private bool isFloating = true;
    private bool isInEntryMode = false;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 entryPosition;
    private Quaternion entryRotation;
    private Vector3 shootTarget;
    private float shootSpeed;
    private float shootRotationSpeed;
    private bool isShooting = false;
    private Quaternion initialShootRotation;

    // Battle mode variables
    private bool battleModeActive = false;
    private float currentVisibility = 1f;
    private Renderer[] swordRenderers;
    private Material[] originalMaterials;
    private Material[] fadeMaterials;

    public bool IsFloating => isFloating;

    void Update()
    {
        if (isShooting)
        {
            MoveDuringShoot();
        }
        else if (isInEntryMode)
        {
            MoveToEntryPosition();
        }
        else if (isFloating)
        {
            MoveToFloatingPosition();
        }
    }

    public void Initialize(FloatingSwordSystem system, int index)
    {
        swordSystem = system;
        swordIndex = index;

        // Add a rigidbody if it doesn't exist and configure it properly
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.useGravity = false;
        rb.isKinematic = true;

        // Add collider if it doesn't exist and make it a trigger to avoid physics conflicts
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            col = gameObject.AddComponent<CapsuleCollider>();
        }
        col.isTrigger = true; // Make it a trigger to avoid physics conflicts during movement

        // Setup visibility system
        SetupVisibilitySystem();
    }

    void SetupVisibilitySystem()
    {
        // Get all renderers on this sword
        swordRenderers = GetComponentsInChildren<Renderer>();

        if (swordRenderers.Length > 0)
        {
            originalMaterials = new Material[swordRenderers.Length];
            fadeMaterials = new Material[swordRenderers.Length];

            for (int i = 0; i < swordRenderers.Length; i++)
            {
                if (swordRenderers[i] != null && swordRenderers[i].material != null)
                {
                    originalMaterials[i] = swordRenderers[i].material;

                    // Create fade material copy
                    fadeMaterials[i] = new Material(originalMaterials[i]);

                    // Make material support transparency
                    if (fadeMaterials[i].HasProperty("_Mode"))
                    {
                        fadeMaterials[i].SetFloat("_Mode", 2); // Fade mode
                        fadeMaterials[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        fadeMaterials[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        fadeMaterials[i].SetInt("_ZWrite", 0);
                        fadeMaterials[i].DisableKeyword("_ALPHATEST_ON");
                        fadeMaterials[i].EnableKeyword("_ALPHABLEND_ON");
                        fadeMaterials[i].DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        fadeMaterials[i].renderQueue = 3000;
                    }
                }
            }
        }
    }

    public void SetBattleMode(bool enabled, float visibility)
    {
        battleModeActive = enabled;
        currentVisibility = visibility;

        // Update visual state
        for (int i = 0; i < swordRenderers.Length; i++)
        {
            if (swordRenderers[i] != null)
            {
                if (visibility <= 0f)
                {
                    // Completely invisible
                    swordRenderers[i].enabled = false;
                }
                else
                {
                    swordRenderers[i].enabled = true;

                    if (visibility >= 1f)
                    {
                        // Fully visible - use original material
                        if (originalMaterials[i] != null)
                        {
                            swordRenderers[i].material = originalMaterials[i];
                        }
                    }
                    else
                    {
                        // Partially visible - use fade material
                        if (fadeMaterials[i] != null)
                        {
                            swordRenderers[i].material = fadeMaterials[i];
                            Color color = fadeMaterials[i].color;
                            color.a = visibility;
                            fadeMaterials[i].color = color;
                        }
                    }
                }
            }
        }

        // Update collider based on battle mode - keep as trigger to avoid physics issues
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = battleModeActive && visibility > 0.5f;
            col.isTrigger = true; // Always keep as trigger for movement phases
        }
    }

    public void SetFloatingPosition(Vector3 position)
    {
        targetPosition = position;
    }

    public void SetFloatingRotation(Quaternion rotation)
    {
        targetRotation = rotation;
    }

    public void SetEntryPosition(Vector3 position)
    {
        entryPosition = position;
        isInEntryMode = true;
    }

    public void SetEntryRotation(Quaternion rotation)
    {
        entryRotation = rotation;
    }

    public void SetFloatingMode(bool enabled)
    {
        isInEntryMode = !enabled;
        isFloating = enabled;
    }

    void MoveToEntryPosition()
    {
        transform.position = Vector3.Lerp(transform.position, entryPosition, Time.deltaTime * 8f);
        if (entryRotation != Quaternion.identity)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, entryRotation, Time.deltaTime * 8f);
        }
    }

    void MoveToFloatingPosition()
    {
        // Only move if in battle mode or transitioning
        if (!battleModeActive && currentVisibility <= 0f) return;

        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 5f);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
    }

    public void ShootFromCurrentPosition(Vector3 target, float speed, float tiltAngle, float rotSpeed)
    {
        isFloating = false;
        isShooting = true;
        isInEntryMode = false;
        shootTarget = target;
        shootSpeed = speed;
        shootRotationSpeed = rotSpeed;

        // Calculate direction to target
        Vector3 direction = (target - transform.position).normalized;

        // Create rotation that points toward target with tilt
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        // Tilt the sword forward (around the right axis) so the sharp end points forward
        Quaternion tiltRotation = Quaternion.AngleAxis(-tiltAngle, Vector3.right);
        initialShootRotation = lookRotation * tiltRotation;
        transform.rotation = initialShootRotation;

        // Enable collider for shooting but keep it as trigger initially
        Collider swordCollider = GetComponent<Collider>();
        if (swordCollider != null)
        {
            swordCollider.enabled = true;
            swordCollider.isTrigger = true; // Keep as trigger to avoid physics conflicts during flight
        }
    }

    void MoveDuringShoot()
    {
        Vector3 direction = (shootTarget - transform.position).normalized;
        transform.position += direction * shootSpeed * Time.deltaTime;

        // Add spinning rotation during flight for visual effect
        if (shootRotationSpeed > 0)
        {
            Quaternion spinRotation = Quaternion.AngleAxis(shootRotationSpeed * Time.deltaTime, transform.forward);
            transform.rotation = spinRotation * transform.rotation;
        }

        // Check if we've reached the target or passed it
        if (Vector3.Distance(transform.position, shootTarget) < 0.5f)
        {
            isShooting = false;
            // Handle hitting the target
            OnReachTarget();
        }
    }

    void OnReachTarget()
    {
        // Perform a small raycast check for enemies at target location
        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, 1f);
        foreach (Collider col in hitEnemies)
        {
            if (col.CompareTag(swordSystem.enemyTag))
            {
                EnemyHealth enemyHealth = col.GetComponent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.TakeDamage(25f);
                }
                Debug.Log("Sword hit enemy: " + col.name);
                break; // Only hit one enemy per sword
            }
        }
    }

    public void ReturnToFloat()
    {
        isFloating = true;
        isShooting = false;
        isInEntryMode = false;

        // Reset collider to trigger mode for floating
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        // Smoothly return to floating rotation (will be handled in MoveToFloatingPosition)
        // The floating rotation will be set by the main system
    }

    void OnTriggerEnter(Collider other)
    {
        if (isShooting && other.CompareTag(swordSystem.enemyTag))
        {
            // Additional hit detection if needed
            Debug.Log("Sword hit: " + other.name);
        }
    }
}

// Optional: Simple enemy health script for testing
public class EnemyHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        Debug.Log($"{gameObject.name} took {damage} damage. Health: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log($"{gameObject.name} died!");
    }
}