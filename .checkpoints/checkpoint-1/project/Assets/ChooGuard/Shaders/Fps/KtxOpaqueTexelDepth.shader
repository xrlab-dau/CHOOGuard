Shader "ChooGuard/KTX/OpaqueTexelDepth"
{
    Properties
    {
        _BaseMap("Source RGBA",2D)="white"{}
        _BaseColor("Source exported multiplier",Color)=(1,1,1,1)
        _Cull("Source culling",Float)=0
        _OpaqueThreshold("Only fully opaque filtered texels",Float)=0.99999
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+499" }
        Pass
        {
            Name "OpaqueTexelDepth"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            ZTest LEqual
            Cull [_Cull]
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _OpaqueThreshold;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=TRANSFORM_TEX(input.uv,_BaseMap); return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float alpha=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a*_BaseColor.a;
                clip(alpha-_OpaqueThreshold); return 0;
            }
            ENDHLSL
        }
    }
}
