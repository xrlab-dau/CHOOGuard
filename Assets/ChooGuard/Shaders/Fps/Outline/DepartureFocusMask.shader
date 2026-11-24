// Source UV/alpha/cull connection adapted from ChooGuard/KTX/OpaqueTexelDepth.
// RenderGraph foundation: Unity official per-object example, Apache-2.0 (see ThirdParty).
Shader "Hidden/ChooGuard/DepartureFocusMask"
{
    Properties
    {
        _BaseMap("Source",2D)="white"{}
        _Alpha("Source alpha",Float)=1
        _Cutoff("Coverage threshold",Float)=0.003921569
        _NeedsAlpha("Sample source alpha",Float)=0
        _BoxMap("Source station projection",Float)=0
        _SampleTextureAlpha("Source alpha channel",Float)=1
        _Cull("Source culling",Float)=2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float _Alpha, _Cutoff, _NeedsAlpha, _BoxMap, _SampleTextureAlpha;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                // Reused StationLitPasses::StationBoxMap projection. No tangent needed for coverage.
                float2 uv=input.uv;
                if (_BoxMap > .5)
                {
                    float3 p=TransformObjectToWorld(input.positionOS.xyz);
                    float3 n=TransformObjectToWorldNormal(input.normalOS);
                    float3 a=abs(n),t,b;
                    if(a.y>=a.x && a.y>=a.z){t=float3(1,0,0);b=float3(0,0,1);}
                    else if(a.x>=a.z){t=float3(0,0,n.x>=0?-1:1);b=float3(0,1,0);}
                    else {t=float3(n.z>=0?1:-1,0,0);b=float3(0,1,0);}
                    uv=float2(dot(p,t),dot(p,b));
                }
                output.uv=uv*_BaseMap_ST.xy+_BaseMap_ST.zw; return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float depth=SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS));
                // Single-sample mask reads URP's resolved depth. No mismatched MSAA attachment or depth bias.
                #if UNITY_REVERSED_Z
                clip(input.positionCS.z-depth);
                #else
                clip(depth-input.positionCS.z);
                #endif
                if (_NeedsAlpha > .5) clip((_SampleTextureAlpha > .5 ? SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a : 1)*_Alpha-_Cutoff);
                return 1;
            }
            ENDHLSL
        }
    }
}
