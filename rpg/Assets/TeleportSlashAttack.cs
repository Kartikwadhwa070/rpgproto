using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TeleportSlashAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    public float attackRange = 15f;
    public int numberOfSlashes = 5;
    public float slashInterval = 0.2f;
    public float damage = 25f;
    public float finalSlashDamage = 50f;
    public LayerMask enemyLayerMask = -1;

    [Header("Movement Settings")]
    public float teleportDuration = 0.1f;
    public float pauseBeforeNextSlash = 0.1f;
    public float finalPauseDuration = 0.3f;
    public AnimationCurve teleportCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Visual Effects")]
    public GameObject slashEffectPrefab;
    public GameObject teleportEffectPrefab;
    public float slashEffectDuration = 0.5f;
    public float invisibilityFlashDuration = 0.05f;

    [Header("Audio Effects")]
    public AudioSource audioSource;
    public AudioClip teleportSFX;
    public AudioClip slashSFX;
    public AudioClip ultimateStartSFX;
    public AudioClip ultimateEndSFX;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Camera Effects")]
    public bool enableCameraShake = true;
    public float cameraShakeIntensity = 0.5f;
    public float cameraShakeDuration = 0.1f;

    [Header("Input")]
    public KeyCode ultimateKey = KeyCode.Q;

    [Header("Cooldown")]
    public float cooldownDuration = 10f;

    // Cached components
    private PlayerController playerController;
    private CharacterController characterController;
    private Camera playerCamera;
    private Renderer[] playerRenderers;
    private Transform cachedTransform;

    // State management
    private bool isPerformingAttack = false;
    private bool canUseUltimate = true;
    private float cooldownStartTime;

    // Attack data
    private readonly List<GameObject> enemiesInRange = new List<GameObject>(10);
    private readonly HashSet<GameObject> hitEnemies = new HashSet<GameObject>();
    private Vector3 originalPosition;

    // Object pooling for effects
    private readonly Queue<GameObject> slashEffectPool = new Queue<GameObject>();
    private readonly Queue<GameObject> teleportEffectPool = new Queue<GameObject>();
    private const int EFFECT_POOL_SIZE = 5;

    // Cached materials and shaders
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static Material slashMaterial;
    private static Material teleportMaterial;

    // Wait instructions (reused)
    private readonly WaitForSeconds pauseWait;
    private readonly WaitForSeconds finalPauseWait;
    private readonly WaitForSeconds invisibilityWait;

    public TeleportSlashAttack()
    {
        pauseWait = new WaitForSeconds(pauseBeforeNextSlash);
        finalPauseWait = new WaitForSeconds(finalPauseDuration);
        invisibilityWait = new WaitForSeconds(invisibilityFlashDuration);
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
        InitializeEffectPools();
    }

    void Update()
    {
        if (Input.GetKeyDown(ultimateKey) && canUseUltimate && !isPerformingAttack)
        {
            StartCoroutine(PerformTeleportSlashAttack());
        }
    }

    void InitializeComponents()
    {
        playerController = GetComponent<PlayerController>();
        characterController = GetComponent<CharacterController>();
        playerCamera = Camera.main ?? FindObjectOfType<Camera>();
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
                color = Color.cyan
            };
        }

        if (teleportMaterial == null)
        {
            teleportMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                color = Color.blue
            };
        }
    }

    void InitializeEffectPools()
    {
        // Pre-instantiate effect objects for pooling
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
        }
    }

    IEnumerator PerformTeleportSlashAttack()
    {
        // Initialize attack
        isPerformingAttack = true;
        canUseUltimate = false;
        cooldownStartTime = Time.time;
        hitEnemies.Clear();
        originalPosition = cachedTransform.position;

        PlaySFX(ultimateStartSFX);

        // Disable player movement
        SetPlayerControlEnabled(false);

        // Find enemies
        FindEnemiesInRange();

        if (enemiesInRange.Count == 0)
        {
            EndAttack();
            yield break;
        }

        // Perform slashes
        int slashCount = Mathf.Min(numberOfSlashes, enemiesInRange.Count);
        for (int i = 0; i < slashCount; i++)
        {
            var target = enemiesInRange[i];
            if (target != null)
            {
                bool isFinalSlash = i == slashCount - 1;
                yield return PerformSingleSlash(target, isFinalSlash);

                if (!isFinalSlash)
                {
                    yield return pauseWait;
                }
            }
        }

        // Return to original position
        yield return finalPauseWait;
        yield return TeleportToPosition(originalPosition, true);

        PlaySFX(ultimateEndSFX);
        EndAttack();
    }

    void FindEnemiesInRange()
    {
        enemiesInRange.Clear();

        // Use NonAlloc version to avoid GC
        var colliders = new Collider[20]; // Reasonable max enemy count
        int count = Physics.OverlapSphereNonAlloc(cachedTransform.position, attackRange, colliders, enemyLayerMask);

        for (int i = 0; i < count; i++)
        {
            var col = colliders[i];
            if (col.gameObject != gameObject && col.CompareTag("Enemy"))
            {
                enemiesInRange.Add(col.gameObject);
            }
        }

        // Sort by distance using cached positions
        var playerPos = cachedTransform.position;
        enemiesInRange.Sort((a, b) =>
        {
            float distA = (a.transform.position - playerPos).sqrMagnitude; // Use sqrMagnitude for performance
            float distB = (b.transform.position - playerPos).sqrMagnitude;
            return distA.CompareTo(distB);
        });
    }

    IEnumerator PerformSingleSlash(GameObject target, bool isFinalSlash)
    {
        if (target == null) yield break;

        var targetPos = target.transform.position;
        var attackPosition = targetPos + (cachedTransform.position - targetPos).normalized * 2f + Vector3.up * 0.5f;

        // Teleport to attack position
        yield return TeleportToPosition(attackPosition, false);

        // Face target efficiently
        var lookDir = targetPos - cachedTransform.position;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            cachedTransform.rotation = Quaternion.LookRotation(lookDir);
        }

        // Effects and damage
        CreateSlashEffect(targetPos);
        PlaySFX(slashSFX);

        if (enableCameraShake && playerCamera != null)
        {
            StartCoroutine(CameraShake());
        }

        DealDamageToTarget(target, isFinalSlash ? finalSlashDamage : damage);
        StartCoroutine(InvisibilityFlash());

        yield return new WaitForSeconds(slashInterval);
    }

    IEnumerator TeleportToPosition(Vector3 targetPosition, bool isReturning)
    {
        var startPos = cachedTransform.position;
        float timer = 0f;

        CreateTeleportEffect(startPos);
        PlaySFX(teleportSFX);
        SetPlayerVisibility(false);

        // Cache component states
        bool ccEnabled = characterController.enabled;

        while (timer < teleportDuration)
        {
            timer += Time.deltaTime;
            float t = teleportCurve.Evaluate(timer / teleportDuration);
            var newPos = Vector3.Lerp(startPos, targetPosition, t);

            if (!isReturning)
            {
                newPos.y = Mathf.Max(newPos.y, targetPosition.y);
            }

            // More efficient position updates
            characterController.enabled = false;
            cachedTransform.position = newPos;
            characterController.enabled = ccEnabled;

            yield return null;
        }

        // Final position
        characterController.enabled = false;
        cachedTransform.position = targetPosition;
        characterController.enabled = ccEnabled;

        CreateTeleportEffect(targetPosition);
        SetPlayerVisibility(true);
    }

    IEnumerator InvisibilityFlash()
    {
        SetPlayerVisibility(false);
        yield return invisibilityWait;
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

    void SetPlayerControlEnabled(bool enabled)
    {
        if (playerController != null)
            playerController.enabled = enabled;
    }

    void CreateSlashEffect(Vector3 position)
    {
        if (slashEffectPrefab != null && slashEffectPool.Count > 0)
        {
            var effect = slashEffectPool.Dequeue();
            effect.transform.position = position;
            effect.SetActive(true);
            StartCoroutine(ReturnEffectToPool(effect, slashEffectPool, slashEffectDuration));
        }
        else
        {
            CreateSimpleSlashEffect(position);
        }
    }

    void CreateSimpleSlashEffect(Vector3 position)
    {
        var slashLine = new GameObject("SlashEffect");
        var t = slashLine.transform;
        t.position = position;

        var lr = slashLine.AddComponent<LineRenderer>();
        lr.material = slashMaterial;
        lr.startColor = lr.endColor = Color.cyan;
        lr.startWidth = lr.endWidth = 0.1f;
        lr.positionCount = 2;
        lr.useWorldSpace = true;

        var start = position + Vector3.up + Vector3.left;
        var end = position - Vector3.up + Vector3.right;

        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        StartCoroutine(AnimateSlashEffect(slashLine, lr));
    }

    IEnumerator AnimateSlashEffect(GameObject slashObject, LineRenderer lr)
    {
        float timer = 0f;
        var originalColor = lr.startColor;

        while (timer < slashEffectDuration)
        {
            timer += Time.deltaTime;
            float alpha = 1f - (timer / slashEffectDuration);
            var newColor = originalColor;
            newColor.a = alpha;
            lr.startColor = lr.endColor = newColor;
            yield return null;
        }

        Destroy(slashObject);
    }

    void CreateTeleportEffect(Vector3 position)
    {
        if (teleportEffectPrefab != null && teleportEffectPool.Count > 0)
        {
            var effect = teleportEffectPool.Dequeue();
            effect.transform.position = position;
            effect.SetActive(true);
            StartCoroutine(ReturnEffectToPool(effect, teleportEffectPool, 1f));
        }
        else
        {
            CreateSimpleTeleportEffect(position);
        }
    }

    IEnumerator ReturnEffectToPool(GameObject effect, Queue<GameObject> pool, float delay)
    {
        yield return new WaitForSeconds(delay);
        effect.SetActive(false);
        pool.Enqueue(effect);
    }

    void CreateSimpleTeleportEffect(Vector3 position)
    {
        const int particleCount = 8;
        for (int i = 0; i < particleCount; i++)
        {
            var particle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var t = particle.transform;
            t.position = position + Random.insideUnitSphere * 0.5f;
            t.localScale = Vector3.one * 0.1f;

            var renderer = particle.GetComponent<Renderer>();
            renderer.material = teleportMaterial;

            Destroy(particle.GetComponent<Collider>());
            StartCoroutine(AnimateTeleportParticle(particle, t, renderer));
        }
    }

    IEnumerator AnimateTeleportParticle(GameObject particle, Transform t, Renderer renderer)
    {
        var startScale = t.localScale;
        var targetScale = startScale * 3f;
        var velocity = Random.insideUnitSphere * 2f;
        float timer = 0f;
        const float duration = 0.5f;

        var mat = renderer.material;
        var originalColor = mat.color;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / duration;

            t.localScale = Vector3.Lerp(startScale, targetScale, normalizedTime);
            t.position += velocity * Time.deltaTime;

            var color = originalColor;
            color.a = 1f - normalizedTime;
            mat.SetColor(ColorProperty, color);

            yield return null;
        }

        Destroy(particle);
    }

    IEnumerator CameraShake()
    {
        if (playerCamera == null) yield break;

        var originalPos = playerCamera.transform.localPosition;
        float timer = 0f;

        while (timer < cameraShakeDuration)
        {
            timer += Time.deltaTime;
            var randomOffset = Random.insideUnitSphere * cameraShakeIntensity;
            randomOffset.z = 0;
            playerCamera.transform.localPosition = originalPos + randomOffset;
            yield return null;
        }

        playerCamera.transform.localPosition = originalPos;
    }

    void DealDamageToTarget(GameObject target, float damageAmount)
    {
        if (hitEnemies.Contains(target)) return;

        hitEnemies.Add(target);

        // Try multiple damage methods efficiently
        var components = target.GetComponents<MonoBehaviour>();
        for (int i = 0; i < components.Length; i++)
        {
            var component = components[i];
            var type = component.GetType();

            var method = type.GetMethod("TakeDamage") ?? type.GetMethod("Damage");
            if (method != null)
            {
                method.Invoke(component, new object[] { damageAmount });
                return;
            }
        }
    }

    void EndAttack()
    {
        isPerformingAttack = false;
        SetPlayerControlEnabled(true);
        StartCoroutine(UltimateCooldown());
    }

    IEnumerator UltimateCooldown()
    {
        yield return new WaitForSeconds(cooldownDuration);
        canUseUltimate = true;
    }

    void PlaySFX(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, sfxVolume);
        }
    }

    // Visualization
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (Application.isPlaying && enemiesInRange.Count > 0)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < enemiesInRange.Count; i++)
            {
                if (enemiesInRange[i] != null)
                {
                    Gizmos.DrawLine(transform.position, enemiesInRange[i].transform.position);
                }
            }
        }
    }

    // Public API
    public bool IsAttackOnCooldown() => !canUseUltimate;
    public bool IsPerformingAttack() => isPerformingAttack;
    public float GetCooldownTimeRemaining() => canUseUltimate ? 0f : Mathf.Max(0f, cooldownDuration - (Time.time - cooldownStartTime));
    public float GetCooldownPercentage() => canUseUltimate ? 0f : (Time.time - cooldownStartTime) / cooldownDuration;
}