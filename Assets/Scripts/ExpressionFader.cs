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
    const float InOvershoot = 1.55f;

    // The outgoing portrait finishes dissolving at this point of the transition, just
    // before the new one settles. Leaving it up for the whole transition would put both
    // portraits at equal strength in the middle of the switch, which reads as a double
    // exposure; dropping it early instead leaves the new face cleanly on its own.
    const float OutgoingPortion = 0.78f;

    // A single long frame must not skip part of a fade, or the switch reads as a cut.
    const float MaxStep = 1f / 30f;

    static readonly Dictionary<int, ExpressionPose> poses = new Dictionary<int, ExpressionPose>();
    static readonly Dictionary<int, Track> tracks = new Dictionary<int, Track>();
    static readonly List<int> staleKeys = new List<int>();

    static FaderHost host;

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

        Track track = Begin(expressions);

        if (duration <= 0f || !Application.isPlaying || PoseOf(target) == null)
        {
            ApplyImmediately(target, expressions);
            return;
        }

        FaderHost runner = GetHost();
        if (runner == null)
        {
            ApplyImmediately(target, expressions);
            return;
        }

        runner.StartCoroutine(Crossfade(target, expressions, duration, track, track.token));
    }

    /// <summary>Fades every expression in the set out, then deactivates it.</summary>
    public static void HideAll(GameObject[] expressions, float duration = DefaultDuration)
    {
        if (expressions == null)
        {
            return;
        }

        Track track = Begin(expressions);

        if (duration <= 0f || !Application.isPlaying)
        {
            ApplyImmediately(null, expressions);
            return;
        }

        FaderHost runner = GetHost();
        if (runner == null)
        {
            ApplyImmediately(null, expressions);
            return;
        }

        runner.StartCoroutine(FadeOutAll(expressions, duration, track, track.token));
    }

    static IEnumerator Crossfade(GameObject target, GameObject[] expressions, float duration, Track track, int token)
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

                // Whatever is still partly visible from a switch that got interrupted is
                // picked up here from its current opacity, so a fast line change never
                // strands a half-faded portrait on screen.
                if (AlphaOf(candidate) <= 0.001f)
                {
                    FinishOutgoing(candidate);
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
        bool popIn = targetStartAlpha < 0.98f;

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
            if (!IsCurrent(track, token))
            {
                yield break;
            }

            float t = Mathf.Clamp01(elapsed / duration);
            float outgoingT = Mathf.Clamp01(t / OutgoingPortion);

            if (popIn)
            {
                ApplyIncoming(target, pose, targetStartAlpha, t);
            }

            for (int i = 0; i < outgoing.Count; i++)
            {
                ApplyOutgoing(outgoing[i], outgoingAlpha[i], outgoingT);
            }

            yield return null;

            elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
        }

        if (!IsCurrent(track, token))
        {
            yield break;
        }

        if (pose != null)
        {
            pose.Restore();
        }

        for (int i = 0; i < outgoing.Count; i++)
        {
            FinishOutgoing(outgoing[i]);
        }
    }

    static IEnumerator FadeOutAll(GameObject[] expressions, float duration, Track track, int token)
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
            if (!IsCurrent(track, token))
            {
                yield break;
            }

            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < visible.Count; i++)
            {
                // Same curve as a crossfade's outgoing half: hold the portrait, then let it
                // go quickly at the end so a scene's last expression never fades in a flat line.
                SetAlpha(visible[i], Mathf.Lerp(startAlpha[i], 0f, EaseInCubic(t)));
            }

            yield return null;

            elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
        }

        if (!IsCurrent(track, token))
        {
            yield break;
        }

        for (int i = 0; i < visible.Count; i++)
        {
            FinishOutgoing(visible[i]);
        }
    }

    // Dissolves the new expression in while it springs up from a slightly smaller,
    // slightly tilted pose, so the change lands with a bit of weight. The alpha leads the
    // motion: the face is readable early, while the scale and tilt settle after it.
    static void ApplyIncoming(GameObject target, ExpressionPose pose, float startAlpha, float t)
    {
        if (target == null || pose == null)
        {
            return;
        }

        SetAlpha(target, Mathf.Lerp(startAlpha, 1f, EaseOutCubic(t)));

        float spring = EaseOutBack(t, InOvershoot);
        float settle = EaseOutCubic(t);

        Transform targetTransform = target.transform;
        targetTransform.localScale = pose.scale * Mathf.Lerp(InStartScale, 1f, spring);
        targetTransform.localRotation = pose.rotation * Quaternion.Euler(0f, 0f, Mathf.Lerp(InStartTilt, 0f, settle));
    }

    // Dissolves the old expression out. It keeps its authored pose: moving it would smear
    // against the incoming portrait, and the crossfade already reads as motion.
    static void ApplyOutgoing(GameObject expression, float startAlpha, float t)
    {
        if (expression == null)
        {
            return;
        }

        SetAlpha(expression, Mathf.Lerp(startAlpha, 0f, EaseInCubic(t)));
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

        PruneDeadPoses();

        pose = new ExpressionPose(expression);
        poses[id] = pose;
        return pose;
    }

    /// <summary>
    /// Starts a new transition for one expression set and makes it the only one that set
    /// obeys. Sets are tracked separately, so hiding one character never cuts a fade that
    /// another set is still running.
    /// </summary>
    static Track Begin(GameObject[] expressions)
    {
        int key = KeyOf(expressions);

        Track track;
        if (!tracks.TryGetValue(key, out track) || track.IsDead)
        {
            PruneDeadTracks();
            track = new Track();
            tracks[key] = track;
        }

        track.members = expressions;
        track.token = track.token + 1;
        return track;
    }

    static bool IsCurrent(Track track, int token)
    {
        return track != null && track.token == token;
    }

    static int KeyOf(GameObject[] expressions)
    {
        unchecked
        {
            int hash = (int)2166136261;
            int length = expressions != null ? expressions.Length : 0;

            hash = (hash ^ length) * 16777619;

            for (int i = 0; i < length; i++)
            {
                int id = expressions[i] != null ? expressions[i].GetInstanceID() : 0;
                hash = (hash ^ id) * 16777619;
            }

            return hash;
        }
    }

    static void PruneDeadTracks()
    {
        if (tracks.Count < 8)
        {
            return;
        }

        staleKeys.Clear();
        foreach (KeyValuePair<int, Track> pair in tracks)
        {
            if (pair.Value.IsDead)
            {
                staleKeys.Add(pair.Key);
            }
        }

        for (int i = 0; i < staleKeys.Count; i++)
        {
            tracks.Remove(staleKeys[i]);
        }
    }

    static void PruneDeadPoses()
    {
        if (poses.Count < 64)
        {
            return;
        }

        staleKeys.Clear();
        foreach (KeyValuePair<int, ExpressionPose> pair in poses)
        {
            if (pair.Value.owner == null)
            {
                staleKeys.Add(pair.Key);
            }
        }

        for (int i = 0; i < staleKeys.Count; i++)
        {
            poses.Remove(staleKeys[i]);
        }
    }

    static FaderHost GetHost()
    {
        if (host != null)
        {
            return host;
        }

        GameObject go = new GameObject("Expression Fader");
        go.hideFlags = HideFlags.HideAndDontSave;
        host = go.AddComponent<FaderHost>();
        return host;
    }

    static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    static float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
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
/// One set of expressions that transitions together. The token lets a newer request take
/// over from an older one without either of them touching the other's portraits.
/// </summary>
class Track
{
    public GameObject[] members;
    public int token;

    public bool IsDead
    {
        get
        {
            if (members == null)
            {
                return true;
            }

            for (int i = 0; i < members.Length; i++)
            {
                if (members[i] != null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

/// <summary>
/// Remembers an expression's resting pose and the graphics that get faded, so the
/// transition can always restore the sprite exactly as it was authored.
///
/// Fading is done in normalized space: the authored opacity of every layer is kept and
/// scaled, so a portrait built from a solid base plus a translucent highlight keeps its
/// shading through the fade instead of collapsing into one flat alpha.
/// </summary>
class ExpressionPose
{
    public readonly GameObject owner;

    public readonly Vector3 scale;
    public readonly Quaternion rotation;

    readonly CanvasGroup canvasGroup;
    readonly float canvasGroupAlpha;

    readonly Graphic[] graphics;
    readonly float[] graphicAlpha;

    readonly SpriteRenderer[] spriteRenderers;
    readonly float[] spriteAlpha;

    public ExpressionPose(GameObject owner)
    {
        this.owner = owner;

        Transform ownerTransform = owner.transform;
        scale = ownerTransform.localScale;
        rotation = ownerTransform.localRotation;

        canvasGroup = owner.GetComponent<CanvasGroup>();
        canvasGroupAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;

        // A CanvasGroup multiplies every graphic under it, so the group is faded instead of
        // the graphics themselves; doing both would darken the portrait twice and bend the
        // fade curve. Sprites live outside the canvas and are always faded directly.
        graphics = canvasGroup == null
            ? owner.GetComponentsInChildren<Graphic>(true)
            : new Graphic[0];

        graphicAlpha = new float[graphics.Length];
        for (int i = 0; i < graphics.Length; i++)
        {
            graphicAlpha[i] = graphics[i] != null ? graphics[i].color.a : 1f;
        }

        spriteRenderers = owner.GetComponentsInChildren<SpriteRenderer>(true);
        spriteAlpha = new float[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            spriteAlpha[i] = spriteRenderers[i] != null ? spriteRenderers[i].color.a : 1f;
        }
    }

    /// <summary>Opacity of the portrait on screen, from 0 (gone) to 1 (fully shown).</summary>
    public float Alpha
    {
        get
        {
            if (canvasGroup != null)
            {
                return Normalize(canvasGroup.alpha, canvasGroupAlpha);
            }

            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                {
                    return Normalize(graphics[i].color.a, graphicAlpha[i]);
                }
            }

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    return Normalize(spriteRenderers[i].color.a, spriteAlpha[i]);
                }
            }

            return 1f;
        }
    }

    public void SetAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = canvasGroupAlpha * alpha;
        }

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
            {
                continue;
            }

            Color color = graphic.color;
            color.a = graphicAlpha[i] * alpha;
            graphic.color = color;
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            Color color = spriteRenderer.color;
            color.a = spriteAlpha[i] * alpha;
            spriteRenderer.color = color;
        }
    }

    public void Restore()
    {
        RestoreTransform();
        RestoreAlpha();
    }

    public void RestoreTransform()
    {
        if (owner == null)
        {
            return;
        }

        Transform ownerTransform = owner.transform;
        ownerTransform.localScale = scale;
        ownerTransform.localRotation = rotation;
    }

    public void RestoreAlpha()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = canvasGroupAlpha;
        }

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
            {
                continue;
            }

            Color color = graphic.color;
            color.a = graphicAlpha[i];
            graphic.color = color;
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            Color color = spriteRenderer.color;
            color.a = spriteAlpha[i];
            spriteRenderer.color = color;
        }
    }

    static float Normalize(float current, float authored)
    {
        if (authored <= 0.001f)
        {
            return current > 0.001f ? 1f : 0f;
        }

        return Mathf.Clamp01(current / authored);
    }
}
