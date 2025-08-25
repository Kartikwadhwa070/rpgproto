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

    [Header("Afterimage Settings")]
    public bool enableAfterImage = true;
    public int afterImageCount = 3;
    public float afterImageInterval = 0.05f;
    public float afterImageLifetime = 0.4f;
    public Color afterImageColor = new Color(0.5f, 0f, 1f, 0.5f);

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

        if (!isTeleporting)
        {
            UpdateTargetEnemy();
        }
    }

    void SetupTeleportMaterial()
    {
        if (playerRenderers.Length > 0)
        {
            List<Material> mats = new List<Material>();
            foreach (Renderer renderer in playerRenderers)
            {
                if (renderer != null)
                {
                    mats.AddRange(renderer.materials);
                }
            }
            originalMaterials = mats.ToArray();
        }

        teleportMaterial = new Material(Shader.Find("Standard"));
        teleportMaterial.SetFloat("_Mode", 3);
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
    }

    GameObject FindBestTeleportTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        GameObject closestEnemy = null;
        float closestDistance = float.MaxValue;

        Vector3 playerPos = transform.position;
        Vector3 cameraForward = playerCamera.transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        foreach (GameObject enemy in enemies)
        {
            if (((1 << enemy.layer) & enemyLayerMask) == 0)
                continue;

            Vector3 enemyPos = enemy.transform.position;
            float distance = Vector3.Distance(playerPos, enemyPos);

            if (distance <= teleportRange)
            {
                Vector3 dirToEnemy = (enemyPos - playerPos).normalized;
                dirToEnemy.y = 0;
                float dotProduct = Vector3.Dot(cameraForward, dirToEnemy);
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

        Vector3 enemyPos = enemy.transform.position;
        Vector3 enemyForward = enemy.transform.forward;
        Vector3 behindPos = enemyPos - (enemyForward * behindDistance);
        behindPos.y = enemyPos.y + teleportHeight;

        behindPos = ValidateTeleportPosition(behindPos, enemyPos);

        Vector3 originalPos = transform.position;

        if (teleportSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(teleportSound, soundVolume);
        }

        // Fade out effect
        yield return StartCoroutine(TeleportVisualEffect(true));

        // Disable controller to teleport
        characterController.enabled = false;

        // --- TELEPORT INSTANTLY ---
        transform.position = behindPos;

        Vector3 directionToEnemy = (enemyPos - behindPos).normalized;
        directionToEnemy.y = 0;
        if (directionToEnemy != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(directionToEnemy);
        }

        characterController.enabled = true;

        // --- SPAWN AFTERIMAGE TRAIL ALONG PATH ---
        if (enableAfterImage)
        {
            StartCoroutine(SpawnAfterImageTrail(originalPos, behindPos, transform.rotation));
        }

        yield return new WaitForSeconds(0.1f);

        // Fade back in
        yield return StartCoroutine(TeleportVisualEffect(false));

        isTeleporting = false;

        StartCoroutine(TeleportCooldown());

        Debug.Log($"Teleported behind {enemy.name}!");
    }


    Vector3 ValidateTeleportPosition(Vector3 desiredPos, Vector3 enemyPos)
    {
        Vector3 validatedPos = desiredPos;

        RaycastHit hit;
        if (Physics.Raycast(desiredPos + Vector3.up * 2f, Vector3.down, out hit, 10f))
        {
            validatedPos.y = hit.point.y + 0.1f;
        }

        float capsuleRadius = characterController.radius;
        float capsuleHeight = characterController.height;

        Vector3 capsuleTop = validatedPos + Vector3.up * (capsuleHeight - capsuleRadius);
        Vector3 capsuleBottom = validatedPos + Vector3.up * capsuleRadius;

        if (Physics.CheckCapsule(capsuleBottom, capsuleTop, capsuleRadius))
        {
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

        while (timer < teleportFadeTime)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / teleportFadeTime;
            float curveValue = teleportCurve.Evaluate(normalizedTime);

            float alpha = fadeOut ? Mathf.Lerp(1f, 0f, curveValue) : Mathf.Lerp(0f, 1f, curveValue);
            SetPlayerAlpha(alpha);

            yield return null;
        }

        if (!fadeOut)
        {
            RestoreOriginalMaterials();
        }
    }

    void SetPlayerAlpha(float alpha)
    {
        if (alpha <= 0.1f)
        {
            foreach (Renderer renderer in playerRenderers)
            {
                if (renderer != null) renderer.enabled = false;
            }
        }
        else
        {
            foreach (Renderer renderer in playerRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                    Material[] materials = renderer.materials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (originalMaterials != null && i < originalMaterials.Length)
                        {
                            materials[i] = new Material(originalMaterials[i]);
                            Color color = materials[i].color;
                            color.a = alpha;
                            materials[i].color = color;
                        }
                    }
                    renderer.materials = materials;
                }
            }
        }
    }

    void RestoreOriginalMaterials()
    {
        if (originalMaterials == null) return;

        int matIndex = 0;
        foreach (Renderer renderer in playerRenderers)
        {
            if (renderer == null) continue;

            renderer.enabled = true;

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

    IEnumerator SpawnAfterImageTrail(Vector3 startPos, Vector3 endPos, Quaternion rot)
    {
        for (int i = 0; i < afterImageCount; i++)
        {
            float t = (float)i / (afterImageCount - 1); // normalize 0 → 1
            Vector3 spawnPos = Vector3.Lerp(startPos, endPos, t);

            CreateAfterImage(spawnPos, rot);

            yield return new WaitForSeconds(afterImageInterval);
        }
    }


    void CreateAfterImage(Vector3 pos, Quaternion rot)
    {
        GameObject ghost = new GameObject("AfterImage");
        ghost.transform.position = pos;
        ghost.transform.rotation = rot;

        foreach (Renderer r in playerRenderers)
        {
            if (r == null) continue;
            GameObject ghostPart = new GameObject("GhostPart");
            ghostPart.transform.SetParent(ghost.transform);
            ghostPart.transform.position = r.transform.position;
            ghostPart.transform.rotation = r.transform.rotation;
            ghostPart.transform.localScale = r.transform.localScale;

            MeshFilter mf = ghostPart.AddComponent<MeshFilter>();
            MeshRenderer mr = ghostPart.AddComponent<MeshRenderer>();

            MeshFilter originalMF = r.GetComponent<MeshFilter>();
            if (originalMF != null)
            {
                mf.mesh = originalMF.sharedMesh;
            }

            Material mat = new Material(r.material);
            mat.color = afterImageColor;
            mr.material = mat;
        }
        StartCoroutine(FadeAfterImage(ghost));
    }

    IEnumerator FadeAfterImage(GameObject ghost)
    {
        float timer = 0f;
        Renderer[] renderers = ghost.GetComponentsInChildren<Renderer>();

        while (timer < afterImageLifetime)
        {
            timer += Time.deltaTime;
            float t = timer / afterImageLifetime;

            foreach (Renderer r in renderers)
            {
                foreach (Material m in r.materials)
                {
                    if (m.HasProperty("_Color"))
                    {
                        Color c = m.color;
                        c.a = Mathf.Lerp(afterImageColor.a, 0f, t);
                        m.color = c;
                    }
                }
            }
            yield return null;
        }
        Destroy(ghost);
    }

    public bool CanTeleport => canTeleport && !isTeleporting;
    public bool IsTeleporting => isTeleporting;
    public GameObject CurrentTarget => targetEnemy;
    public float TeleportCooldownRemaining => canTeleport ? 0f : teleportCooldown;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, teleportRange);

        if (targetEnemy != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, targetEnemy.transform.position);

            Vector3 enemyPos = targetEnemy.transform.position;
            Vector3 enemyForward = targetEnemy.transform.forward;
            Vector3 behindPos = enemyPos - (enemyForward * behindDistance);
            behindPos.y = enemyPos.y + teleportHeight;

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(behindPos, Vector3.one * 0.5f);
        }
    }
}
