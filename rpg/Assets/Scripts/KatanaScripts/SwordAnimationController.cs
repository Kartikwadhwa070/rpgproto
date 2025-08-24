using UnityEngine;
using System.Collections;

public class SwordAnimationController : MonoBehaviour
{
    private MeleeSwordSystem swordSystem;
    private Transform swordModel;

    [Header("Animation Settings")]
    public AnimationCurve swingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float swingIntensity = 90f;

    // Original sword transform values
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private bool isAnimating = false;

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

        StartCoroutine(AnimateAttack(comboIndex, duration));
    }

    IEnumerator AnimateAttack(int comboIndex, float duration)
    {
        isAnimating = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / duration;
            float curveValue = swingCurve.Evaluate(normalizedTime);

            ApplyAttackAnimation(comboIndex, curveValue, normalizedTime);

            yield return null;
        }

        // Return to original position
        StartCoroutine(ReturnToOriginalPosition());
        isAnimating = false;
    }

    void ApplyAttackAnimation(int comboIndex, float curveValue, float normalizedTime)
    {
        Vector3 position = originalPosition;
        Quaternion rotation = originalRotation;

        switch (comboIndex)
        {
            case 0: // First attack - Unsheathe (diagonal slash from top-right to bottom-left)
                rotation = originalRotation * Quaternion.Euler(
                    Mathf.Lerp(45f, -45f, curveValue),  // X rotation
                    Mathf.Lerp(-30f, 30f, curveValue),  // Y rotation
                    Mathf.Lerp(45f, -45f, curveValue)   // Z rotation
                );
                position += Vector3.forward * Mathf.Sin(normalizedTime * Mathf.PI) * 0.3f;
                break;

            case 1: // Second attack - Right to Left horizontal slash
                rotation = originalRotation * Quaternion.Euler(
                    0f,                                  // X rotation
                    Mathf.Lerp(60f, -60f, curveValue),  // Y rotation
                    Mathf.Lerp(-15f, 15f, curveValue)   // Z rotation
                );
                position += Vector3.right * Mathf.Lerp(0.2f, -0.2f, curveValue);
                break;

            case 2: // Third attack - Left to Right horizontal slash
                rotation = originalRotation * Quaternion.Euler(
                    0f,                                  // X rotation
                    Mathf.Lerp(-60f, 60f, curveValue),  // Y rotation
                    Mathf.Lerp(15f, -15f, curveValue)   // Z rotation
                );
                position += Vector3.right * Mathf.Lerp(-0.2f, 0.2f, curveValue);
                break;

            case 3: // Fourth attack - Bottom to Top uppercut slash (launcher)
                rotation = originalRotation * Quaternion.Euler(
                    Mathf.Lerp(-60f, 60f, curveValue),  // X rotation (bottom to top)
                    0f,                                  // Y rotation
                    Mathf.Lerp(-10f, 10f, curveValue)   // Z rotation
                );
                position += Vector3.up * Mathf.Lerp(-0.3f, 0.5f, curveValue);
                position += Vector3.forward * Mathf.Sin(normalizedTime * Mathf.PI) * 0.4f;
                break;
        }

        // Apply the calculated position and rotation
        swordModel.localPosition = position;
        swordModel.localRotation = rotation;

        // Add some movement blur effect by slightly moving the sword
        if (normalizedTime > 0.2f && normalizedTime < 0.8f)
        {
            // Add slight random movement during the swing for more dynamic feel
            Vector3 blur = new Vector3(
                Mathf.Sin(normalizedTime * 20f) * 0.02f,
                Mathf.Cos(normalizedTime * 25f) * 0.02f,
                0f
            );
            swordModel.localPosition += blur;
        }
    }

    IEnumerator ReturnToOriginalPosition()
    {
        float returnDuration = 0.3f;
        float elapsed = 0f;

        Vector3 startPos = swordModel.localPosition;
        Quaternion startRot = swordModel.localRotation;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / returnDuration;

            swordModel.localPosition = Vector3.Lerp(startPos, originalPosition, normalizedTime);
            swordModel.localRotation = Quaternion.Lerp(startRot, originalRotation, normalizedTime);

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