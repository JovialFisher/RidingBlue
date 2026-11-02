using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gives the dialogue box a designed panel instead of the bare black rectangle it ships
/// with: a deep blue bottom card with rounded top corners, a soft shadow above it and a cool
/// hairline top edge, all fading smoothly from #002Dff at the top to #000C47 at the bottom.
/// Everything is drawn from sprites generated at runtime, so no extra assets are needed.
/// </summary>
public static class DialoguePanel
{
    const int TextureSize = 128;
    const int GradientWidth = 8;
    const int GradientHeight = 64;

    const float CornerRadius = 26f;
    const float FrameWidth = 2f;
    const float FrameBand = 40f;
    const float PixelsPerUnit = 100f;

    const float ShadowHeight = 72f;
    const float ShadowOverhang = 60f;

    const string FillName = "Panel Fill";
    const string ShadowName = "Panel Shadow";
    const string FrameName = "Panel Frame";
    const string SplitterName = "Splitter";

    /// <summary>The panel fades from #002Dff along its top edge to #000C47 at the bottom.</summary>
    static readonly Color GradientTop = new Color32(0x00, 0x2D, 0xFF, 0xFF);
    static readonly Color GradientBottom = new Color32(0x00, 0x0C, 0x47, 0xFF);

    /// <summary>Tint for the fill; its hue comes from the vertex gradient, so it only carries alpha.</summary>
    static readonly Color FillTint = new Color(1f, 1f, 1f, 0.88f);
    static readonly Color AccentColor = new Color(0.66f, 0.8f, 1f, 0.55f);
    static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.38f);
    static readonly Color SplitterColor = new Color(0.66f, 0.8f, 1f, 0.22f);

    static Sprite panelSprite;
    static Sprite frameSprite;
    static Sprite shadowSprite;

    /// <summary>Restyles the given panel, unless it is not the plain texture-less slab.</summary>
    public static void Apply(GameObject panel)
    {
        if (panel == null)
        {
            return;
        }

        RawImage rawImage = panel.GetComponent<RawImage>();
        if (rawImage == null || rawImage.texture != null)
        {
            return;
        }

        if (panel.transform.Find(FillName) != null)
        {
            return;
        }

        // A GameObject only accepts one Graphic, and the box already carries a RawImage, so the
        // slab is switched off and the designed fill is drawn by a child layer instead.
        bool raycastTarget = rawImage.raycastTarget;
        rawImage.enabled = false;

        // Children draw over the panel and in sibling order, so the decoration goes in first
        // and the authored name, splitter and body text keep rendering on top of it.
        AddShadow(panel);
        AddFill(panel, raycastTarget);
        AddLayer(panel, FrameName, FrameSprite, AccentColor, 2, Image.Type.Sliced);
        TintSplitter(panel);
    }

    static void AddFill(GameObject panel, bool raycastTarget)
    {
        RectTransform rect = AddLayer(panel, FillName, PanelSprite, FillTint, 1, Image.Type.Sliced);
        Image image = rect.GetComponent<Image>();
        image.raycastTarget = raycastTarget;

        // The ramp is painted per vertex rather than baked into the sprite, because the fill is
        // nine-sliced and a texture gradient would band where the slices stretch.
        UIVertexGradient gradient = rect.GetComponent<UIVertexGradient>();
        if (gradient == null)
        {
            gradient = rect.gameObject.AddComponent<UIVertexGradient>();
        }

        gradient.SetColors(GradientTop, GradientBottom);
    }

    static void AddShadow(GameObject panel)
    {
        RectTransform rect = AddLayer(panel, ShadowName, ShadowSprite, ShadowColor, 0, Image.Type.Simple);

        // Sits just above the panel's top edge, reaching a little past it on both sides.
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(ShadowOverhang * 2f, ShadowHeight);
        rect.anchoredPosition = Vector2.zero;
    }

    static RectTransform AddLayer(GameObject panel, string name, Sprite sprite, Color color, int siblingIndex, Image.Type type)
    {
        Transform existing = panel.transform.Find(name);
        GameObject layer;

        if (existing != null)
        {
            layer = existing.gameObject;
        }
        else
        {
            layer = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            layer.transform.SetParent(panel.transform, false);
        }

        RectTransform rect = (RectTransform)layer.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = layer.GetComponent<Image>();
        image.sprite = sprite;
        image.type = type;
        image.color = color;
        image.raycastTarget = false;

        layer.transform.SetSiblingIndex(siblingIndex);
        return rect;
    }

    static void TintSplitter(GameObject panel)
    {
        Transform splitter = panel.transform.Find(SplitterName);
        if (splitter == null)
        {
            return;
        }

        Graphic graphic = splitter.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.color = SplitterColor;
        }
    }

    static Sprite PanelSprite
    {
        get
        {
            if (panelSprite == null)
            {
                panelSprite = BuildPanelSprite();
            }

            return panelSprite;
        }
    }

    static Sprite FrameSprite
    {
        get
        {
            if (frameSprite == null)
            {
                frameSprite = BuildFrameSprite();
            }

            return frameSprite;
        }
    }

    static Sprite ShadowSprite
    {
        get
        {
            if (shadowSprite == null)
            {
                shadowSprite = BuildGradientSprite("Dialogue Panel Shadow", 0f, 1f);
            }

            return shadowSprite;
        }
    }

    static Sprite BuildPanelSprite()
    {
        Texture2D texture = CreateTexture("Dialogue Panel", TextureSize, TextureSize);

        float half = TextureSize * 0.5f;
        Color32[] pixels = new Color32[TextureSize * TextureSize];

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float px = x + 0.5f - half;
                float py = y + 0.5f - half;

                float coverage = Coverage(TopRoundedDistance(px, py, half, CornerRadius));
                pixels[y * TextureSize + x] = White(coverage);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        float border = CornerRadius + FrameWidth + 2f;
        return BuildSprite(texture, border, 12f, border, border);
    }

    static Sprite BuildFrameSprite()
    {
        Texture2D texture = CreateTexture("Dialogue Panel Frame", TextureSize, TextureSize);

        float half = TextureSize * 0.5f;
        float insetHalf = half - FrameWidth;
        float insetRadius = Mathf.Max(0f, CornerRadius - FrameWidth);

        // The stroke only lives in the top band and tapers on its way down, so the sides fade
        // out just past the corners instead of running down to the bottom of the screen.
        float fadeTop = half - FrameWidth;
        float fadeBottom = half - FrameBand;

        Color32[] pixels = new Color32[TextureSize * TextureSize];

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float px = x + 0.5f - half;
                float py = y + 0.5f - half;

                float coverage = Coverage(TopRoundedDistance(px, py, half, CornerRadius))
                                 - Coverage(TopRoundedDistance(px, py, insetHalf, insetRadius));

                if (coverage > 0f)
                {
                    coverage *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeBottom, fadeTop, py));
                }

                pixels[y * TextureSize + x] = White(coverage);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        return BuildSprite(texture, FrameBand, 8f, FrameBand, FrameBand);
    }

    /// <summary>Builds a vertical white ramp whose alpha runs from one end to the other.</summary>
    static Sprite BuildGradientSprite(string name, float alphaAtTop, float alphaAtBottom)
    {
        Texture2D texture = CreateTexture(name, GradientWidth, GradientHeight);

        Color32[] pixels = new Color32[GradientWidth * GradientHeight];

        for (int y = 0; y < GradientHeight; y++)
        {
            float t = y / (GradientHeight - 1f);
            float alpha = Mathf.Lerp(alphaAtBottom, alphaAtTop, Mathf.SmoothStep(0f, 1f, t));

            for (int x = 0; x < GradientWidth; x++)
            {
                pixels[y * GradientWidth + x] = White(alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        return BuildSprite(texture, 0f, 0f, 0f, 0f);
    }

    static Texture2D CreateTexture(string name, int width, int height)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontUnloadUnusedAsset
        };
    }

    static Sprite BuildSprite(Texture2D texture, float left, float bottom, float right, float top)
    {
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            PixelsPerUnit,
            0,
            SpriteMeshType.FullRect,
            new Vector4(left, bottom, right, top));

        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }

    static Color32 White(float alpha)
    {
        return new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
    }

    /// <summary>
    /// Signed distance to a rectangle whose top corners are rounded and whose bottom corners
    /// are square; negative inside. Only the top edges are rounded because the panel sits
    /// flush against the bottom of the screen.
    /// </summary>
    static float TopRoundedDistance(float px, float py, float halfSize, float radius)
    {
        float box = Mathf.Max(Mathf.Abs(px) - halfSize, Mathf.Max(py - halfSize, -halfSize - py));

        float dx = Mathf.Abs(px) - (halfSize - radius);
        float dy = py - (halfSize - radius);
        float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
        float corner = outside - radius;

        return Mathf.Max(box, corner);
    }

    /// <summary>Turns the distance into antialiased coverage one texel wide.</summary>
    static float Coverage(float distance)
    {
        return Mathf.Clamp01(0.5f - distance);
    }
}
