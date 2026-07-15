Shader "Hidden/Pixelate"
{
    HLSLINCLUDE
    #pragma editor_sync_compilation
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float _PixelSize;
    float _ColorLevels;
    float _DitherStrength;

    // 4x4 Bayer matrisi — gerçek PS1'in bantlaşmayı maskeleme yöntemi
    static const float bayer4[16] = {
         0.0,  8.0,  2.0, 10.0,
        12.0,  4.0, 14.0,  6.0,
         3.0, 11.0,  1.0,  9.0,
        15.0,  7.0, 13.0,  5.0
    };

    float4 Frag(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);
        float depth = LoadCameraDepth(varyings.positionCS.xy);
        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw,
                                    depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

        float2 snappedUV = (floor(posInput.positionNDC.xy * _ScreenSize.xy / _PixelSize)
                            * _PixelSize + 0.5) / _ScreenSize.xy;

        float4 color = float4(CustomPassSampleCameraColor(snappedUV, 0), 1.0);

        // Sanal piksel koordinatına göre dither ofseti (posterize bantlarını kırar)
        uint2 vp  = uint2(varyings.positionCS.xy / _PixelSize);
        float d   = (bayer4[(vp.y & 3u) * 4u + (vp.x & 3u)] / 16.0 - 0.5) * _DitherStrength;

        color.rgb = floor(color.rgb * _ColorLevels + 0.5 + d) / _ColorLevels;
        return color;
    }
    ENDHLSL

    SubShader
    {
        Pass
        {
            Name "Custom Pass 0"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
    Fallback Off
}
