using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class SwordEntryManager : MonoBehaviour
{
    [Header("Dramatic Entry Settings")]
    public float entryRiseHeight = 5f;
    public float entryRiseSpeed = 8f;
    public float entryPauseTime = 0.5f;
    public float duplicationTime = 1f;
    public float spreadToPositionTime = 1.5f;
    public AnimationCurve entryRiseCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));
    public AnimationCurve spreadCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));

    private FloatingSwordSystem swordSystem;

    public void Initialize(FloatingSwordSystem system)
    {
        swordSystem = system;
    }

    public IEnumerator PerformDramaticEntry(Action onComplete = null)
    {
        var swords = swordSystem.Swords;

        // Phase 1: Setup initial positions
        Vector3 startPos = swordSystem.transform.position + Vector3.up * (entryRiseHeight + 2f);
        Vector3 riseTarget = swordSystem.transform.position + Vector3.up * entryRiseHeight;

        InitializeSwordsForEntry(swords, startPos);

        // Phase 2: Descend first sword to rise height
        yield return StartCoroutine(DescendFirstSword(swords, startPos, riseTarget));

        // Phase 3: Pause at peak
        yield return new WaitForSeconds(entryPauseTime);

        // Phase 4: Duplication effect
        yield return StartCoroutine(DuplicateSwords(swords, riseTarget));

        // Phase 5: Spread to final positions
        yield return StartCoroutine(SpreadToFinalPositions(swords, riseTarget));

        // Phase 6: Switch to normal floating mode
        foreach (SwordController sword in swords)
        {
            sword.SetFloatingMode(true);
        }

        onComplete?.Invoke();
    }

    public IEnumerator PerformDramaticExit(Action onComplete = null)
    {
        var swords = swordSystem.Swords;

        // Phase 1: Gather to center
        Vector3 gatherTarget = swordSystem.transform.position + Vector3.up * entryRiseHeight;
        yield return StartCoroutine(GatherSwords(swords, gatherTarget));

        // Phase 2: Pause at peak
        yield return new WaitForSeconds(entryPauseTime);

        // Phase 3: Fade out swords except first
        yield return StartCoroutine(FadeOutSwords(swords, gatherTarget));

        // Phase 4: Rise and fade final sword
        if (swords.Count > 0)
        {
            yield return StartCoroutine(FinalSwordExit(swords[0], gatherTarget));
        }

        onComplete?.Invoke();
    }

    void InitializeSwordsForEntry(List<SwordController> swords, Vector3 startPos)
    {
        for (int i = 0; i < swords.Count; i++)
        {
            swords[i].SetBattleMode(true, i == 0 ? 1f : 0f);
            swords[i].SetEntryPosition(startPos);
        }
    }

    IEnumerator DescendFirstSword(List<SwordController> swords, Vector3 startPos, Vector3 riseTarget)
    {
        float descendTimer = 0f;
        float descendDuration = 2f / entryRiseSpeed;

        while (descendTimer < descendDuration)
        {
            descendTimer += Time.deltaTime;
            float normalizedTime = descendTimer / descendDuration;
            float curveValue = entryRiseCurve.Evaluate(normalizedTime);

            Vector3 currentPos = Vector3.Lerp(startPos, riseTarget, curveValue);

            for (int i = 0; i < swords.Count; i++)
            {
                swords[i].SetEntryPosition(currentPos);
            }

            yield return null;
        }

        // Ensure all swords are at target position
        for (int i = 0; i < swords.Count; i++)
        {
            swords[i].SetEntryPosition(riseTarget);
        }
    }

    IEnumerator DuplicateSwords(List<SwordController> swords, Vector3 peakPosition)
    {
        float duplicationTimer = 0f;

        while (duplicationTimer < duplicationTime)
        {
            duplicationTimer += Time.deltaTime;
            float normalizedTime = duplicationTimer / duplicationTime;

            for (int i = 1; i < swords.Count; i++)
            {
                float swordDelay = (float)(i - 1) / (swords.Count - 1) * 0.5f;
                float swordTime = Mathf.Clamp01((normalizedTime - swordDelay) * 2f);

                swords[i].SetBattleMode(true, swordTime);

                Vector3 offset = Vector3.up * Mathf.Sin(normalizedTime * Mathf.PI * 3) * 0.1f;
                swords[i].SetEntryPosition(peakPosition + offset);
            }

            yield return null;
        }

        // Ensure all swords are fully visible
        for (int i = 0; i < swords.Count; i++)
        {
            swords[i].SetBattleMode(true, 1f);
            swords[i].SetEntryPosition(peakPosition);
        }
    }

    IEnumerator SpreadToFinalPositions(List<SwordController> swords, Vector3 peakPosition)
    {
        float spreadTimer = 0f;

        while (spreadTimer < spreadToPositionTime)
        {
            spreadTimer += Time.deltaTime;
            float normalizedTime = spreadTimer / spreadToPositionTime;
            float curveValue = spreadCurve.Evaluate(normalizedTime);

            for (int i = 0; i < swords.Count; i++)
            {
                float angle = (360f / swords.Count) * i;
                Vector3 finalOffset = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * swordSystem.FloatDistance,
                    swordSystem.FloatHeight,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * swordSystem.FloatDistance
                );
                Vector3 finalPosition = swordSystem.transform.position + finalOffset;

                Vector3 currentPos = Vector3.Lerp(peakPosition, finalPosition, curveValue);
                swords[i].SetEntryPosition(currentPos);

                Quaternion finalRotation = Quaternion.LookRotation(finalOffset.normalized);
                swords[i].SetEntryRotation(finalRotation);
            }

            yield return null;
        }
    }

    IEnumerator GatherSwords(List<SwordController> swords, Vector3 gatherTarget)
    {
        float gatherTimer = 0f;

        foreach (SwordController sword in swords)
        {
            sword.SetFloatingMode(false);
        }

        while (gatherTimer < spreadToPositionTime)
        {
            gatherTimer += Time.deltaTime;
            float normalizedTime = gatherTimer / spreadToPositionTime;
            float curveValue = spreadCurve.Evaluate(1f - normalizedTime);

            for (int i = 0; i < swords.Count; i++)
            {
                float angle = (360f / swords.Count) * i;
                Vector3 currentFloatOffset = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * swordSystem.FloatDistance,
                    swordSystem.FloatHeight,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * swordSystem.FloatDistance
                );
                Vector3 currentFloatPosition = swordSystem.transform.position + currentFloatOffset;

                Vector3 currentPos = Vector3.Lerp(currentFloatPosition, gatherTarget, 1f - curveValue);
                swords[i].SetEntryPosition(currentPos);
            }

            yield return null;
        }
    }

    IEnumerator FadeOutSwords(List<SwordController> swords, Vector3 gatherTarget)
    {
        float fadeTimer = 0f;

        while (fadeTimer < duplicationTime)
        {
            fadeTimer += Time.deltaTime;
            float normalizedTime = fadeTimer / duplicationTime;

            for (int i = swords.Count - 1; i >= 1; i--)
            {
                float swordDelay = (float)(swords.Count - 1 - i) / (swords.Count - 1) * 0.5f;
                float swordTime = Mathf.Clamp01((normalizedTime - swordDelay) * 2f);
                float visibility = 1f - swordTime;

                swords[i].SetBattleMode(false, visibility);

                Vector3 offset = Vector3.up * Mathf.Sin((1f - normalizedTime) * Mathf.PI * 3) * 0.1f;
                swords[i].SetEntryPosition(gatherTarget + offset);
            }

            yield return null;
        }
    }

    IEnumerator FinalSwordExit(SwordController lastSword, Vector3 gatherTarget)
    {
        Vector3 finalExitPosition = swordSystem.transform.position + Vector3.up * (entryRiseHeight + 2f);
        float riseTimer = 0f;
        float riseDuration = 2f / entryRiseSpeed;

        while (riseTimer < riseDuration)
        {
            riseTimer += Time.deltaTime;
            float normalizedTime = riseTimer / riseDuration;
            float curveValue = entryRiseCurve.Evaluate(normalizedTime);

            Vector3 currentPos = Vector3.Lerp(gatherTarget, finalExitPosition, curveValue);
            lastSword.SetEntryPosition(currentPos);

            yield return null;
        }

        // Final fade out
        float finalFadeTimer = 0f;
        float finalFadeDuration = 0.5f;

        while (finalFadeTimer < finalFadeDuration)
        {
            finalFadeTimer += Time.deltaTime;
            float visibility = 1f - (finalFadeTimer / finalFadeDuration);
            lastSword.SetBattleMode(false, visibility);

            yield return null;
        }
    }
}