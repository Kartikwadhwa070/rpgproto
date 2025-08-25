using UnityEngine;
using System.Collections;

public class SwordComboSystem : MonoBehaviour
{
    private MeleeSwordSystem swordSystem;
    private float comboTimeWindow;

    // Combo state
    private int currentComboIndex = 0;
    private float lastAttackTime;
    private bool isWaitingForNextAttack = false;
    private Coroutine comboResetCoroutine;

    // Attack data for each combo attack
    private AttackData[] comboAttacks;

    public void Initialize(MeleeSwordSystem system, float timeWindow)
    {
        Debug.Log("[SwordComboSystem] Initializing...");
        swordSystem = system;
        comboTimeWindow = timeWindow;

        SetupComboAttacks();
        Debug.Log($"[SwordComboSystem] Initialized with {comboAttacks.Length} combo attacks and time window: {comboTimeWindow}");
    }

    void SetupComboAttacks()
    {
        var baseData = swordSystem.GetBaseAttackData();

        comboAttacks = new AttackData[4];

        // Attack 1: Unsheathe slash - Fast and moderate damage
        comboAttacks[0] = new AttackData
        {
            damage = baseData.damage,
            knockbackForce = baseData.knockbackForce,
            animationDuration = 0.5f,
            damageDelay = 0.25f
        };

        // Attack 2: Right to Left - Slightly stronger
        comboAttacks[1] = new AttackData
        {
            damage = baseData.damage * 1.2f,
            knockbackForce = baseData.knockbackForce * 1.3f,
            animationDuration = 0.6f,
            damageDelay = 0.3f
        };

        // Attack 3: Left to Right - Even stronger
        comboAttacks[2] = new AttackData
        {
            damage = baseData.damage * 1.5f,
            knockbackForce = baseData.knockbackForce * 1.5f,
            animationDuration = 0.7f,
            damageDelay = 0.35f
        };

        // Attack 4: Launcher - Most powerful, special launch effect
        comboAttacks[3] = new AttackData
        {
            damage = baseData.damage * 2f,
            knockbackForce = 0f, // No horizontal knockback, only launch
            animationDuration = 0.8f,
            damageDelay = 0.4f
        };
    }

    public int GetNextComboAttack()
    {
        Debug.Log($"[SwordComboSystem] GetNextComboAttack - Current combo index: {currentComboIndex}");
        Debug.Log($"[SwordComboSystem] Time since last attack: {Time.time - lastAttackTime}, Window: {comboTimeWindow}");

        // If too much time has passed since last attack, reset combo
        if (Time.time - lastAttackTime > comboTimeWindow && currentComboIndex > 0)
        {
            Debug.Log("[SwordComboSystem] Combo timed out, resetting");
            ResetCombo();
        }

        int attackIndex = currentComboIndex;
        string attackName = GetComboAttackName(attackIndex);

        // Advance combo
        currentComboIndex = (currentComboIndex + 1) % comboAttacks.Length;
        lastAttackTime = Time.time;

        Debug.Log($"[SwordComboSystem] Performing attack: {attackName} (index {attackIndex})");
        Debug.Log($"[SwordComboSystem] Next combo index will be: {currentComboIndex}");

        // Set up combo reset timer
        StartComboResetTimer();

        // If this was the last attack in combo, reset after this attack
        if (currentComboIndex == 0) // We wrapped around
        {
            isWaitingForNextAttack = false;
            Debug.Log("[SwordComboSystem] This was the final attack in combo");
        }
        else
        {
            isWaitingForNextAttack = true;
            Debug.Log("[SwordComboSystem] Waiting for next attack in combo");
        }

        return attackIndex;
    }

    void StartComboResetTimer()
    {
        if (comboResetCoroutine != null)
        {
            StopCoroutine(comboResetCoroutine);
        }

        comboResetCoroutine = StartCoroutine(ComboResetTimer());
    }

    IEnumerator ComboResetTimer()
    {
        yield return new WaitForSeconds(comboTimeWindow);

        // If no new attack was performed, reset combo
        if (Time.time - lastAttackTime >= comboTimeWindow)
        {
            ResetCombo();
        }
    }

    public void ResetCombo()
    {
        currentComboIndex = 0;
        isWaitingForNextAttack = false;

        if (comboResetCoroutine != null)
        {
            StopCoroutine(comboResetCoroutine);
            comboResetCoroutine = null;
        }

        swordSystem.EndAttack();
        Debug.Log("Combo reset");
    }

    public bool CanContinueCombo()
    {
        return isWaitingForNextAttack && (Time.time - lastAttackTime < comboTimeWindow);
    }

    public bool IsWaitingForNextAttack()
    {
        return isWaitingForNextAttack;
    }

    public AttackData GetAttackData(int comboIndex)
    {
        if (comboIndex >= 0 && comboIndex < comboAttacks.Length)
        {
            return comboAttacks[comboIndex];
        }

        return swordSystem.GetBaseAttackData();
    }

    public int GetCurrentComboIndex()
    {
        return currentComboIndex;
    }

    public string GetComboAttackName(int index)
    {
        switch (index)
        {
            case 0: return "Unsheathe Slash";
            case 1: return "Horizontal Right";
            case 2: return "Horizontal Left";
            case 3: return "Rising Launcher";
            default: return "Unknown Attack";
        }
    }
}