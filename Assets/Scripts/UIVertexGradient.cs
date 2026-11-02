using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Paints a UI graphic with a vertical two-colour gradient by tinting its vertices, so the ramp
/// stays perfectly smooth however the graphic is stretched and needs no extra texture or masking.
/// Alpha is left untouched, so the graphic keeps whatever transparency it already has.
/// </summary>
[RequireComponent(typeof(Graphic))]
public class UIVertexGradient : BaseMeshEffect
{
    [SerializeField] Color topColor = Color.white;
    [SerializeField] Color bottomColor = Color.black;

    /// <summary>Sets the ramp's two ends and rebuilds the mesh.</summary>
    public void SetColors(Color top, Color bottom)
    {
        topColor = top;
        bottomColor = bottom;

        if (graphic != null)
        {
            graphic.SetVerticesDirty();
        }
    }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || vertices.currentVertCount == 0)
        {
            return;
        }

        Rect rect = graphic.rectTransform.rect;
        float height = rect.height;

        if (height <= 0f)
        {
            return;
        }

        UIVertex vertex = new UIVertex();

        for (int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);

            float height01 = Mathf.Clamp01((vertex.position.y - rect.yMin) / height);
            Color tint = Color.Lerp(bottomColor, topColor, height01);

            // Only the hue is replaced; the alpha the graphic already painted is kept.
            vertex.color = new Color32(
                (byte)Mathf.RoundToInt(tint.r * 255f),
                (byte)Mathf.RoundToInt(tint.g * 255f),
                (byte)Mathf.RoundToInt(tint.b * 255f),
                vertex.color.a);

            vertices.SetUIVertex(vertex, i);
        }
    }
}
