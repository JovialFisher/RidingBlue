using UnityEngine;

/// <summary>
/// Draws the pause menu's shapes at runtime: a deep-blue gradient card with a cool hairline and
/// soft halos, a dimmer for the scene behind it, a rounded plate for the corner pause button and
/// its two-bar icon. The colours are the dialogue panel's, so the menu belongs to the same UI.
/// </summary>
public static class PauseMenuArt
{
    const float PixelsPerUnit = 100f;

    const int PanelSize = 256;
    const float PanelRadius = 38f;
    const float PanelFrameWidth = 2f;

    const int PlateSize = 96;
    const float PlateRadius = 14f;
    const float PlateFrameWidth = 1.5f;

    static readonly Color32 GradientTopColor = new Color32(0x00, 0x2D, 0xFF, 0xFF);
    static readonly Color32 GradientBottomColor = new Color32(0x00, 0x0C, 0x47, 0xFF);

    static Sprite panelSprite;
    static Sprite frameSprite;
    static Sprite plateSprite;
    static Sprite plateFrameSprite;
    static Sprite dimSprite;
    static Sprite pauseIconSprite;

    /// <summary>The shared accent of the game's menus and dialogue panel.</summary>
    public static readonly Color Accent = new Color(0.66f, 0.8f, 1f, 1f);

    public static readonly Color GradientTop = GradientTopColor;
    public static readonly Color GradientBottom = GradientBottomColor;

    /// <summary>Tint for the card fill; its hue comes from the vertex gradient, so only alpha carries.</summary>
    public static readonly Color CardTint = new Color(1f, 1f, 1f, 0.96f);

    public static readonly Color DimColor = new Color32(0x00, 0x0A, 0x24, 0xFF);
    public static readonly Color HaloColor = new Color(0.24f, 0.45f, 1f, 0.14f);
    public static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.34f);
    public static readonly Color FrameColor = new Color(0.66f, 0.8f, 1f, 0.5f);

    public static Sprite Panel
    {
        get
        {
            if (panelSprite == null)
            {
                panelSprite = BuildRoundedSprite("Pause Menu Panel", PanelSize, PanelRadius);
            }

            return panelSprite;
        }
    }

    public static Sprite Frame
    {
        get
        {
            if (frameSprite == null)
            {
                frameSprite = BuildRoundedFrame("Pause Menu Frame", PanelSize, PanelRadius, PanelFrameWidth);
            }

            return frameSprite;
        }
    }

    public static Sprite Plate
    {
        get
        {
            if (plateSprite == null)
            {
                plateSprite = BuildRoundedSprite("Pause Menu Plate", PlateSize, PlateRadius);
            }

            return plateSprite;
        }
    }

    public static Sprite PlateFrame
    {
        get
        {
            if (plateFrameSprite == null)
            {
                plateFrameSprite = BuildRoundedFrame("Pause Menu Plate Frame", PlateSize, PlateRadius, PlateFrameWidth);
            }

            return plateFrameSprite;
        }
    }

    /// <summary>A vertical white ramp; tinting it dims the scene darker towards the bottom.</summary>
    public static Sprite Dim
    {
        get
        {
            if (dimSprite == null)
            {
                dimSprite = BuildAlphaRamp("Pause Menu Dim", 8, 128, 0.62f, 0.82f);
            }

            return dimSprite;
        }
    }

    /// <summary>Two soft bars for the corner pause button.</summary>
    public static Sprite PauseIcon
    {
        get
        {
            if (pauseIconSprite == null)
            {
                pauseIconSprite = BuildPauseIcon("Pause Menu Icon", 64);
            }

            return pauseIconSprite;
        }
    }

    static Sprite BuildRoundedSprite(string name, int size, float radius)
    {
        Texture2D texture = CreateTexture(name, size, size);

        float half = size * 0.5f;
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - half;
                float py = y + 0.5f - half;

                pixels[y * size + x] = White(Coverage(RoundedBoxDistance(px, py, 0f, 0f, half, half, radius)));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        float border = Mathf.Min(radius + 2f, size * 0.5f - 1f);
        return BuildSprite(texture, border, border, border, border);
    }

    static Sprite BuildRoundedFrame(string name, int size, float radius, float width)
    {
        Texture2D texture = CreateTexture(name, size, size);

        float half = size * 0.5f;
        float insetHalf = half - width;
        float insetRadius = Mathf.Max(0f, radius - width);

        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - half;
                float py = y + 0.5f - half;

                float coverage = Coverage(RoundedBoxDistance(px, py, 0f, 0f, half, half, radius))
                                 - Coverage(RoundedBoxDistance(px, py, 0f, 0f, insetHalf, insetHalf, insetRadius));

                pixels[y * size + x] = White(coverage);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        float border = Mathf.Min(radius + width + 2f, size * 0.5f - 1f);
        return BuildSprite(texture, border, border, border, border);
    }

    static Sprite BuildAlphaRamp(string name, int width, int height, float alphaAtTop, float alphaAtBottom)
    {
        Texture2D texture = CreateTexture(name, width, height);

        Color32[] pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            float t = y / (height - 1f);
            float alpha = Mathf.Lerp(alphaAtBottom, alphaAtTop, Mathf.SmoothStep(0f, 1f, t));

            for (int x = 0; x < width; x++)
            {
                pixels[y * width + x] = White(alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        return BuildSprite(texture, 0f, 0f, 0f, 0f);
    }

    static Sprite BuildPauseIcon(string name, int size)
    {
        Texture2D texture = CreateTexture(name, size, size);

        float half = size * 0.5f;
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - half;
                float py = y + 0.5f - half;

                float left = RoundedBoxDistance(px, py, -7f, 0f, 3.2f, 13f, 3.2f);
                float right = RoundedBoxDistance(px, py, 7f, 0f, 3.2f, 13f, 3.2f);

                pixels[y * size + x] = White(Coverage(Mathf.Min(left, right)));
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

    /// <summary>Signed distance to a rounded box, negative inside.</summary>
    static float RoundedBoxDistance(float px, float py, float centerX, float centerY, float halfWidth, float halfHeight, float radius)
    {
        float dx = Mathf.Abs(px - centerX) - (halfWidth - radius);
        float dy = Mathf.Abs(py - centerY) - (halfHeight - radius);

        float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
        float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);

        return outside + inside - radius;
    }

    /// <summary>Turns the distance into antialiased coverage one texel wide.</summary>
    static float Coverage(float distance)
    {
        return Mathf.Clamp01(0.5f - distance);
    }
}
