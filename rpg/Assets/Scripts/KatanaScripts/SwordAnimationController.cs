using UnityEngine;
using System.Collections;

public class SwordAnimationController : MonoBehaviour
{
    private MeleeSwordSystem swordSystem;
    private Transform swordModel;

    [Header("Animation Settings")]
    public AnimationCurve swingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve anticipationCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.3f, -0.5f), new Keyframe(1, 1));
    public float swingIntensity = 90f;
    public float anticipationStrength = 0.8f;

    // Original sword transform values
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private bool isAnimating = false;

    // Katana-specific settings
    [Header("Katana Motion Settings")]
    public float arcRadius = 1.2f;
    public float followThroughMultiplier = 1.3f;
    public Vector3 pivotPoint = new Vector3(0, -0.5f, 0); // Hand/grip position relative to sword

    public void Initialize(MeleeSwordSystem system)
    {
        swordSystem = system;
        swordModel = system.SwordModel;

        if (swordModel != null)
        {
            originalPosition = swordModel.localPosition;
            originalRotation = swordModel.localRotation;
        }
    }

    public void PlayAttackAnimation(int comboIndex, float duration)
    {
        if (swordModel == null) return;

        StartCoroutine(AnimateKatanaAttack(comboIndex, duration));
    }

    IEnumerator AnimateKatanaAttack(int comboIndex, float duration)
    {
        isAnimating = true;
        float elapsed = 0f;

        // Split animation into phases: anticipation (20%), strike (60%), follow-through (20%)
        float anticipationDuration = duration * 0.2f;
        float strikeDuration = duration * 0.6f;
        float followThroughDuration = duration * 0.2f;

        // Anticipation phase
        while (elapsed < anticipationDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / anticipationDuration;
            ApplyAnticipation(comboIndex, normalizedTime);
            yield return null;
        }

        // Strike phase
        float strikeStartTime = elapsed;
        while (elapsed < strikeStartTime + strikeDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = (elapsed - strikeStartTime) / strikeDuration;
            float curveValue = swingCurve.Evaluate(normalizedTime);
            ApplyStrikeAnimation(comboIndex, curveValue, normalizedTime);
            yield return null;
        }

        // Follow-through phase
        float followStartTime = elapsed;
        while (elapsed < strikeStartTime + strikeDuration + followThroughDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = (elapsed - followStartTime) / followThroughDuration;
            ApplyFollowThrough(comboIndex, normalizedTime);
            yield return null;
        }

        // Return to original position
        StartCoroutine(ReturnToOriginalPosition());
        isAnimating = false;
    }

    void ApplyAnticipation(int comboIndex, float normalizedTime)
    {
        Vector3 position = originalPosition;
        Quaternion rotation = originalRotation;
        float anticipationValue = anticipationCurve.Evaluate(normalizedTime) * anticipationStrength;

        switch (comboIndex)
        {
            case 0: // Kesagiri - Diagonal cut (shoulder to opposite hip)
                // Pull back and up for the diagonal strike
                rotation = originalRotation * Quaternion.Euler(
                    30f * anticipationValue,    // Raise blade
                    -45f * anticipationValue,   // Angle for diagonal
                    -20f * anticipationValue    // Slight twist
                );
                position += Vector3.up * (0.2f * anticipationValue);
                position += Vector3.back * (0.1f * anticipationValue);
                break;

            case 1: // Yokogiri - Horizontal cut (right to left)
                // Pull blade to the right side
                rotation = originalRotation * Quaternion.Euler(
                    10f * anticipationValue,    // Slight lift
                    -90f * anticipationValue,   // Turn to side
                    -30f * anticipationValue    // Cock the wrist
                );
                position += Vector3.right * (0.3f * anticipationValue);
                break;

            case 2: // Gyakuyo - Reverse horizontal (left to right)
                // Pull blade to the left side
                rotation = originalRotation * Quaternion.Euler(
                    10f * anticipationValue,    // Slight lift
                    90f * anticipationValue,    // Turn to other side
                    30f * anticipationValue     // Reverse wrist cock
                );
                position += Vector3.left * (0.3f * anticipationValue);
                break;

            case 3: // Kiriage - Rising cut (upward slash)
                // Lower blade for upward strike
                rotation = originalRotation * Quaternion.Euler(
                    -45f * anticipationValue,   // Lower the blade
                    15f * anticipationValue,    // Slight angle
                    -10f * anticipationValue    // Wrist position
                );
                position += Vector3.down * (0.2f * anticipationValue);
                position += Vector3.back * (0.15f * anticipationValue);
                break;
        }

        swordModel.localPosition = position;
        swordModel.localRotation = rotation;
    }

    void ApplyStrikeAnimation(int comboIndex, float curveValue, float normalizedTime)
    {
        Vector3 position = originalPosition;
        Quaternion rotation = originalRotation;

        // Create natural arc motion using sine waves for smooth katana cuts
        float arcProgress = Mathf.Sin(normalizedTime * Mathf.PI); // Creates natural arc motion

        switch (comboIndex)
        {
            case 0: // Kesagiri - Diagonal cut
                // Smooth diagonal arc from high right to low left
                float diagonalAngle = Mathf.Lerp(-45f, 135f, curveValue);
                rotation = originalRotation * Quaternion.Euler(
                    Mathf.Lerp(30f, -60f, curveValue),      // High to low
                    Mathf.Lerp(-45f, 45f, curveValue),      // Right to left
                    Mathf.Lerp(-20f, 30f, curveValue)       // Natural wrist rotation
                );

                // Arc motion through space
                position += new Vector3(
                    Mathf.Lerp(0.2f, -0.4f, curveValue),   // Right to left
                    Mathf.Lerp(0.3f, -0.4f, curveValue),   // High to low
                    arcProgress * 0.5f                      // Forward thrust
                );
                break;

            case 1: // Yokogiri - Horizontal cut (right to left)
                rotation = originalRotation * Quaternion.Euler(
                    5f + Mathf.Sin(normalizedTime * Mathf.PI) * 15f,    // Slight up-down motion
                    Mathf.Lerp(-90f, 90f, curveValue),                  // Full horizontal sweep
                    Mathf.Lerp(-30f, 45f, curveValue)                   // Wrist follow-through
                );

                // Horizontal arc motion
                position += new Vector3(
                    Mathf.Lerp(0.4f, -0.4f, curveValue),               // Right to left sweep
                    Mathf.Sin(normalizedTime * Mathf.PI) * 0.1f,       // Slight vertical arc
                    arcProgress * 0.3f                                  // Forward motion
                );
                break;

            case 2: // Gyakuyo - Reverse horizontal (left to right)
                rotation = originalRotation * Quaternion.Euler(
                    5f + Mathf.Sin(normalizedTime * Mathf.PI) * 15f,    // Slight up-down motion
                    Mathf.Lerp(90f, -90f, curveValue),                  // Full horizontal sweep (reversed)
                    Mathf.Lerp(30f, -45f, curveValue)                   // Reverse wrist follow-through
                );

                // Reverse horizontal arc motion
                position += new Vector3(
                    Mathf.Lerp(-0.4f, 0.4f, curveValue),               // Left to right sweep
                    Mathf.Sin(normalizedTime * Mathf.PI) * 0.1f,       // Slight vertical arc
                    arcProgress * 0.3f                                  // Forward motion
                );
                break;

            case 3: // Kiriage - Rising cut (launcher)
                rotation = originalRotation * Quaternion.Euler(
                    Mathf.Lerp(-45f, 80f, curveValue),     // Low to high sweep
                    Mathf.Sin(normalizedTime * Mathf.PI * 2) * 10f,    // Slight side motion
                    Mathf.Lerp(-10f, 25f, curveValue)      // Upward wrist motion
                );

                // Rising arc motion
                position += new Vector3(
                    Mathf.Sin(normalizedTime * Mathf.PI) * 0.2f,       // Side arc motion
                    Mathf.Lerp(-0.3f, 0.7f, curveValue),               // Low to high
                    arcProgress * 0.6f                                  // Strong forward thrust
                );
                break;
        }

        // Add subtle blade trail effect during peak motion
        if (normalizedTime > 0.3f && normalizedTime < 0.7f)
        {
            float trailIntensity = Mathf.Sin((normalizedTime - 0.3f) / 0.4f * Mathf.PI);
            Vector3 trailOffset = new Vector3(
                Mathf.Sin(Time.time * 30f) * 0.01f * trailIntensity,
                Mathf.Cos(Time.time * 35f) * 0.01f * trailIntensity,
                0f
            );
            position += trailOffset;
        }

        swordModel.localPosition = position;
        swordModel.localRotation = rotation;
    }

    void ApplyFollowThrough(int comboIndex, float normalizedTime)
    {
        Vector3 position = originalPosition;
        Quaternion rotation = originalRotation;

        // Smooth deceleration curve
        float followValue = 1f - (normalizedTime * normalizedTime); // Ease out

        switch (comboIndex)
        {
            case 0: // Kesagiri follow-through
                rotation = originalRotation * Quaternion.Euler(
                    -60f * followValue * followThroughMultiplier,
                    45f * followValue * followThroughMultiplier,
                    30f * followValue * followThroughMultiplier
                );
                position += new Vector3(-0.4f, -0.4f, 0.3f) * followValue;
                break;

            case 1: // Yokogiri follow-through
                rotation = originalRotation * Quaternion.Euler(
                    0f,
                    90f * followValue * followThroughMultiplier,
                    45f * followValue * followThroughMultiplier
                );
                position += new Vector3(-0.4f, 0f, 0.2f) * followValue;
                break;

            case 2: // Gyakuyo follow-through
                rotation = originalRotation * Quaternion.Euler(
                    0f,
                    -90f * followValue * followThroughMultiplier,
                    -45f * followValue * followThroughMultiplier
                );
                position += new Vector3(0.4f, 0f, 0.2f) * followValue;
                break;

            case 3: // Kiriage follow-through
                rotation = originalRotation * Quaternion.Euler(
                    80f * followValue * followThroughMultiplier,
                    0f,
                    25f * followValue * followThroughMultiplier
                );
                position += new Vector3(0f, 0.7f, 0.4f) * followValue;
                break;
        }

        swordModel.localPosition = position;
        swordModel.localRotation = rotation;
    }

    IEnumerator ReturnToOriginalPosition()
    {
        float returnDuration = 0.4f; // Slightly longer for more natural return
        float elapsed = 0f;

        Vector3 startPos = swordModel.localPosition;
        Quaternion startRot = swordModel.localRotation;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / returnDuration;

            // Use ease-out curve for natural return motion
            float easeOut = 1f - Mathf.Pow(1f - normalizedTime, 3f);

            swordModel.localPosition = Vector3.Lerp(startPos, originalPosition, easeOut);
            swordModel.localRotation = Quaternion.Lerp(startRot, originalRotation, easeOut);

            yield return null;
        }

        // Ensure exact original position
        swordModel.localPosition = originalPosition;
        swordModel.localRotation = originalRotation;
    }

    public void ResetToOriginalPosition()
    {
        if (swordModel != null)
        {
            swordModel.localPosition = originalPosition;
            swordModel.localRotation = originalRotation;
        }
    }

    public bool IsAnimating => isAnimating;
}