using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class EnemyHP : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public bool showHealthBar = true;
    public bool destroyOnDeath = true;
    public float deathDelay = 2f;

    [Header("Damage Effects")]
    public bool flashOnDamage = true;
    public Color damageFlashColor = Color.red;
    public float flashDuration = 0.1f;
    public bool shakeOnDamage = true;
    public float shakeIntensity = 0.1f;
    public float shakeDuration = 0.2f;

    [Header("Health Bar UI")]
    public Canvas healthBarCanvas;
    public Image healthBarFill;
    public Image healthBarBackground;
    public Vector3 healthBarOffset = new Vector3(0, 2.5f, 0);

    // Private variables
    private float currentHealth;
    private bool isDead = false;
    private BasicEnemyAI enemyAI;
    private Renderer enemyRenderer;
    private Material originalMaterial;
    private Material flashMaterial;
    private Camera playerCamera;

    // Health bar
    private GameObject healthBarObject;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        currentHealth = maxHealth;
        enemyAI = GetComponent<BasicEnemyAI>();
        enemyRenderer = GetComponentInChildren<Renderer>();
        playerCamera = Camera.main;

        if (enemyRenderer != null)
        {
            originalMaterial = enemyRenderer.material;
            CreateFlashMaterial();
        }

        if (showHealthBar)
        {
            CreateHealthBar();
        }

        UpdateHealthBar();
    }

    void CreateFlashMaterial()
    {
        if (originalMaterial != null)
        {
            flashMaterial = new Material(originalMaterial);
            flashMaterial.color = damageFlashColor;
            flashMaterial.SetFloat("_Metallic", 0f);
            flashMaterial.SetFloat("_Smoothness", 0f);
        }
    }

    void CreateHealthBar()
    {
        // Create health bar GameObject
        healthBarObject = new GameObject($"{gameObject.name}_HealthBar");
        healthBarObject.transform.SetParent(transform);
        healthBarObject.transform.localPosition = healthBarOffset;

        // Add Canvas component
        healthBarCanvas = healthBarObject.AddComponent<Canvas>();
        healthBarCanvas.renderMode = RenderMode.WorldSpace;
        healthBarCanvas.worldCamera = playerCamera;

        // Scale down the canvas
        healthBarObject.transform.localScale = Vector3.one * 0.01f;

        // Create background
        GameObject backgroundObj = new GameObject("Background");
        backgroundObj.transform.SetParent(healthBarObject.transform);
        backgroundObj.transform.localPosition = Vector3.zero;
        backgroundObj.transform.localScale = Vector3.one;

        healthBarBackground = backgroundObj.AddComponent<Image>();
        healthBarBackground.color = Color.black;

        RectTransform bgRect = backgroundObj.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(100f, 10f);

        // Create fill
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(backgroundObj.transform);
        fillObj.transform.localPosition = Vector3.zero;
        fillObj.transform.localScale = Vector3.one;

        healthBarFill = fillObj.AddComponent<Image>();
        healthBarFill.color = Color.green;
        healthBarFill.type = Image.Type.Filled;
        healthBarFill.fillMethod = Image.FillMethod.Horizontal;

        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.sizeDelta = new Vector2(100f, 10f);
        fillRect.anchoredPosition = Vector3.zero;
    }

    void Update()
    {
        if (showHealthBar && healthBarCanvas != null && playerCamera != null)
        {
            // Make health bar face the camera
            Vector3 directionToCamera = playerCamera.transform.position - healthBarCanvas.transform.position;
            healthBarCanvas.transform.rotation = Quaternion.LookRotation(directionToCamera);

            // Update health bar color based on health percentage
            UpdateHealthBarColor();
        }
    }

    void UpdateHealthBarColor()
    {
        if (healthBarFill == null) return;

        float healthPercentage = currentHealth / maxHealth;

        if (healthPercentage > 0.6f)
            healthBarFill.color = Color.green;
        else if (healthPercentage > 0.3f)
            healthBarFill.color = Color.yellow;
        else
            healthBarFill.color = Color.red;
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        UpdateHealthBar();

        // Trigger damage effects
        if (flashOnDamage)
        {
            StartCoroutine(DamageFlash());
        }

        if (shakeOnDamage)
        {
            StartCoroutine(DamageShake());
        }

        // Check for death
        if (currentHealth <= 0f)
        {
            Die();
        }

        Debug.Log($"{gameObject.name} took {damage} damage. Health: {currentHealth}/{maxHealth}");
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        UpdateHealthBar();

        Debug.Log($"{gameObject.name} healed for {amount}. Health: {currentHealth}/{maxHealth}");
    }

    void UpdateHealthBar()
    {
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = currentHealth / maxHealth;
        }
    }

    IEnumerator DamageFlash()
    {
        if (enemyRenderer != null && flashMaterial != null)
        {
            enemyRenderer.material = flashMaterial;
            yield return new WaitForSeconds(flashDuration);
            enemyRenderer.material = originalMaterial;
        }
    }

    IEnumerator DamageShake()
    {
        Vector3 originalPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeIntensity;
            float z = Random.Range(-1f, 1f) * shakeIntensity;

            transform.position = originalPosition + new Vector3(x, 0, z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = originalPosition;
    }

    void Die()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log($"{gameObject.name} has died!");

        // Disable AI
        if (enemyAI != null)
        {
            enemyAI.enabled = false;
        }

        // Disable NavMeshAgent
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }

        // Hide health bar
        if (healthBarObject != null)
        {
            healthBarObject.SetActive(false);
        }

        // Change material to indicate death
        if (enemyRenderer != null && originalMaterial != null)
        {
            Material deathMaterial = new Material(originalMaterial);
            deathMaterial.color = Color.gray;
            enemyRenderer.material = deathMaterial;
        }

        // Handle destruction
        if (destroyOnDeath)
        {
            StartCoroutine(DeathSequence());
        }
    }

    IEnumerator DeathSequence()
    {
        // Wait before destroying
        yield return new WaitForSeconds(deathDelay);

        // Fade out effect (optional)
        if (enemyRenderer != null)
        {
            float fadeTime = 1f;
            float elapsed = 0f;
            Material fadeMaterial = enemyRenderer.material;
            Color originalColor = fadeMaterial.color;

            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
                Color newColor = originalColor;
                newColor.a = alpha;
                fadeMaterial.color = newColor;

                yield return null;
            }
        }

        Destroy(gameObject);
    }

    // Public getters
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => currentHealth / maxHealth;
    public bool IsDead() => isDead;

    // Method to set health (useful for spawning enemies with different health)
    public void SetHealth(float health)
    {
        maxHealth = health;
        currentHealth = health;
        UpdateHealthBar();
    }

    void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        flashDuration = Mathf.Clamp(flashDuration, 0.05f, 1f);
        shakeDuration = Mathf.Clamp(shakeDuration, 0.1f, 2f);
        shakeIntensity = Mathf.Clamp(shakeIntensity, 0.01f, 1f);
        deathDelay = Mathf.Clamp(deathDelay, 0f, 10f);
    }
}