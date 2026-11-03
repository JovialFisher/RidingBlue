using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fades UI elements in and out without needing a CanvasGroup authored in the scene: one is
/// added on demand and its alpha is ramped on unscaled time, so it keeps working while time
/// is paused or a scene transition is running.
/// </summary>
public static class UIFade
{
    /// <summary>Default time an element takes to appear or disappear.</summary>
    public const float DefaultDuration = 0.35f;

    /// <summary>Default distance, in UI units, an element rises while it appears.</summary>
    public const float DefaultRiseDistance = 20f;

    /// <summary>Shows the given objects and eases them in together.</summary>
    public static IEnumerator FadeIn(params GameObject[] targets)
    {
        return Show(DefaultDuration, 0f, targets);
    }

    /// <summary>Shows the given objects and eases them in together over the given time.</summary>
    public static IEnumerator FadeIn(float duration, params GameObject[] targets)
    {
        return Show(duration, 0f, targets);
    }

    /// <summary>Shows the objects, easing them in as they rise into their authored spot.</summary>
    public static IEnumerator RiseIn(params GameObject[] targets)
    {
        return Show(DefaultDuration, DefaultRiseDistance, targets);
    }

    /// <summary>Shows the objects, rising them the given distance as they appear.</summary>
    public static IEnumerator RiseIn(float riseDistance, params GameObject[] targets)
    {
        return Show(DefaultDuration, riseDistance, targets);
    }

    /// <summary>Shows the objects over the given time, rising them the given distance.</summary>
    public static IEnumerator RiseIn(float duration, float riseDistance, params GameObject[] targets)
    {
        return Show(duration, riseDistance, targets);
    }

    /// <summary>Eases the given objects out together, then hides them.</summary>
    public static IEnumerator FadeOutAndHide(params GameObject[] targets)
    {
        return FadeOutAndHide(DefaultDuration, targets);
    }

    /// <summary>Eases the given objects out over the given time, then hides them.</summary>
    public static IEnumerator FadeOutAndHide(float duration, params GameObject[] targets)
    {
        List<CanvasGroup> groups = new List<CanvasGroup>();
        List<float> startAlphas = new List<float>();

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null || !targets[i].activeInHierarchy)
            {
                continue;
            }

            CanvasGroup group = EnsureCanvasGroup(targets[i]);

            // Stop a second pick while the options are still dissolving.
            group.blocksRaycasts = false;
            groups.Add(group);
            startAlphas.Add(group.alpha);
        }

        if (groups.Count > 0)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                for (int i = 0; i < groups.Count; i++)
                {
                    if (groups[i] != null)
                    {
                        groups[i].alpha = Mathf.Lerp(startAlphas[i], 0f, progress);
                    }
                }

                yield return null;
            }
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null)
            {
                continue;
            }

            CanvasGroup group = targets[i].GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 0f;
            }

            targets[i].SetActive(false);
        }
    }

    static IEnumerator Show(float duration, float riseDistance, GameObject[] targets)
    {
        List<CanvasGroup> groups = new List<CanvasGroup>();
        List<RectTransform> rects = new List<RectTransform>();
        List<Vector2> restingPositions = new List<Vector2>();

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null)
            {
                continue;
            }

            CanvasGroup group = EnsureCanvasGroup(targets[i]);

            targets[i].SetActive(true);
            group.alpha = 0f;
            group.blocksRaycasts = true;
            group.interactable = true;

            RectTransform rect = targets[i].GetComponent<RectTransform>();
            Vector2 restingPosition = rect != null ? rect.anchoredPosition : Vector2.zero;

            if (rect != null && riseDistance != 0f)
            {
                // Start low and let the element rise into the spot it was designed at.
                rect.anchoredPosition = restingPosition - Vector2.up * riseDistance;
            }

            groups.Add(group);
            rects.Add(rect);
            restingPositions.Add(restingPosition);
        }

        if (groups.Count == 0 || duration <= 0f)
        {
            SetAlpha(groups, 1f);
            SetPositions(rects, restingPositions);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float movement = Mathf.SmoothStep(0f, 1f, progress);

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] != null)
                {
                    groups[i].alpha = progress;
                }

                if (rects[i] != null)
                {
                    rects[i].anchoredPosition = Vector2.Lerp(
                        restingPositions[i] - Vector2.up * riseDistance,
                        restingPositions[i],
                        movement);
                }
            }

            yield return null;
        }

        SetAlpha(groups, 1f);
        SetPositions(rects, restingPositions);
    }

    static void SetAlpha(List<CanvasGroup> groups, float alpha)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i] != null)
            {
                groups[i].alpha = alpha;
            }
        }
    }

    static void SetPositions(List<RectTransform> rects, List<Vector2> positions)
    {
        for (int i = 0; i < rects.Count; i++)
        {
            if (rects[i] != null)
            {
                rects[i].anchoredPosition = positions[i];
            }
        }
    }

    static CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = target.AddComponent<CanvasGroup>();
        }

        return group;
    }
}
