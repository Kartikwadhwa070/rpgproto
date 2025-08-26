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

        // Stop any existing animation before starting new one
        StopAllCoroutines();
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
                // Dramatic wind-up - raise sword high and back
                rotation = originalRotation * Quaternion.Euler(
                    60f * anticipationValue,     // Raise blade high overhead
                    -30f * anticipationValue,    // Angle for diagonal trajectory
                    -15f * anticipationValue     // Slight twist preparing for cut
                );
                position += Vector3.up * (0.4f * anticipationValue);
                position += Vector3.back * (0.3f * anticipationValue);
                position += Vector3.right * (0.2f * anticipationValue);
                break;

            case 1: // Yokogiri - Horizontal cut (right to left)
                // Pull far to the right with dramatic shoulder turn
                rotation = originalRotation * Quaternion.Euler(
                    15f * anticipationValue,     // Slight lift for power
                    -120f * anticipationValue,   // Dramatic turn to right
                    -45f * anticipationValue     // Cock wrist for explosive release
                );
                position += Vector3.right * (0.6f * anticipationValue);
                position += Vector3.up * (0.1f * anticipationValue);
                break;

            case 2: // Gyakuyo - Reverse horizontal (left to right)
                // Mirror of attack 1 but to the left
                rotation = originalRotation * Quaternion.Euler(
                    15f * anticipationValue,     // Slight lift for power
                    120f * anticipationValue,    // Dramatic turn to left
                    45f * anticipationValue      // Reverse wrist cock
                );
                position += Vector3.left * (0.6f * anticipationValue);
                position += Vector3.up * (0.1f * anticipationValue);
                break;

            case 3: // Kiriage - Rising cut (upward slash)
                // Crouch low for explosive upward strike
                rotation = originalRotation * Quaternion.Euler(
                    -75f * anticipationValue,    // Lower blade dramatically
                    25f * anticipationValue,     // Slight angle for trajectory
                    -20f * anticipationValue     // Prepare wrist for upward snap
                );
                position += Vector3.down * (0.5f * anticipationValue);
                position += Vector3.back * (0.4f * anticipationValue);
                break;
        }

        swordModel.localPosition = position;
        swordModel.localRotation = rotation;
    }

    void ApplyStrikeAnimation(int comboIndex, float curveValue, float normalizedTime)
    {
        Vector3 position = originalPosition;
        Quaternion rotation = originalRotation;

        // Enhanced arc motion with acceleration/deceleration
        float arcProgress = Mathf.Sin(normalizedTime * Mathf.PI);
        float speedCurve = swingCurve.Evaluate(normalizedTime);

        // Add impact emphasis at peak speed
        float impactBoost = 1f + Mathf.Sin(normalizedTime * Mathf.PI) * 0.3f;

        switch (comboIndex)
        {
            case 0: // Kesagiri - Diagonal cut (MASSIVE diagonal slash)
                rotation = originalRotation * Quaternion.Euler(
                    Mathf.Lerp(60f, -90f, speedCurve),      // High overhead to deep low
                    Mathf.Lerp(-30f, 75f, speedCurve),      // Sweep across body dramatically
                    Mathf.Lerp(-15f, 45f, speedCurve)       // Wrist snap through cut
                );

                // Exaggerated diagonal arc motion
                position += new Vector3(
                    Mathf.Lerp(0.3f, -0.8f, speedCurve) * impactBoost,   // Right to far left
                    Mathf.Lerp(0.5f, -0.7f, speedCurve) * impactBoost,   // High to very low
                    arcProgress * 0.8f * impactBoost                      // Deep forward lunge
                );

                // Add shoulder rotation effect
                position += Vector3.forward * Mathf.Sin(normalizedTime * Mathf.PI * 2) * 0.1f;
                break;

            case 1: // Yokogiri - Horizontal cut (WIDE sweeping arc)
                rotation = originalRotation * Quaternion.Euler(
                    10f + Mathf.Sin(normalizedTime * Mathf.PI) * 25f * impactBoost,  // Dynamic up-down
                    Mathf.Lerp(-120f, 120f, speedCurve),                             // Full 240-degree sweep
                    Mathf.Lerp(-45f, 60f, speedCurve) * impactBoost                 // Aggressive wrist snap
                );

                // Wide horizontal arc with body rotation
                position += new Vector3(
                    Mathf.Lerp(0.7f, -0.7f, speedCurve) * impactBoost,             // Wide right to left
                    Mathf.Sin(normalizedTime * Mathf.PI) * 0.2f * impactBoost,     // Natural arc height
                    arcProgress * 0.5f * impactBoost                                // Forward step into cut
                );

                // Add centrifugal force effect
                float centrifugal = Mathf.Sin(normalizedTime * Mathf.PI * 3) * 0.05f;
                position += new Vector3(centrifugal, 0, centrifugal);
                break;

            case 2: // Gyakuyo - Reverse horizontal (COUNTER-SWEEPING arc)
                rotation = originalRotation * Quaternion.Euler(
                    10f + Mathf.Sin(normalizedTime * Mathf.PI) * 25f * impactBoost,  // Dynamic up-down
                    Mathf.Lerp(120f, -120f, speedCurve),                             // Reverse 240-degree sweep
                    Mathf.Lerp(45f, -60f, speedCurve) * impactBoost                 // Reverse wrist snap
                );

                // Wide reverse horizontal arc
                position += new Vector3(
                    Mathf.Lerp(-0.7f, 0.7f, speedCurve) * impactBoost,             // Wide left to right
                    Mathf.Sin(normalizedTime * Mathf.PI) * 0.2f * impactBoost,     // Natural arc height
                    arcProgress * 0.5f * impactBoost                                // Forward step
                );

                // Reverse centrifugal force
                float reverseCentrifugal = Mathf.Sin(normalizedTime * Mathf.PI * -3) * 0.05f;
                position += new Vector3(reverseCentrifugal, 0, reverseCentrifugal);
                break;

            case 3: // Kiriage - Rising cut (EXPLOSIVE upward launcher)
                rotation = originalRotation * Quaternion.Euler(
                    Mathf.Lerp(-75f, 110f, speedCurve) * impactBoost,              // Explosive low to high
                    Mathf.Sin(normalizedTime * Mathf.PI * 2) * 15f,                // Side-to-side snap
                    Mathf.Lerp(-20f, 40f, speedCurve) * impactBoost                // Powerful wrist uppercut
                );

                // Explosive rising motion with forward lunge
                position += new Vector3(
                    Mathf.Sin(normalizedTime * Mathf.PI) * 0.3f * impactBoost,     // Side arc motion
                    Mathf.Lerp(-0.6f, 1.2f, speedCurve) * impactBoost,             // Deep crouch to high reach
                    arcProgress * 1.0f * impactBoost                                // Powerful forward thrust
                );

                // Add explosive "pop" at peak
                if (normalizedTime > 0.4f && normalizedTime < 0.6f)
                {
                    float explosiveBoost = Mathf.Sin((normalizedTime - 0.4f) / 0.2f * Mathf.PI) * 0.2f;
                    position += Vector3.up * explosiveBoost;
                }
                break;
        }

        // Enhanced blade trail effect with speed-based intensity
        if (normalizedTime > 0.2f && normalizedTime < 0.8f)
        {
            float trailIntensity = Mathf.Sin((normalizedTime - 0.2f) / 0.6f * Mathf.PI) * speedCurve;
            Vector3 trailOffset = new Vector3(
                Mathf.Sin(Time.time * 40f) * 0.02f * trailIntensity,
                Mathf.Cos(Time.time * 45f) * 0.02f * trailIntensity,
                Mathf.Sin(Time.time * 35f) * 0.01f * trailIntensity
            );
            position += trailOffset;
        }

        // Add impact vibration at peak speed
        if (normalizedTime > 0.45f && normalizedTime < 0.55f)
        {
            float vibration = Mathf.Sin(Time.time * 60f) * 0.03f * speedCurve;
            position += new Vector3(vibration, vibration * 0.5f, 0);
        }

        swordModel.localPosition = position;
        swordModel.localRotation = rotation;
    }

    void ApplyFollowThrough(int comboIndex, float normalizedTime)
    {
        Vector3 position = originalPosition;
        Quaternion rotation = originalRotation;

        // Enhanced deceleration curve with natural settling
        float followValue = 1f - (normalizedTime * normalizedTime * normalizedTime); // Smooth ease out
        float settleBoost = followThroughMultiplier * (1f + Mathf.Sin(normalizedTime * Mathf.PI) * 0.2f);

        switch (comboIndex)
        {
            case 0: // Kesagiri follow-through - Dramatic settling after diagonal cut
                rotation = originalRotation * Quaternion.Euler(
                    -90f * followValue * settleBoost,      // Deep follow through down
                    75f * followValue * settleBoost,       // Complete the diagonal sweep
                    45f * followValue * settleBoost        // Wrist completes the snap
                );
                position += new Vector3(-0.8f, -0.7f, 0.6f) * followValue * settleBoost;

                // Add settling vibration
                if (normalizedTime < 0.3f)
                {
                    float settle = Mathf.Sin(normalizedTime * Mathf.PI * 8) * 0.02f * followValue;
                    position += new Vector3(settle, settle * 0.5f, 0);
                }
                break;

            case 1: // Yokogiri follow-through - Wide horizontal completion
                rotation = originalRotation * Quaternion.Euler(
                    5f * followValue,                       // Maintain slight elevation
                    120f * followValue * settleBoost,      // Complete the wide sweep
                    60f * followValue * settleBoost        // Strong wrist follow-through
                );
                position += new Vector3(-0.7f, 0.1f, 0.4f) * followValue * settleBoost;

                // Add rotational momentum settling
                float spinSettle = Mathf.Sin(normalizedTime * Mathf.PI * 6) * 0.03f * followValue;
                position += new Vector3(spinSettle, 0, spinSettle);
                break;

            case 2: // Gyakuyo follow-through - Reverse horizontal completion
                rotation = originalRotation * Quaternion.Euler(
                    5f * followValue,                       // Maintain slight elevation
                    -120f * followValue * settleBoost,     // Complete reverse sweep
                    -60f * followValue * settleBoost       // Reverse wrist follow-through
                );
                position += new Vector3(0.7f, 0.1f, 0.4f) * followValue * settleBoost;

                // Add reverse rotational momentum settling
                float reverseSpinSettle = Mathf.Sin(normalizedTime * Mathf.PI * -6) * 0.03f * followValue;
                position += new Vector3(reverseSpinSettle, 0, reverseSpinSettle);
                break;

            case 3: // Kiriage follow-through - Explosive upward completion with gravity
                rotation = originalRotation * Quaternion.Euler(
                    110f * followValue * settleBoost,      // Complete the explosive upward arc
                    0f,                                     // Center the blade
                    40f * followValue * settleBoost        // Complete the uppercut snap
                );

                // Gravity-affected follow-through (blade comes down naturally)
                float gravityEffect = normalizedTime * normalizedTime; // Accelerating downward
                position += new Vector3(
                    0f,
                    (1.2f * followValue * settleBoost) - (gravityEffect * 0.3f), // Peak then fall
                    0.8f * followValue * settleBoost
                );

                // Add impact settle from the explosive launch
                if (normalizedTime < 0.4f)
                {
                    float impactSettle = Mathf.Sin(normalizedTime * Mathf.PI * 12) * 0.04f * followValue;
                    position += new Vector3(impactSettle, impactSettle * 2f, 0);
                }
                break;
        }

        // Add universal settling effect for all attacks
        if (normalizedTime > 0.5f)
        {
            float universalSettle = Mathf.Sin((normalizedTime - 0.5f) * Mathf.PI * 4) * 0.01f * followValue;
            position += new Vector3(universalSettle, universalSettle, 0);
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