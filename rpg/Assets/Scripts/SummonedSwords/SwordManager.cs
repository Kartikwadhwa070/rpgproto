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

    [Header("Sword Orientation")]
    [Range(-90f, 90f)]
    public float swordPitchAngle = 0f; // Up/down tilt of swords
    [Range(0f, 360f)]
    public float swordYawOffset = 0f; // Additional rotation around Y axis
    [Range(-45f, 45f)]
    public float swordRollAngle = 0f; // Banking angle of swords
    public bool pointSwordsOutward = true; // Point sharp end away from player
    public bool alignWithMovement = false; // Align swords with orbital movement

    [Header("Visual Settings")]
    public float rotationSpeed = 30f;
    public float bobSpeed = 2f;
    public float bobAmount = 0.2f;
    public float shootRotationSpeed = 180f;

    [Header("Shooting Settings")]
    public float shootSpeed = 20f;
    public float shootRange = 100f;
    public float swordReturnDelay = 3f;
    public float shootTiltAngle = 45f;
    public LayerMask enemyLayer = -1;
    public string enemyTag = "Enemy";

    [Header("Battle Mode Settings")]
    public KeyCode battleModeToggleKey = KeyCode.X;
    public float battleModeTransitionTime = 1f;
    public AnimationCurve battleModeTransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Magic Circle")]
    public GameObject magicCirclePrefab; // Assign your magic circle model here
    public float circleHeight = 3f; // Height above player
    public float circleScale = 1f; // Scale of the magic circle
    public float circleSpinSpeed = 45f; // Rotation speed of the circle
    public bool reverseCircleRotation = false;
    public AnimationCurve circleFadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float circleGlowIntensity = 2f; // For emissive materials

    [Header("Enhanced Entry Animation")]
    public float entryRiseHeight = 5f;
    public float entryRiseSpeed = 8f;
    public float entryPauseTime = 0.8f;
    public float duplicationTime = 2f;
    public float spreadToPositionTime = 2f;
    
    [Header("Entry Effects")]
    public float swordSpinDuringEntry = 720f; // Degrees to spin during entry
    public float swordGlowDuringEntry = 3f; // Glow intensity during entry
    public AnimationCurve entrySpinCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve entryGlowCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Spiral Entry Pattern")]
    public bool useSpiralEntry = true;
    public float spiralRadius = 1f;
    public float spiralSpeed = 2f;
    public int spiralTurns = 3;
    
    [Header("Exit Effects")]
    public bool useVortexExit = true;
    public float vortexSpinSpeed = 360f;
    public float vortexPullSpeed = 5f;
    public AnimationCurve vortexCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Animation Curves")]
    public AnimationCurve entryRiseCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));
    public AnimationCurve spreadCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));

    [Header("References")]
    public Camera playerCamera;
    public Transform shootOrigin;
    public CameraController cameraController;

    private List<SwordController> swords = new List<SwordController>();
    private GameObject magicCircleInstance;
    private float rotationAngle = 0f;
    private bool isBattleMode = false;
    private bool isTransitioning = false;
    private float transitionTimer = 0f;
    private bool isPerformingDramaticEntry = false;
    private MagicCircleController circleController;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (shootOrigin == null)
            shootOrigin = playerCamera.transform;

        if (cameraController == null)
            cameraController = playerCamera.GetComponent<CameraController>();

        CreateSwords();
        SetBattleMode(false, true);
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
        UpdateMagicCircle();
    }

    void CreateSwords()
    {
        swords.Clear();
    }

    void CreateAllSwords()
    {
        if (swords.Count >= swordCount) return;

        // Clear existing swords
        for (int i = 0; i < swords.Count; i++)
        {
            if (swords[i] != null && swords[i].gameObject != null)
            {
                Destroy(swords[i].gameObject);
            }
        }
        swords.Clear();

        // Create magic circle if prefab is assigned
        if (magicCirclePrefab != null && magicCircleInstance == null)
        {
            Vector3 circlePosition = transform.position + Vector3.up * circleHeight;
            magicCircleInstance = Instantiate(magicCirclePrefab, circlePosition, Quaternion.identity);
            magicCircleInstance.transform.localScale = Vector3.one * circleScale;
            
            circleController = magicCircleInstance.GetComponent<MagicCircleController>();
            if (circleController == null)
                circleController = magicCircleInstance.AddComponent<MagicCircleController>();
            
            circleController.Initialize(circleGlowIntensity);
            circleController.SetVisibility(0f); // Start invisible
        }

        // Create swords at magic circle position
        Vector3 spawnPosition = transform.position + Vector3.up * circleHeight;

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

    void UpdateMagicCircle()
    {
        if (magicCircleInstance != null && isBattleMode)
        {
            // Keep circle above player
            Vector3 targetPosition = transform.position + Vector3.up * circleHeight;
            magicCircleInstance.transform.position = Vector3.Lerp(
                magicCircleInstance.transform.position, 
                targetPosition, 
                Time.deltaTime * 5f
            );

            // Rotate the circle
            float spinDirection = reverseCircleRotation ? -1f : 1f;
            magicCircleInstance.transform.Rotate(Vector3.up, circleSpinSpeed * spinDirection * Time.deltaTime);
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
            if (enabled)
            {
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
                CreateAllSwords();
                StartCoroutine(PerformEnhancedDramaticEntry());
            }
            else
            {
                StartCoroutine(PerformEnhancedDramaticExit());
            }
        }
    }

    IEnumerator PerformEnhancedDramaticEntry()
    {
        isPerformingDramaticEntry = true;

        // Phase 0: Fade in magic circle with dramatic effect
        if (circleController != null)
        {
            float circleFadeTime = 1f;
            float timer = 0f;

            while (timer < circleFadeTime)
            {
                timer += Time.deltaTime;
                float normalizedTime = timer / circleFadeTime;
                float curveValue = circleFadeCurve.Evaluate(normalizedTime);

                circleController.SetVisibility(curveValue);
                circleController.SetGlowIntensity(circleGlowIntensity * curveValue);

                yield return null;
            }

            circleController.SetVisibility(1f);
        }

        // Initialize all swords at circle position (invisible)
        for (int i = 0; i < swords.Count; i++)
        {
            Vector3 circlePosition = transform.position + Vector3.up * circleHeight;
            swords[i].SetBattleMode(true, 0f);
            swords[i].SetEntryPosition(circlePosition);
            swords[i].SetEntryGlow(0f);
        }

        yield return new WaitForSeconds(0.5f);

        // Phase 1: Summon first sword with dramatic effect
        swords[0].SetBattleMode(true, 1f);
        swords[0].SetEntryGlow(swordGlowDuringEntry);

        float spinTimer = 0f;
        float spinDuration = 1f;
        Quaternion initialRotation = swords[0].transform.rotation;

        while (spinTimer < spinDuration)
        {
            spinTimer += Time.deltaTime;
            float normalizedTime = spinTimer / spinDuration;
            float spinAmount = entrySpinCurve.Evaluate(normalizedTime) * swordSpinDuringEntry;

            Quaternion spinRotation = Quaternion.AngleAxis(spinAmount, Vector3.up);
            swords[0].transform.rotation = initialRotation * spinRotation;

            yield return null;
        }

        yield return new WaitForSeconds(entryPauseTime);

        // Phase 2: Enhanced duplication with spiral pattern
        float duplicationTimer = 0f;

        while (duplicationTimer < duplicationTime)
        {
            duplicationTimer += Time.deltaTime;
            float normalizedTime = duplicationTimer / duplicationTime;

            // Keep circle attached to player
            Vector3 circlePosition = transform.position + Vector3.up * circleHeight;

            for (int i = 1; i < swords.Count; i++)
            {
                float swordDelay = (float)(i - 1) / (swordCount - 1) * 0.7f;
                float swordTime = Mathf.Clamp01((normalizedTime - swordDelay) * 1.5f);

                if (swordTime > 0)
                {
                    swords[i].SetBattleMode(true, swordTime);
                    swords[i].SetEntryGlow(swordGlowDuringEntry * swordTime);

                    if (useSpiralEntry)
                    {
                        float spiralAngle = swordTime * spiralTurns * 360f + (i * 60f);
                        Vector3 spiralOffset = new Vector3(
                            Mathf.Cos(spiralAngle * Mathf.Deg2Rad) * spiralRadius * (1f - swordTime),
                            Mathf.Sin(swordTime * Mathf.PI * spiralSpeed) * 0.3f,
                            Mathf.Sin(spiralAngle * Mathf.Deg2Rad) * spiralRadius * (1f - swordTime)
                        );
                        swords[i].SetEntryPosition(circlePosition + spiralOffset);
                    }

                    float spinAmount = entrySpinCurve.Evaluate(swordTime) * swordSpinDuringEntry * 2f;
                    Quaternion entrySpinRotation = Quaternion.AngleAxis(spinAmount + (i * 45f), Vector3.up);
                    swords[i].SetEntryRotation(entrySpinRotation);
                }
            }

            yield return null;
        }

        // Ensure all swords are fully visible
        for (int i = 0; i < swords.Count; i++)
        {
            Vector3 circlePosition = transform.position + Vector3.up * circleHeight;
            swords[i].SetBattleMode(true, 1f);
            swords[i].SetEntryPosition(circlePosition);
            swords[i].SetEntryGlow(swordGlowDuringEntry);
        }

        yield return new WaitForSeconds(0.5f);

        // Phase 3: Dramatic spread to final positions
        float spreadTimer = 0f;

        while (spreadTimer < spreadToPositionTime)
        {
            spreadTimer += Time.deltaTime;
            float normalizedTime = spreadTimer / spreadToPositionTime;
            float curveValue = spreadCurve.Evaluate(normalizedTime);

            // Circle follows player
            Vector3 circlePosition = transform.position + Vector3.up * circleHeight;

            for (int i = 0; i < swords.Count; i++)
            {
                float angle = (360f / swordCount) * i;
                Vector3 finalOffset = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * floatDistance,
                    floatHeight,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * floatDistance
                );
                Vector3 finalPosition = transform.position + finalOffset;
                Quaternion finalRotation = CalculateSwordRotation(finalOffset, angle);

                Vector3 currentPos = Vector3.Lerp(circlePosition, finalPosition, curveValue);
                swords[i].SetEntryPosition(currentPos);
                swords[i].SetEntryRotation(Quaternion.Lerp(swords[i].transform.rotation, finalRotation, curveValue));

                float glowReduction = Mathf.Lerp(swordGlowDuringEntry, 0f, curveValue);
                swords[i].SetEntryGlow(glowReduction);
            }

            yield return null;
        }

        // Phase 4: Switch to normal floating mode
        foreach (SwordController sword in swords)
        {
            sword.SetFloatingMode(true);
            sword.SetEntryGlow(0f); // Remove entry glow
        }

        isPerformingDramaticEntry = false;
        isBattleMode = true;
    }


    IEnumerator PerformEnhancedDramaticExit()
    {
        isPerformingDramaticEntry = true;
        isBattleMode = false;

        Vector3 circlePosition = transform.position + Vector3.up * circleHeight;

        // Switch all swords to entry mode
        foreach (SwordController sword in swords)
        {
            sword.SetFloatingMode(false);
            sword.SetEntryGlow(swordGlowDuringEntry * 0.5f); // Add some glow for dramatic effect
        }

        // Phase 1: Vortex gather effect
        float gatherTimer = 0f;
        
        while (gatherTimer < spreadToPositionTime)
        {
            gatherTimer += Time.deltaTime;
            float normalizedTime = gatherTimer / spreadToPositionTime;
            float curveValue = useVortexExit ? vortexCurve.Evaluate(normalizedTime) : (1f - normalizedTime);

            for (int i = 0; i < swords.Count; i++)
            {
                // Current floating position
                float currentAngle = (360f / swordCount) * i + rotationAngle;
                Vector3 currentFloatOffset = new Vector3(
                    Mathf.Cos(currentAngle * Mathf.Deg2Rad) * floatDistance,
                    floatHeight,
                    Mathf.Sin(currentAngle * Mathf.Deg2Rad) * floatDistance
                );
                Vector3 currentFloatPosition = transform.position + currentFloatOffset;

                if (useVortexExit)
                {
                    // Vortex effect - spiral inward
                    float vortexRadius = floatDistance * (1f - curveValue);
                    float vortexAngle = currentAngle + (curveValue * vortexSpinSpeed * 3f);
                    Vector3 vortexOffset = new Vector3(
                        Mathf.Cos(vortexAngle * Mathf.Deg2Rad) * vortexRadius,
                        floatHeight + (curveValue * (circleHeight - floatHeight)),
                        Mathf.Sin(vortexAngle * Mathf.Deg2Rad) * vortexRadius
                    );
                    Vector3 vortexPosition = transform.position + vortexOffset;
                    swords[i].SetEntryPosition(vortexPosition);
                    
                    // Spin swords during vortex
                    Quaternion vortexRotation = Quaternion.AngleAxis(curveValue * 720f, Vector3.up);
                    swords[i].SetEntryRotation(vortexRotation);
                }
                else
                {
                    // Simple gather
                    Vector3 currentPos = Vector3.Lerp(currentFloatPosition, circlePosition, curveValue);
                    swords[i].SetEntryPosition(currentPos);
                }

                // Increase glow as they gather
                float gatherGlow = Mathf.Lerp(0f, swordGlowDuringEntry, curveValue);
                swords[i].SetEntryGlow(gatherGlow);
            }

            yield return null;
        }

        yield return new WaitForSeconds(entryPauseTime);

        // Phase 2: Reverse duplication - fade out with effects
        float fadeTimer = 0f;

        while (fadeTimer < duplicationTime)
        {
            fadeTimer += Time.deltaTime;
            float normalizedTime = fadeTimer / duplicationTime;

            for (int i = swords.Count - 1; i >= 1; i--)
            {
                float swordDelay = (float)(swords.Count - 1 - i) / (swordCount - 1) * 0.5f;
                float swordTime = Mathf.Clamp01((normalizedTime - swordDelay) * 2f);
                float visibility = 1f - swordTime;

                swords[i].SetBattleMode(false, visibility);

                // Spin and rise effect during fade
                Vector3 fadeOffset = Vector3.up * (swordTime * 2f);
                float fadeSpinAngle = swordTime * 360f * 2f;
                Quaternion fadeSpinRotation = Quaternion.AngleAxis(fadeSpinAngle, Vector3.up);
                
                swords[i].SetEntryPosition(circlePosition + fadeOffset);
                swords[i].SetEntryRotation(fadeSpinRotation);
                swords[i].SetEntryGlow(swordGlowDuringEntry * (1f - swordTime));
            }

            yield return null;
        }

        // Phase 3: Final sword dramatic exit
        Vector3 finalExitPosition = circlePosition + Vector3.up * 3f;
        float finalTimer = 0f;
        float finalDuration = 1.5f;

        while (finalTimer < finalDuration)
        {
            finalTimer += Time.deltaTime;
            float normalizedTime = finalTimer / finalDuration;
            float curveValue = entryRiseCurve.Evaluate(normalizedTime);

            Vector3 currentPos = Vector3.Lerp(circlePosition, finalExitPosition, curveValue);
            swords[0].SetEntryPosition(currentPos);

            // Final dramatic spin
            Quaternion finalSpin = Quaternion.AngleAxis(curveValue * 1080f, Vector3.up);
            swords[0].SetEntryRotation(finalSpin);

            // Fade visibility and glow
            float finalVisibility = 1f - curveValue;
            swords[0].SetBattleMode(false, finalVisibility);
            swords[0].SetEntryGlow(swordGlowDuringEntry * finalVisibility);

            yield return null;
        }

        // Phase 4: Fade out magic circle
        if (circleController != null)
        {
            float circleFadeTime = 0.8f;
            float timer = 0f;
            
            while (timer < circleFadeTime)
            {
                timer += Time.deltaTime;
                float normalizedTime = timer / circleFadeTime;
                float visibility = 1f - normalizedTime;
                
                circleController.SetVisibility(visibility);
                circleController.SetGlowIntensity(circleGlowIntensity * visibility);
                
                yield return null;
            }
        }

        // Clean up
        for (int i = 0; i < swords.Count; i++)
        {
            if (swords[i] != null && swords[i].gameObject != null)
            {
                Destroy(swords[i].gameObject);
            }
        }
        swords.Clear();

        if (magicCircleInstance != null)
        {
            Destroy(magicCircleInstance);
            magicCircleInstance = null;
            circleController = null;
        }

        isPerformingDramaticEntry = false;
    }

    void HandleTransition()
    {
        // Handled by coroutines
    }

    void UpdateSwordPositions()
    {
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
                swords[i].SetFloatingRotation(CalculateSwordRotation(offset, angle));
            }
        }
    }

    Quaternion CalculateSwordRotation(Vector3 offset, float orbitAngle)
    {
        Quaternion rotation = Quaternion.identity;

        if (pointSwordsOutward)
        {
            // Point the sword outward from the player
            rotation = Quaternion.LookRotation(offset.normalized);
        }
        else
        {
            // Point inward toward player
            rotation = Quaternion.LookRotation(-offset.normalized);
        }

        if (alignWithMovement)
        {
            // Align with orbital movement direction
            Vector3 movementDirection = new Vector3(-Mathf.Sin(orbitAngle * Mathf.Deg2Rad), 0, Mathf.Cos(orbitAngle * Mathf.Deg2Rad));
            rotation = Quaternion.LookRotation(movementDirection);
        }

        // Apply custom angles
        rotation *= Quaternion.Euler(swordPitchAngle, swordYawOffset, swordRollAngle);

        return rotation;
    }

    void HandleInput()
    {
        if (!isBattleMode || isPerformingDramaticEntry) return;

        if (Input.GetMouseButtonDown(1))
        {
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

        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        RaycastHit hit;

        Vector3 targetPoint;
        if (Physics.Raycast(ray, out hit, shootRange, enemyLayer))
        {
            targetPoint = hit.point;

            if (hit.collider.CompareTag(enemyTag))
            {
                Debug.Log("Hit enemy: " + hit.collider.name);
            }
        }
        else
        {
            targetPoint = ray.origin + ray.direction * shootRange;
        }

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

    public bool IsBattleMode => isBattleMode;
    public bool IsTransitioning => isTransitioning;
    public bool IsPerformingDramaticEntry => isPerformingDramaticEntry;
}

// Magic Circle Controller
public class MagicCircleController : MonoBehaviour
{
    private Renderer[] circleRenderers;
    private Material[] originalMaterials;
    private Material[] glowMaterials;
    private float baseGlowIntensity = 1f;

    public void Initialize(float glowIntensity)
    {
        baseGlowIntensity = glowIntensity;
        SetupGlowSystem();
    }

    void SetupGlowSystem()
    {
        circleRenderers = GetComponentsInChildren<Renderer>();
        
        if (circleRenderers.Length > 0)
        {
            originalMaterials = new Material[circleRenderers.Length];
            glowMaterials = new Material[circleRenderers.Length];

            for (int i = 0; i < circleRenderers.Length; i++)
            {
                if (circleRenderers[i] != null && circleRenderers[i].material != null)
                {
                    originalMaterials[i] = circleRenderers[i].material;
                    glowMaterials[i] = new Material(originalMaterials[i]);

                    // Setup for emission if material supports it
                    if (glowMaterials[i].HasProperty("_EmissionColor"))
                    {
                        glowMaterials[i].EnableKeyword("_EMISSION");
                    }

                    // Setup for transparency
                    if (glowMaterials[i].HasProperty("_Mode"))
                    {
                        glowMaterials[i].SetFloat("_Mode", 2); // Fade mode
                        glowMaterials[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        glowMaterials[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        glowMaterials[i].SetInt("_ZWrite", 0);
                        glowMaterials[i].DisableKeyword("_ALPHATEST_ON");
                        glowMaterials[i].EnableKeyword("_ALPHABLEND_ON");
                        glowMaterials[i].DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        glowMaterials[i].renderQueue = 3000;
                    }
                }
            }
        }
    }

    public void SetVisibility(float visibility)
    {
        for (int i = 0; i < circleRenderers.Length; i++)
        {
            if (circleRenderers[i] != null)
            {
                if (visibility <= 0f)
                {
                    circleRenderers[i].enabled = false;
                }
                else
                {
                    circleRenderers[i].enabled = true;

                    if (glowMaterials[i] != null)
                    {
                        circleRenderers[i].material = glowMaterials[i];
                        
                        Color color = glowMaterials[i].color;
                        color.a = visibility;
                        glowMaterials[i].color = color;

                        // Update emission
                        if (glowMaterials[i].HasProperty("_EmissionColor"))
                        {
                            Color emissionColor = glowMaterials[i].GetColor("_EmissionColor");
                            emissionColor *= visibility;
                            glowMaterials[i].SetColor("_EmissionColor", emissionColor);
                        }
                    }
                }
            }
        }
    }

    public void SetGlowIntensity(float intensity)
    {
        for (int i = 0; i < circleRenderers.Length; i++)
        {
            if (circleRenderers[i] != null && glowMaterials[i] != null)
            {
                if (glowMaterials[i].HasProperty("_EmissionColor"))
                {
                    Color baseEmission = originalMaterials[i].GetColor("_EmissionColor");
                    Color newEmission = baseEmission * intensity;
                    glowMaterials[i].SetColor("_EmissionColor", newEmission);
                }
            }
        }
    }
}

// Enhanced Sword Controller
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
    private Material[] glowMaterials;

    // Enhanced visual effects
    private float currentGlowIntensity = 0f;
    private bool hasGlowEffect = false;

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

        // Setup rigidbody
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.useGravity = false;
        rb.isKinematic = true;

        // Setup collider
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            col = gameObject.AddComponent<CapsuleCollider>();
        }
        col.isTrigger = true;

        SetupEnhancedVisualSystem();
    }

    void SetupEnhancedVisualSystem()
    {
        swordRenderers = GetComponentsInChildren<Renderer>();

        if (swordRenderers.Length > 0)
        {
            originalMaterials = new Material[swordRenderers.Length];
            fadeMaterials = new Material[swordRenderers.Length];
            glowMaterials = new Material[swordRenderers.Length];

            for (int i = 0; i < swordRenderers.Length; i++)
            {
                if (swordRenderers[i] != null && swordRenderers[i].material != null)
                {
                    originalMaterials[i] = swordRenderers[i].material;

                    // Create fade material
                    fadeMaterials[i] = new Material(originalMaterials[i]);
                    SetupTransparencyMaterial(fadeMaterials[i]);

                    // Create glow material
                    glowMaterials[i] = new Material(originalMaterials[i]);
                    SetupTransparencyMaterial(glowMaterials[i]);
                    
                    // Setup emission for glow effect
                    if (glowMaterials[i].HasProperty("_EmissionColor"))
                    {
                        glowMaterials[i].EnableKeyword("_EMISSION");
                        Color emissionColor = glowMaterials[i].color * 2f; // Base glow
                        glowMaterials[i].SetColor("_EmissionColor", emissionColor);
                        hasGlowEffect = true;
                    }
                }
            }
        }
    }

    void SetupTransparencyMaterial(Material mat)
    {
        if (mat.HasProperty("_Mode"))
        {
            mat.SetFloat("_Mode", 2); // Fade mode
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
    }

    public void SetBattleMode(bool enabled, float visibility)
    {
        battleModeActive = enabled;
        currentVisibility = visibility;

        UpdateVisualState();
    }

    public void SetEntryGlow(float intensity)
    {
        currentGlowIntensity = intensity;
        UpdateVisualState();
    }

    void UpdateVisualState()
    {
        for (int i = 0; i < swordRenderers.Length; i++)
        {
            if (swordRenderers[i] != null)
            {
                if (currentVisibility <= 0f)
                {
                    swordRenderers[i].enabled = false;
                }
                else
                {
                    swordRenderers[i].enabled = true;

                    Material materialToUse;
                    
                    if (currentGlowIntensity > 0f && hasGlowEffect)
                    {
                        // Use glow material
                        materialToUse = glowMaterials[i];
                        
                        // Update glow intensity
                        if (materialToUse.HasProperty("_EmissionColor"))
                        {
                            Color baseColor = originalMaterials[i].color;
                            Color emissionColor = baseColor * currentGlowIntensity;
                            materialToUse.SetColor("_EmissionColor", emissionColor);
                        }
                    }
                    else if (currentVisibility >= 1f)
                    {
                        // Use original material
                        materialToUse = originalMaterials[i];
                    }
                    else
                    {
                        // Use fade material
                        materialToUse = fadeMaterials[i];
                    }

                    swordRenderers[i].material = materialToUse;

                    // Update alpha
                    if (materialToUse != originalMaterials[i])
                    {
                        Color color = materialToUse.color;
                        color.a = currentVisibility;
                        materialToUse.color = color;
                    }
                }
            }
        }

        // Update collider
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = battleModeActive && currentVisibility > 0.5f;
            col.isTrigger = true;
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

        Vector3 direction = (target - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        Quaternion tiltRotation = Quaternion.AngleAxis(-tiltAngle, Vector3.right);
        initialShootRotation = lookRotation * tiltRotation;
        transform.rotation = initialShootRotation;

        // Add shooting glow effect
        SetEntryGlow(2f);

        Collider swordCollider = GetComponent<Collider>();
        if (swordCollider != null)
        {
            swordCollider.enabled = true;
            swordCollider.isTrigger = true;
        }
    }

    void MoveDuringShoot()
    {
        Vector3 direction = (shootTarget - transform.position).normalized;
        transform.position += direction * shootSpeed * Time.deltaTime;

        if (shootRotationSpeed > 0)
        {
            Quaternion spinRotation = Quaternion.AngleAxis(shootRotationSpeed * Time.deltaTime, transform.forward);
            transform.rotation = spinRotation * transform.rotation;
        }

        if (Vector3.Distance(transform.position, shootTarget) < 0.5f)
        {
            isShooting = false;
            OnReachTarget();
        }
    }

    void OnReachTarget()
    {
        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, 1f);
        foreach (Collider col in hitEnemies)
        {
            if (col.CompareTag(swordSystem.enemyTag))
            {
                EnemyHP enemyHP = col.GetComponent<EnemyHP>();
                if (enemyHP != null)
                {
                    enemyHP.TakeDamage(25f);
                }
                Debug.Log("Sword hit enemy: " + col.name);
                break;
            }
        }
    }

    public void ReturnToFloat()
    {
        isFloating = true;
        isShooting = false;
        isInEntryMode = false;

        // Remove shooting glow
        SetEntryGlow(0f);

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (isShooting && other.CompareTag(swordSystem.enemyTag))
        {
            Debug.Log("Sword hit: " + other.name);
        }
    }
}

