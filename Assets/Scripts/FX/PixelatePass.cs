using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Experimental.Rendering;

namespace Bloodrush.FX
{
[System.Serializable]
public class PixelatePass : CustomPass
{
    public int   pixelSize      = 4;
    public int   colorLevels    = 8;
    public float ditherStrength = 0.35f;  // PS1 tarzı bant kırıcı — düşük tut, desen görünmesin

    Material  mat;
    RTHandle  tempRT;

    static readonly int PSize   = Shader.PropertyToID("_PixelSize");
    static readonly int PLevels = Shader.PropertyToID("_ColorLevels");
    static readonly int PDither = Shader.PropertyToID("_DitherStrength");

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        var shader = Shader.Find("Hidden/Pixelate");
        if (shader == null) { Debug.LogError("Hidden/Pixelate shader bulunamadı!"); return; }
        mat = new Material(shader);

        tempRT = RTHandles.Alloc(
            Vector2.one,
            TextureXR.slices,
            dimension: TextureXR.dimension,
            colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
            useDynamicScale: true,
            name: "PixelateTempRT");
    }

    protected override void Execute(CustomPassContext ctx)
    {
        if (mat == null || tempRT == null) return;
        mat.SetFloat(PSize,   pixelSize);
        mat.SetFloat(PLevels, colorLevels);
        mat.SetFloat(PDither, ditherStrength);

        // Camera → temp (pixelate + posterize), temp → camera (kopyala)
        HDUtils.BlitCameraTexture(ctx.cmd, ctx.cameraColorBuffer, tempRT, mat, 0);
        HDUtils.BlitCameraTexture(ctx.cmd, tempRT, ctx.cameraColorBuffer);
    }

    protected override void Cleanup()
    {
        CoreUtils.Destroy(mat);
        tempRT?.Release();
    }
}
}
