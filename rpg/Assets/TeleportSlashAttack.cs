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
    public Material playerInvisibleMaterial;
    public float invisibilityFlashDuration = 0.05f;

    [Header("Camera Effects")]
    public bool enableCameraShake = true;
    public float cameraShakeIntensity = 0.5f;
    public float cameraShakeDuration = 0.1f;

    [Header("Input")]
    public KeyCode ultimateKey = KeyCode.Q;

    [Header("Cooldown")]
    public float cooldownDuration = 10f;

    // Private variables
    private PlayerController playerController;
    private CharacterController characterController;
    private Camera playerCamera;
    private bool isPerformingAttack = false;
    private bool canUseUltimate = true;
    private List<GameObject> enemiesInRange = new List<GameObject>();
    private Vector3 originalPosition;
    private Material[] originalMaterials;
    private Renderer[] playerRenderers;

    // Attack tracking
    private List<GameObject> hitEnemies = new List<GameObject>();

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        characterController = GetComponent<CharacterController>();
        playerCamera = Camera.main;
        if (playerCamera == null)
            playerCamera = FindObjectOfType<Camera>();

        playerRenderers = GetComponentsInChildren<Renderer>();
        StoreOriginalMaterials();
    }

    void Update()
    {
        if (Input.GetKeyDown(ultimateKey) && canUseUltimate && !isPerformingAttack)
        {
            StartCoroutine(PerformTeleportSlashAttack());
        }
    }

    void StoreOriginalMaterials()
    {
        List<Material> materials = new List<Material>();
        foreach (Renderer renderer in playerRenderers)
        {
            if (renderer != null)
            {
                materials.AddRange(renderer.materials);
            }
        }
        originalMaterials = materials.ToArray();
    }

    IEnumerator PerformTeleportSlashAttack()
    {
        isPerformingAttack = true;
        canUseUltimate = false;
        hitEnemies.Clear();
        originalPosition = transform.position;

        // Disable player movement
        if (playerController != null)
            playerController.enabled = false;

        // Find all enemies in range
        FindEnemiesInRange();

        if (enemiesInRange.Count == 0)
        {
            Debug.Log("No enemies in range for ultimate attack!");
            EndAttack();
            yield break;
        }

        Debug.Log($"Starting Teleport Slash Attack on {enemiesInRange.Count} enemies!");

        // Perform slash sequence
        for (int i = 0; i < numberOfSlashes && i < enemiesInRange.Count; i++)
        {
            if (enemiesInRange[i] != null)
            {
                yield return StartCoroutine(PerformSingleSlash(enemiesInRange[i], i == numberOfSlashes - 1 || i == enemiesInRange.Count - 1));

                if (i < numberOfSlashes - 1 && i < enemiesInRange.Count - 1)
                {
                    yield return new WaitForSeconds(pauseBeforeNextSlash);
                }
            }
        }

        // Final pause and return
        yield return new WaitForSeconds(finalPauseDuration);

        // Teleport back to original position with effect
        yield return StartCoroutine(TeleportToPosition(originalPosition, true));

        EndAttack();
    }

    void FindEnemiesInRange()
    {
        enemiesInRange.Clear();
        Collider[] colliders = Physics.OverlapSphere(transform.position, attackRange, enemyLayerMask);

        foreach (Collider col in colliders)
        {
            if (col.gameObject != gameObject && col.CompareTag("Enemy"))
            {
                enemiesInRange.Add(col.gameObject);
            }
        }

        // Sort enemies by distance for optimal attack sequence
        enemiesInRange.Sort((a, b) =>
        {
            float distA = Vector3.Distance(transform.position, a.transform.position);
            float distB = Vector3.Distance(transform.position, b.transform.position);
            return distA.CompareTo(distB);
        });
    }

    IEnumerator PerformSingleSlash(GameObject target, bool isFinalSlash)
    {
        if (target == null) yield break;

        Vector3 targetPosition = target.transform.position + Vector3.up * 0.5f;
        Vector3 attackPosition = targetPosition + (transform.position - targetPosition).normalized * 2f;

        // Teleport to attack position
        yield return StartCoroutine(TeleportToPosition(attackPosition, false));

        // Face the target
        Vector3 lookDirection = (target.transform.position - transform.position).normalized;
        lookDirection.y = 0;
        if (lookDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        // Create slash effect
        CreateSlashEffect(target.transform.position);

        // Camera shake
        if (enableCameraShake && playerCamera != null)
        {
            StartCoroutine(CameraShake());
        }

        // Deal damage
        float currentDamage = isFinalSlash ? finalSlashDamage : damage;
        DealDamageToTarget(target, currentDamage);

        // Brief invisibility flash during slash
        StartCoroutine(InvisibilityFlash());

        yield return new WaitForSeconds(slashInterval);
    }

    IEnumerator TeleportToPosition(Vector3 targetPosition, bool isReturning)
    {
        Vector3 startPosition = transform.position;
        float timer = 0f;

        // Create teleport effect at start position
        CreateTeleportEffect(startPosition);

        // Make player invisible during teleport
        SetPlayerVisibility(false);

        while (timer < teleportDuration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / teleportDuration;
            float curveValue = teleportCurve.Evaluate(normalizedTime);

            Vector3 currentPosition = Vector3.Lerp(startPosition, targetPosition, curveValue);

            // Ensure we don't go through the ground
            if (!isReturning)
            {
                currentPosition.y = Mathf.Max(currentPosition.y, targetPosition.y);
            }

            characterController.enabled = false;
            transform.position = currentPosition;
            characterController.enabled = true;

            yield return null;
        }

        // Ensure final position
        characterController.enabled = false;
        transform.position = targetPosition;
        characterController.enabled = true;

        // Create teleport effect at end position
        CreateTeleportEffect(targetPosition);

        // Make player visible again
        SetPlayerVisibility(true);
    }

    IEnumerator InvisibilityFlash()
    {
        SetPlayerVisibility(false);
        yield return new WaitForSeconds(invisibilityFlashDuration);
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
            GameObject effect = Instantiate(slashEffectPrefab, position, Quaternion.identity);
            Destroy(effect, slashEffectDuration);
        }
        else
        {
            // Create a simple visual effect if no prefab is assigned
            CreateSimpleSlashEffect(position);
        }
    }

    void CreateSimpleSlashEffect(Vector3 position)
    {
        // Create a simple slash line effect using LineRenderer
        GameObject slashLine = new GameObject("SlashEffect");
        slashLine.transform.position = position;

        LineRenderer lr = slashLine.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.cyan;
        lr.endColor = Color.cyan;
        lr.startWidth = 0.1f;
        lr.endWidth = 0.1f;
        lr.positionCount = 2;

        Vector3 start = position + Vector3.up * 1f + Vector3.left * 1f;
        Vector3 end = position + Vector3.down * 1f + Vector3.right * 1f;

        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        StartCoroutine(AnimateSlashEffect(slashLine, lr));
    }

    IEnumerator AnimateSlashEffect(GameObject slashObject, LineRenderer lr)
    {
        float timer = 0f;
        Color originalColor = lr.startColor;

        while (timer < slashEffectDuration)
        {
            timer += Time.deltaTime;
            float alpha = 1f - (timer / slashEffectDuration);
            Color newColor = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            lr.startColor = newColor;
            lr.endColor = newColor;
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
            // Create simple particle-like effect
            CreateSimpleTeleportEffect(position);
        }
    }

    void CreateSimpleTeleportEffect(Vector3 position)
    {
        // Create multiple small spheres that expand and fade
        for (int i = 0; i < 8; i++)
        {
            GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            particle.transform.position = position + Random.insideUnitSphere * 0.5f;
            particle.transform.localScale = Vector3.one * 0.1f;

            Renderer renderer = particle.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.material.color = Color.blue;

            // Remove collider
            Destroy(particle.GetComponent<Collider>());

            StartCoroutine(AnimateTeleportParticle(particle));
        }
    }

    IEnumerator AnimateTeleportParticle(GameObject particle)
    {
        Vector3 startScale = particle.transform.localScale;
        Vector3 targetScale = startScale * 3f;
        Vector3 velocity = Random.insideUnitSphere * 2f;
        float timer = 0f;
        float duration = 0.5f;

        Renderer renderer = particle.GetComponent<Renderer>();
        Color originalColor = renderer.material.color;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / duration;

            // Scale up
            particle.transform.localScale = Vector3.Lerp(startScale, targetScale, normalizedTime);

            // Move outward
            particle.transform.position += velocity * Time.deltaTime;

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
            randomOffset.z = 0; // Don't shake forward/backward

            playerCamera.transform.localPosition = originalPosition + randomOffset;

            yield return null;
        }

        playerCamera.transform.localPosition = originalPosition;
    }

    void DealDamageToTarget(GameObject target, float damageAmount)
    {
        // Prevent hitting the same enemy multiple times
        if (hitEnemies.Contains(target))
            return;

        hitEnemies.Add(target);

        // Try to find EnemyHP component (you'll add this later)
        // EnemyHP enemyHP = target.GetComponent<EnemyHP>();
        // if (enemyHP != null)
        // {
        //     enemyHP.TakeDamage(damageAmount);
        // }

        // For now, just log the damage
        Debug.Log($"Dealt {damageAmount} damage to {target.name}!");

        // You can also try other common health component names
        MonoBehaviour[] components = target.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour component in components)
        {
            // Look for common health/damage methods
            var takeDamageMethod = component.GetType().GetMethod("TakeDamage");
            if (takeDamageMethod != null)
            {
                takeDamageMethod.Invoke(component, new object[] { damageAmount });
                Debug.Log($"Called TakeDamage on {component.GetType().Name}");
                break;
            }

            var damageMethod = component.GetType().GetMethod("Damage");
            if (damageMethod != null)
            {
                damageMethod.Invoke(component, new object[] { damageAmount });
                Debug.Log($"Called Damage on {component.GetType().Name}");
                break;
            }
        }
    }

    void EndAttack()
    {
        isPerformingAttack = false;

        // Re-enable player movement
        if (playerController != null)
            playerController.enabled = true;

        // Start cooldown
        StartCoroutine(UltimateCooldown());

        Debug.Log("Teleport Slash Attack completed!");
    }

    IEnumerator UltimateCooldown()
    {
        yield return new WaitForSeconds(cooldownDuration);
        canUseUltimate = true;
        Debug.Log("Ultimate attack ready!");
    }

    // Visual debugging
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (Application.isPlaying && enemiesInRange.Count > 0)
        {
            Gizmos.color = Color.yellow;
            foreach (GameObject enemy in enemiesInRange)
            {
                if (enemy != null)
                {
                    Gizmos.DrawLine(transform.position, enemy.transform.position);
                }
            }
        }
    }

    // Public methods for external access
    public bool IsAttackOnCooldown()
    {
        return !canUseUltimate;
    }

    public float GetCooldownTimeRemaining()
    {
        // You could implement a timer system here if needed
        return canUseUltimate ? 0f : cooldownDuration;
    }

    public bool IsPerformingAttack()
    {
        return isPerformingAttack;
    }
}