// Adapted horizontal/vertical neighborhood dilation and alpha composite from Unity
// OutlineDilation.shader, 919617c847e41fc0f397220ccd4f32bb81b7210d, Apache-2.0.
// Copyright 2023 Unity Technologies. Full notices in ThirdParty.
// Modifications: bounded hard morphology; carry original center mask in green; no shared stencil.
Shader "Hidden/ChooGuard/DepartureFocusEdge"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        int _Spread;
        float4 _EdgeColor;
        ENDHLSL
        Pass
        {
            Name "Horizontal"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Horizontal
            float4 Horizontal(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float center=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,i.texcoord).r;
                float value=center;
                int spread=clamp(_Spread,1,3);
                for(int x=-spread;x<=spread;x++)
                    value=max(value,SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,i.texcoord+float2(_BlitTexture_TexelSize.x*x,0)).r);
                return float4(value,center,0,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "VerticalComposite"
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Vertical
            float4 Vertical(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float center=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,i.texcoord).g;
                float value=0;
                int spread=clamp(_Spread,1,3);
                for(int y=-spread;y<=spread;y++)
                    value=max(value,SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,i.texcoord+float2(0,_BlitTexture_TexelSize.y*y)).r);
                return float4(_EdgeColor.rgb,saturate(value-center)*_EdgeColor.a);
            }
            ENDHLSL
        }
    }
}
