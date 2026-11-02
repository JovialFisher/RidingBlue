using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Transitions between a character's expression sprites instead of swapping them
/// inside a single frame. The expression that is leaving dissolves out while the
/// new one springs in with a small pop, so mood changes read as a beat of acting
/// rather than a hard cut.
///
/// Usage: build a list of every expression object a character owns, then call
/// <see cref="Show"/> for a change (it fades the rest of the set out for you) or
/// <see cref="HideAll"/> to clear the stage.
/// </summary>
public static class ExpressionFader
{
    /// <summary>Length of a normal expression transition, in seconds.</summary>
    public const float DefaultDuration = 0.22f;

    // The incoming expression springs up from a slightly smaller, slightly tilted
    // pose. The amplitudes stay small: the portraits are camera locked, so this reads
    // as a lively accent rather than a zoom.
    const float InStartScale = 0.95f;
    const float InStartTilt = -1.2f;

    static readonly Dictionary<int, ExpressionPose> poses = new Dictionary<int, ExpressionPose>();

    static FaderHost host;
    static int animationToken;

    /// <summary>
    /// Fades <paramref name="target"/> in and every other expression in
    /// <paramref name="expressions"/> out. Passing a null target hides the whole set.
    /// </summary>
    public static void Show(GameObject target, GameObject[] expressions, float duration = DefaultDuration)
    {
        if (target == null)
        {
            HideAll(expressions, duration);
            return;
        }

        int token = ++animationToken;

        if (duration <= 0f)
        {
            ApplyImmediately(target, expressions);
            return;
        }

        GetHost().StartCoroutine(Crossfade(target, expressions, duration, token));
    }

    /// <summary>Fades every expression in the set out, then deactivates it.</summary>
    public static void HideAll(GameObject[] expressions, float duration = DefaultDuration)
    {
        if (expressions == null)
        {
            return;
        }

        int token = ++animationToken;

        if (duration <= 0f)
        {
            ApplyImmediately(null, expressions);
            return;
        }

        GetHost().StartCoroutine(FadeOutAll(expressions, duration, token));
    }

    static IEnumerator Crossfade(GameObject target, GameObject[] expressions, float duration, int token)
    {
        List<GameObject> outgoing = new List<GameObject>(expressions != null ? expressions.Length : 0);

        if (expressions != null)
        {
            for (int i = 0; i < expressions.Length; i++)
            {
                GameObject candidate = expressions[i];
                if (candidate == null || candidate == target || !candidate.activeSelf)
                {
                    continue;
                }

                outgoing.Add(candidate);
            }
        }

        float[] outgoingAlpha = new float[outgoing.Count];
        for (int i = 0; i < outgoing.Count; i++)
        {
            outgoingAlpha[i] = AlphaOf(outgoing[i]);
        }

        // A sprite that is switched off is not on screen yet, so it has to fade up from nothing.
        float targetStartAlpha = target.activeSelf ? AlphaOf(target) : 0f;
        // Repeating the expression that is already on screen should not replay the pop.
        bool popIn = targetStartAlpha < 0.99f;

        if (!popIn && outgoing.Count == 0)
        {
            yield break;
        }

        if (!target.activeSelf)
        {
            target.SetActive(true);
        }

        ExpressionPose pose = PoseOf(target);

        if (popIn)
        {
            ApplyIncoming(target, pose, targetStartAlpha, 0f);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (token != animationToken)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (popIn)
            {
                ApplyIncoming(target, pose, targetStartAlpha, t);
            }

            for (int i = 0; i < outgoing.Count; i++)
            {
                ApplyOutgoing(outgoing[i], outgoingAlpha[i], t);
            }

            yield return null;
        }

        if (token != animationToken)
        {
            yield break;
        }

        if (pose != null)
        {
            pose.Restore();
        }

        SetAlpha(target, 1f);

        for (int i = 0; i < outgoing.Count; i++)
        {
            FinishOutgoing(outgoing[i]);
        }
    }

    static IEnumerator FadeOutAll(GameObject[] expressions, float duration, int token)
    {
        List<GameObject> visible = new List<GameObject>(expressions.Length);
        List<float> startAlpha = new List<float>(expressions.Length);

        for (int i = 0; i < expressions.Length; i++)
        {
            GameObject expression = expressions[i];
            if (expression == null || !expression.activeSelf)
            {
                continue;
            }

            float alpha = AlphaOf(expression);
            if (alpha <= 0.001f)
            {
                FinishOutgoing(expression);
                continue;
            }

            visible.Add(expression);
            startAlpha.Add(alpha);
        }

        if (visible.Count == 0)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (token != animationToken)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < visible.Count; i++)
            {
                ApplyOutgoing(visible[i], startAlpha[i], t);
            }

            yield return null;
        }

        if (token != animationToken)
        {
            yield break;
        }

        for (int i = 0; i < visible.Count; i++)
        {
            FinishOutgoing(visible[i]);
        }
    }

    // Dissolves the new expression in while it springs up from a slightly smaller,
    // slightly tilted pose, so the change lands with a bit of weight.
    static void ApplyIncoming(GameObject target, ExpressionPose pose, float startAlpha, float t)
    {
        if (target == null || pose == null)
        {
            return;
        }

        float fade = EaseOutCubic(t);
        float spring = EaseOutBack(t, 1.6f);

        SetAlpha(target, Mathf.Lerp(startAlpha, 1f, fade));

        Transform targetTransform = target.transform;
        targetTransform.localScale = pose.scale * Mathf.Lerp(InStartScale, 1f, spring);
        targetTransform.localRotation = pose.rotation * Quaternion.Euler(0f, 0f, Mathf.Lerp(InStartTilt, 0f, spring));
    }

    // Dissolves the old expression out on the same curve as the incoming fade, so the
    // two overlap for the whole transition instead of leaving a gap in the middle. It
    // keeps its authored pose: moving it would smear against the incoming portrait.
    static void ApplyOutgoing(GameObject expression, float startAlpha, float t)
    {
        if (expression == null)
        {
            return;
        }

        SetAlpha(expression, Mathf.Lerp(startAlpha, 0f, EaseOutCubic(t)));
    }

    static void FinishOutgoing(GameObject expression)
    {
        if (expression == null)
        {
            return;
        }

        ExpressionPose pose = PoseOf(expression);
        if (pose != null)
        {
            pose.Restore();
        }

        SetAlpha(expression, 0f);
        expression.SetActive(false);
    }

    static void ApplyImmediately(GameObject keep, GameObject[] expressions)
    {
        if (expressions != null)
        {
            for (int i = 0; i < expressions.Length; i++)
            {
                GameObject expression = expressions[i];
                if (expression == null || expression == keep)
                {
                    continue;
                }

                ExpressionPose pose = PoseOf(expression);
                if (pose != null)
                {
                    pose.Restore();
                }

                SetAlpha(expression, 0f);
                expression.SetActive(false);
            }
        }

        if (keep != null)
        {
            if (!keep.activeSelf)
            {
                keep.SetActive(true);
            }

            ExpressionPose pose = PoseOf(keep);
            if (pose != null)
            {
                pose.Restore();
            }

            SetAlpha(keep, 1f);
        }
    }

    static void SetAlpha(GameObject expression, float alpha)
    {
        ExpressionPose pose = PoseOf(expression);
        if (pose != null)
        {
            pose.SetAlpha(alpha);
        }
    }

    static float AlphaOf(GameObject expression)
    {
        ExpressionPose pose = PoseOf(expression);
        return pose != null ? pose.Alpha : 1f;
    }

    static ExpressionPose PoseOf(GameObject expression)
    {
        if (expression == null)
        {
            return null;
        }

        int id = expression.GetInstanceID();
        ExpressionPose pose;
        if (poses.TryGetValue(id, out pose) && pose.owner == expression)
        {
            return pose;
        }

        pose = new ExpressionPose(expression);
        poses[id] = pose;
        return pose;
    }

    static FaderHost GetHost()
    {
        if (host == null)
        {
            GameObject go = new GameObject("Expression Fader");
            go.hideFlags = HideFlags.HideAndDontSave;
            host = go.AddComponent<FaderHost>();
        }

        return host;
    }

    static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    static float EaseOutBack(float t, float overshoot)
    {
        t = Mathf.Clamp01(t);
        float shifted = t - 1f;
        return 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
    }
}

/// <summary>Runs expression transitions on a hidden helper object.</summary>
class FaderHost : MonoBehaviour
{
}

/// <summary>
/// Remembers an expression's resting pose and the graphics that get faded, so the
/// transition can always restore the sprite exactly as it was authored.
/// </summary>
class ExpressionPose
{
    public readonly GameObject owner;

    public readonly Vector3 scale;
    public readonly Quaternion rotation;

    readonly Graphic[] graphics;
    readonly SpriteRenderer[] spriteRenderers;

    public ExpressionPose(GameObject owner)
    {
        this.owner = owner;

        Transform transform = owner.transform;
        scale = transform.localScale;
        rotation = transform.localRotation;
        graphics = owner.GetComponentsInChildren<Graphic>(true);
        spriteRenderers = owner.GetComponentsInChildren<SpriteRenderer>(true);
    }

    public float Alpha
    {
        get
        {
            if (graphics != null && graphics.Length > 0)
            {
                return graphics[0].color.a;
            }

            if (spriteRenderers != null && spriteRenderers.Length > 0)
            {
                return spriteRenderers[0].color.a;
            }

            return 1f;
        }
    }

    public void SetAlpha(float alpha)
    {
        if (graphics != null)
        {
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null)
                {
                    continue;
                }

                Color color = graphic.color;
                color.a = alpha;
                graphic.color = color;
            }
        }

        if (spriteRenderers != null)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null)
                {
                    continue;
                }

                Color color = spriteRenderer.color;
                color.a = alpha;
                spriteRenderer.color = color;
            }
        }
    }

    public void Restore()
    {
        Transform transform = owner.transform;
        transform.localScale = scale;
        transform.localRotation = rotation;
    }
}
