using UnityEngine;
using System.Collections;

public class SwordBattleMode : MonoBehaviour
{
    [Header("Battle Mode Settings")]
    public float dissolveSpeed = 2f; // How fast the sword appears/disappears
    public string dissolvePropertyName = "_DissolveAmount"; // Shader property name
    public AnimationCurve dissolveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // Smooth transition curve

    [Header("Battle Mode States")]
    public bool startInBattleMode = false; // Whether sword starts visible
    public bool disableCombatWhenHidden = true; // Disable attacks when sword is hidden

    [Header("Audio (Optional)")]
    public AudioClip swordAppearSound;
    public AudioClip swordDisappearSound;
    public AudioSource audioSource;

    // Component references
    private MeleeSwordSystem swordSystem;
    private Renderer swordRenderer;
    private Material swordMaterial;

    // State tracking
    private bool isInBattleMode = false;
    private bool isTransitioning = false;
    private Coroutine dissolveCoroutine;

    // Dissolve values
    private const float SWORD_HIDDEN = 1f; // Fully dissolved (invisible)
    private const float SWORD_VISIBLE = 0f; // Not dissolved (visible)

    public bool IsInBattleMode => isInBattleMode;
    public bool IsTransitioning => isTransitioning;

    void Start()
    {
        InitializeComponents();
        SetInitialState();
    }

    void Update()
    {
        HandleInput();
    }

    void InitializeComponents()
    {
        // Get the sword system
        swordSystem = GetComponent<MeleeSwordSystem>();
        if (swordSystem == null)
        {
            Debug.LogError("[SwordBattleMode] MeleeSwordSystem not found on this GameObject!");
            return;
        }

        // Get the sword renderer and material
        if (swordSystem.SwordModel != null)
        {
            swordRenderer = swordSystem.SwordModel.GetComponent<Renderer>();
            if (swordRenderer != null)
            {
                // Create a material instance so we don't modify the original
                swordMaterial = swordRenderer.material;
            }
            else
            {
                Debug.LogError("[SwordBattleMode] Renderer not found on sword model!");
            }
        }
        else
        {
            Debug.LogError("[SwordBattleMode] Sword model not assigned in MeleeSwordSystem!");
        }

        // Setup audio source if not assigned
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
    }

    void SetInitialState()
    {
        isInBattleMode = startInBattleMode;

        if (swordMaterial != null)
        {
            // Set initial dissolve value without animation
            float initialValue = isInBattleMode ? SWORD_VISIBLE : SWORD_HIDDEN;
            swordMaterial.SetFloat(dissolvePropertyName, initialValue);
        }

        Debug.Log($"[SwordBattleMode] Initial state: {(isInBattleMode ? "Battle Mode" : "Hidden")}");
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.X))
        {
            ToggleBattleMode();
        }
    }

    public void ToggleBattleMode()
    {
        if (isTransitioning) return; // Prevent spamming

        isInBattleMode = !isInBattleMode;

        Debug.Log($"[SwordBattleMode] Toggling to: {(isInBattleMode ? "Battle Mode" : "Hidden")}");

        // Stop current transition if any
        if (dissolveCoroutine != null)
        {
            StopCoroutine(dissolveCoroutine);
        }

        // Start new transition
        dissolveCoroutine = StartCoroutine(TransitionDissolve());

        // Play appropriate sound
        PlayTransitionSound();

        // Reset combo if hiding sword
        if (!isInBattleMode && swordSystem != null)
        {
            // Access the combo system through reflection or make it public
            // For now, we'll try to end any current attack
            if (swordSystem.IsAttacking)
            {
                swordSystem.EndAttack();
            }
        }
    }

    IEnumerator TransitionDissolve()
    {
        if (swordMaterial == null) yield break;

        isTransitioning = true;

        // Get current and target dissolve values
        float currentValue = swordMaterial.GetFloat(dissolvePropertyName);
        float targetValue = isInBattleMode ? SWORD_VISIBLE : SWORD_HIDDEN;

        float startValue = currentValue;
        float elapsed = 0f;
        float duration = 1f / dissolveSpeed;

        // Animate the dissolve
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / duration;

            // Apply easing curve
            float curveValue = dissolveCurve.Evaluate(normalizedTime);

            // Interpolate dissolve value
            float dissolveValue = Mathf.Lerp(startValue, targetValue, curveValue);
            swordMaterial.SetFloat(dissolvePropertyName, dissolveValue);

            yield return null;
        }

        // Ensure we end at exactly the target value
        swordMaterial.SetFloat(dissolvePropertyName, targetValue);

        isTransitioning = false;

        Debug.Log($"[SwordBattleMode] Transition complete: {(isInBattleMode ? "Sword Visible" : "Sword Hidden")}");
    }

    void PlayTransitionSound()
    {
        if (audioSource == null) return;

        AudioClip soundToPlay = isInBattleMode ? swordAppearSound : swordDisappearSound;

        if (soundToPlay != null)
        {
            audioSource.PlayOneShot(soundToPlay);
        }
    }

    // Method for other systems to check if combat is allowed
    public bool CanPerformCombat()
    {
        if (disableCombatWhenHidden && !isInBattleMode)
        {
            return false;
        }

        return !isTransitioning;
    }

    // Public methods for external control
    public void SetBattleMode(bool enabled, bool immediate = false)
    {
        if (isInBattleMode == enabled) return;

        isInBattleMode = enabled;

        if (immediate)
        {
            // Instant change without animation
            if (swordMaterial != null)
            {
                float value = isInBattleMode ? SWORD_VISIBLE : SWORD_HIDDEN;
                swordMaterial.SetFloat(dissolvePropertyName, value);
            }
        }
        else
        {
            // Smooth transition
            if (dissolveCoroutine != null)
            {
                StopCoroutine(dissolveCoroutine);
            }
            dissolveCoroutine = StartCoroutine(TransitionDissolve());
        }

        PlayTransitionSound();
    }

    public void ShowSword(bool immediate = false)
    {
        SetBattleMode(true, immediate);
    }

    public void HideSword(bool immediate = false)
    {
        SetBattleMode(false, immediate);
    }

    // Get current dissolve value for debugging
    public float GetCurrentDissolveValue()
    {
        if (swordMaterial != null)
        {
            return swordMaterial.GetFloat(dissolvePropertyName);
        }
        return 0f;
    }

    void OnDestroy()
    {
        // Clean up material instance if we created one
        if (swordMaterial != null && Application.isPlaying)
        {
            Destroy(swordMaterial);
        }
    }

    // Debug GUI for testing
    void OnGUI()
    {
        if (!Debug.isDebugBuild) return;

        GUILayout.BeginArea(new Rect(10, 10, 200, 100));
        GUILayout.Label($"Battle Mode: {isInBattleMode}");
        GUILayout.Label($"Transitioning: {isTransitioning}");
        GUILayout.Label($"Dissolve: {GetCurrentDissolveValue():F2}");

        if (GUILayout.Button("Toggle (X)"))
        {
            ToggleBattleMode();
        }
        GUILayout.EndArea();
    }
}