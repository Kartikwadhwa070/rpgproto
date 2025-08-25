using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyTeleportSystem : MonoBehaviour
{
    [Header("Teleport Settings")]
    public float teleportRange = 15f;
    public float teleportCooldown = 3f;
    public float behindDistance = 2f;
    public float teleportHeight = 0.5f;
    public KeyCode teleportKey = KeyCode.E;

    [Header("Visual Effects")]
    public float teleportFadeTime = 0.2f;
    public Color teleportEffectColor = new Color(0.5f, 0f, 1f, 0.8f);
    public AnimationCurve teleportCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Audio")]
    public AudioClip teleportSound;
    public float soundVolume = 0.7f;

    [Header("Enemy Detection")]
    public LayerMask enemyLayerMask = -1;

    private PlayerController playerController;
    private PlayerInputHandler inputHandler;
    private CharacterController characterController;
    private Camera playerCamera;
    private AudioSource audioSource;

    private bool canTeleport = true;
    private bool isTeleporting = false;
    private GameObject targetEnemy;
    private Renderer[] playerRenderers;

    // Visual effect variables
    private Material[] originalMaterials;
    private Material teleportMaterial;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        inputHandler = GetComponent<PlayerInputHandler>();
        characterController = GetComponent<CharacterController>();
        playerCamera = Camera.main;

        // Create or get AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Get player renderers for visual effects
        playerRenderers = GetComponentsInChildren<Renderer>();
        SetupTeleportMaterial();
    }

    void Start()
    {
        if (playerCamera == null)
        {
            playerCamera = FindObjectOfType<Camera>();
        }
    }

    void Update()
    {
        HandleTeleportInput();

        // Update target enemy highlighting
        if (!isTeleporting)
        {
            UpdateTargetEnemy();
        }
    }

    void SetupTeleportMaterial()
    {
        // Create a simple teleport effect material
        teleportMaterial = new Material(Shader.Find("Standard"));
        teleportMaterial.SetFloat("_Mode", 3); // Transparent mode
        teleportMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        teleportMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        teleportMaterial.SetInt("_ZWrite", 0);
        teleportMaterial.DisableKeyword("_ALPHATEST_ON");
        teleportMaterial.EnableKeyword("_ALPHABLEND_ON");
        teleportMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        teleportMaterial.renderQueue = 3000;
        teleportMaterial.color = teleportEffectColor;
        teleportMaterial.EnableKeyword("_EMISSION");
        teleportMaterial.SetColor("_EmissionColor", teleportEffectColor * 0.5f);
    }

    void HandleTeleportInput()
    {
        if (Input.GetKeyDown(teleportKey) && canTeleport && !isTeleporting && !playerController.IsDashing)
        {
            GameObject enemy = FindBestTeleportTarget();
            if (enemy != null)
            {
                StartCoroutine(TeleportBehindEnemy(enemy));
            }
            else
            {
                Debug.Log("No enemy in range to teleport behind!");
            }
        }
    }

    void UpdateTargetEnemy()
    {
        targetEnemy = FindBestTeleportTarget();

        // You can add visual feedback here, like highlighting the target enemy
        // For now, we'll just keep track of the target
    }

    GameObject FindBestTeleportTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        GameObject closestEnemy = null;
        float closestDistance = float.MaxValue;

        Vector3 playerPos = transform.position;
        Vector3 cameraForward = playerCamera.transform.forward;
        cameraForward.y = 0; // Keep it horizontal
        cameraForward.Normalize();

        foreach (GameObject enemy in enemies)
        {
            // Check if enemy is on the correct layer
            if (((1 << enemy.layer) & enemyLayerMask) == 0)
                continue;

            Vector3 enemyPos = enemy.transform.position;
            float distance = Vector3.Distance(playerPos, enemyPos);

            // Check if enemy is in range
            if (distance <= teleportRange)
            {
                // Check if enemy is roughly in front of the camera (optional - makes it more intuitive)
                Vector3 dirToEnemy = (enemyPos - playerPos).normalized;
                dirToEnemy.y = 0;

                float dotProduct = Vector3.Dot(cameraForward, dirToEnemy);

                // Prioritize enemies in view (dot > 0) but allow any enemy in range
                float priority = distance - (dotProduct > 0 ? dotProduct * 5f : 0);

                if (priority < closestDistance)
                {
                    closestDistance = priority;
                    closestEnemy = enemy;
                }
            }
        }

        return closestEnemy;
    }

    IEnumerator TeleportBehindEnemy(GameObject enemy)
    {
        if (enemy == null) yield break;

        isTeleporting = true;
        canTeleport = false;

        // Calculate position behind enemy
        Vector3 enemyPos = enemy.transform.position;
        Vector3 enemyForward = enemy.transform.forward;
        Vector3 behindPos = enemyPos - (enemyForward * behindDistance);

        // Adjust height
        behindPos.y = enemyPos.y + teleportHeight;

        // Make sure the position is valid (not inside walls, etc.)
        behindPos = ValidateTeleportPosition(behindPos, enemyPos);

        // Store original position
        Vector3 originalPos = transform.position;

        // Play sound effect
        if (teleportSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(teleportSound, soundVolume);
        }

        // Fade out effect
        yield return StartCoroutine(TeleportVisualEffect(true));

        // Disable character controller temporarily
        characterController.enabled = false;

        // Teleport instantly
        transform.position = behindPos;

        // Face the enemy
        Vector3 directionToEnemy = (enemyPos - behindPos).normalized;
        directionToEnemy.y = 0;
        if (directionToEnemy != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(directionToEnemy);
        }

        // Re-enable character controller
        characterController.enabled = true;

        // Small delay for dramatic effect
        yield return new WaitForSeconds(0.1f);

        // Fade in effect
        yield return StartCoroutine(TeleportVisualEffect(false));

        isTeleporting = false;

        // Start cooldown
        StartCoroutine(TeleportCooldown());

        Debug.Log($"Teleported behind {enemy.name}!");
    }

    Vector3 ValidateTeleportPosition(Vector3 desiredPos, Vector3 enemyPos)
    {
        // Simple validation - you can make this more sophisticated
        Vector3 validatedPos = desiredPos;

        // Raycast down to find ground
        RaycastHit hit;
        if (Physics.Raycast(desiredPos + Vector3.up * 2f, Vector3.down, out hit, 10f))
        {
            validatedPos.y = hit.point.y + 0.1f; // Slightly above ground
        }

        // Check if position is blocked
        float capsuleRadius = characterController.radius;
        float capsuleHeight = characterController.height;

        Vector3 capsuleTop = validatedPos + Vector3.up * (capsuleHeight - capsuleRadius);
        Vector3 capsuleBottom = validatedPos + Vector3.up * capsuleRadius;

        if (Physics.CheckCapsule(capsuleBottom, capsuleTop, capsuleRadius))
        {
            // If blocked, try positions around the enemy
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * behindDistance;
                Vector3 testPos = enemyPos + offset;
                testPos.y = validatedPos.y;

                capsuleTop = testPos + Vector3.up * (capsuleHeight - capsuleRadius);
                capsuleBottom = testPos + Vector3.up * capsuleRadius;

                if (!Physics.CheckCapsule(capsuleBottom, capsuleTop, capsuleRadius))
                {
                    validatedPos = testPos;
                    break;
                }
            }
        }

        return validatedPos;
    }

    IEnumerator TeleportVisualEffect(bool fadeOut)
    {
        float timer = 0f;

        // Store original materials if not already stored
        if (originalMaterials == null && playerRenderers.Length > 0)
        {
            List<Material> mats = new List<Material>();
            foreach (Renderer renderer in playerRenderers)
            {
                mats.AddRange(renderer.materials);
            }
            originalMaterials = mats.ToArray();
        }

        while (timer < teleportFadeTime)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / teleportFadeTime;
            float curveValue = teleportCurve.Evaluate(normalizedTime);

            if (fadeOut)
            {
                // Fade to teleport effect
                float alpha = Mathf.Lerp(1f, 0f, curveValue);
                SetPlayerAlpha(alpha);
            }
            else
            {
                // Fade from teleport effect
                float alpha = Mathf.Lerp(0f, 1f, curveValue);
                SetPlayerAlpha(alpha);
            }

            yield return null;
        }

        // Ensure final state
        if (!fadeOut)
        {
            RestoreOriginalMaterials();
        }
    }

    void SetPlayerAlpha(float alpha)
    {
        foreach (Renderer renderer in playerRenderers)
        {
            if (renderer == null) continue;

            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (alpha <= 0.1f)
                {
                    // Use teleport material when nearly invisible
                    materials[i] = teleportMaterial;
                }
                else
                {
                    // Modify alpha of current material
                    Color color = materials[i].color;
                    color.a = alpha;
                    materials[i].color = color;
                }
            }
            renderer.materials = materials;
        }
    }

    void RestoreOriginalMaterials()
    {
        if (originalMaterials == null) return;

        int matIndex = 0;
        foreach (Renderer renderer in playerRenderers)
        {
            if (renderer == null) continue;

            Material[] rendererMats = new Material[renderer.materials.Length];
            for (int i = 0; i < rendererMats.Length && matIndex < originalMaterials.Length; i++)
            {
                rendererMats[i] = originalMaterials[matIndex];
                matIndex++;
            }
            renderer.materials = rendererMats;
        }
    }

    IEnumerator TeleportCooldown()
    {
        yield return new WaitForSeconds(teleportCooldown);
        canTeleport = true;
        Debug.Log("Teleport ready!");
    }

    // Public getters for UI or other systems
    public bool CanTeleport => canTeleport && !isTeleporting;
    public bool IsTeleporting => isTeleporting;
    public GameObject CurrentTarget => targetEnemy;
    public float TeleportCooldownRemaining => canTeleport ? 0f : teleportCooldown;

    // Gizmos for debugging
    void OnDrawGizmosSelected()
    {
        // Draw teleport range
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, teleportRange);

        // Draw line to current target
        if (targetEnemy != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, targetEnemy.transform.position);

            // Draw teleport position preview
            Vector3 enemyPos = targetEnemy.transform.position;
            Vector3 enemyForward = targetEnemy.transform.forward;
            Vector3 behindPos = enemyPos - (enemyForward * behindDistance);
            behindPos.y = enemyPos.y + teleportHeight;

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(behindPos, Vector3.one * 0.5f);
        }
    }
}