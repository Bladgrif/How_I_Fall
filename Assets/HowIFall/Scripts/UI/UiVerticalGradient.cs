using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("How I Fall/UI/Vertical Gradient")]
[RequireComponent(typeof(Graphic))]
public sealed class UiVerticalGradient : BaseMeshEffect
{
    public Color topColor = Color.white;
    public Color bottomColor = Color.white;

    public override void ModifyMesh(VertexHelper vertexHelper)
    {
        if (!IsActive() || vertexHelper.currentVertCount == 0)
        {
            return;
        }

        Rect rect = graphic.rectTransform.rect;
        float height = Mathf.Max(0.0001f, rect.height);
        var vertex = new UIVertex();

        for (int i = 0; i < vertexHelper.currentVertCount; i++)
        {
            vertexHelper.PopulateUIVertex(ref vertex, i);
            float t = Mathf.Clamp01((vertex.position.y - rect.yMin) / height);
            vertex.color = Multiply(vertex.color, Color.Lerp(bottomColor, topColor, t));
            vertexHelper.SetUIVertex(vertex, i);
        }
    }

    private static Color32 Multiply(Color32 source, Color tint)
    {
        return new Color32(
            (byte)Mathf.RoundToInt(source.r * tint.r),
            (byte)Mathf.RoundToInt(source.g * tint.g),
            (byte)Mathf.RoundToInt(source.b * tint.b),
            (byte)Mathf.RoundToInt(source.a * tint.a));
    }
}
