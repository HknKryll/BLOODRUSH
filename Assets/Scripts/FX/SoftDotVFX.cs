using UnityEngine;
using UnityEngine.Rendering;

namespace Bloodrush.FX
{
// Çalışma zamanında yumuşak kenarlı (radyal alfa gradyanlı) bir daire dokusu
// üretir — kan/ölüm gibi parçacık efektleri için. Dışarıdan sprite/asset
// aranmaz; ilk çağrıda üretilip önbelleğe alınır.
public static class SoftDotVFX
{
    static Texture2D texture;

    public static Texture2D Texture()
    {
        if (texture != null) return texture;

        const int size = 64;
        texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name     = "SoftDot_Generated";
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center  = new Vector2(size * 0.5f, size * 0.5f);
        float   maxDist = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = Mathf.Clamp01(1f - dist / maxDist);
                a *= a;   // yumuşak düşüş — merkez opak, kenarlar saydam
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        texture.Apply();
        return texture;
    }

    // Yumuşak dokulu, saydam Unlit materyal üretir (HDRP/Unlit; yoksa Sprites/Default'a düşer).
    public static Material CreateMaterial(Color tint)
    {
        var shader = Shader.Find("HDRP/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var mat = new Material(shader) { name = "SoftDot_Generated_Mat" };

        if (mat.HasProperty("_UnlitColorMap")) mat.SetTexture("_UnlitColorMap", Texture());
        else if (mat.HasProperty("_MainTex"))   mat.SetTexture("_MainTex", Texture());

        if (mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor", tint);
        else if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);

        if (mat.HasProperty("_SurfaceType"))
        {
            mat.SetFloat("_SurfaceType", 1f);   // Transparent
            mat.SetFloat("_BlendMode", 0f);     // Alpha
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_AlphaDstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_CullMode", (float)CullMode.Off);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ALPHA");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        return mat;
    }
}
}
