// Scoped derivative of current project URP PBR passes. Actual source colors are physical mesh vertex colors; no image, font, atlas or text rendering.
Shader "ChooGuard/R29PhysicalSourceVertexInk"
{
    Properties
    {
        [MainColor] _BaseColor("Actual physical ink tint", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.3
        _Metallic("Metallic", Range(0,1)) = 0
        [HDR] _EmissionColor("Existing source emission", Color) = (0,0,0,1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" }
        LOD 300
        ZWrite On
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

        // Same material CBUFFER in every pass: SRP Batcher compatible.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _EmissionColor;
            float _Smoothness, _Metallic, _Cull;
        CBUFFER_END

        struct StoneAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 physicalInk : COLOR;
            float2 staticLightmapUV : TEXCOORD1;
            float2 dynamicLightmapUV : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct StoneVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float4 shadowCoord : TEXCOORD2;
            half4 fogAndVertexLight : TEXCOORD3;
            half4 physicalInk : COLOR;
            DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 4);
            #if defined(DYNAMICLIGHTMAP_ON)
                float2 dynamicLightmapUV : TEXCOORD5;
            #endif
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        struct StoneSimpleVaryings
        {
            float4 positionCS : SV_POSITION;
            half3 normalWS : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        void StoneSurface(half4 ink, out SurfaceData surface)
        {
            surface=(SurfaceData)0;
            #ifndef UNITY_COLORSPACE_GAMMA
                ink.rgb=SRGBToLinear(ink.rgb);
            #endif
            surface.albedo=ink.rgb*_BaseColor.rgb;
            surface.metallic=_Metallic;
            surface.smoothness=_Smoothness;
            surface.normalTS=half3(0,0,1);
            surface.occlusion=1;
            surface.emission=ink.rgb*_EmissionColor.rgb;
            surface.alpha=1;
        }
        StoneVaryings StoneVertex(StoneAttributes input)
        {
            StoneVaryings output = (StoneVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = p.positionCS;
            output.positionWS = p.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.shadowCoord = GetShadowCoord(p);
            output.physicalInk=input.physicalInk;
            output.fogAndVertexLight = half4(ComputeFogFactor(p.positionCS.z), VertexLighting(p.positionWS, output.normalWS));
            OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
            OUTPUT_SH(output.normalWS, output.vertexSH);
            #if defined(DYNAMICLIGHTMAP_ON)
                output.dynamicLightmapUV = input.dynamicLightmapUV * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
            #endif
            return output;
        }
        half4 StoneFragment(StoneVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            SurfaceData surface;
            StoneSurface(input.physicalInk, surface);
            InputData lighting = (InputData)0;
            lighting.positionWS = input.positionWS;
            lighting.positionCS = input.positionCS;
            lighting.normalWS = NormalizeNormalPerPixel(input.normalWS);
            lighting.normalWS *= IS_FRONT_VFACE(face, 1.0h, -1.0h);
            lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            lighting.shadowCoord = input.shadowCoord;
            lighting.fogCoord = InitializeInputDataFog(float4(input.positionWS,1.0), input.fogAndVertexLight.x);
            lighting.vertexLighting = input.fogAndVertexLight.yzw;
            lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            #if defined(DYNAMICLIGHTMAP_ON)
                lighting.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, lighting.normalWS);
            #else
                lighting.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, lighting.normalWS);
            #endif
            lighting.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
            half4 color = UniversalFragmentPBR(lighting, surface);
            color.rgb = MixFog(color.rgb, lighting.fogCoord);
            color.a = 1.0h;
            return color;
        }
        StoneSimpleVaryings StoneDepthVertex(StoneAttributes input)
        {
            StoneSimpleVaryings output = (StoneSimpleVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            return output;
        }
        half4 StoneDepthFragment(StoneSimpleVaryings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return half4(input.positionCS.zzz, 1.0h);
        }
        half4 StoneDepthNormalsFragment(StoneSimpleVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float3 n = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(n);
                return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
            #else
                return half4(n, 0);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex StoneVertex
            #pragma fragment StoneFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex StoneShadowVertex
            #pragma fragment StoneDepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            StoneSimpleVaryings StoneShadowVertex(StoneAttributes input)
            {
                StoneSimpleVaryings output = StoneDepthVertex(input);
                float3 p = TransformObjectToWorld(input.positionOS.xyz);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirection = normalize(_LightPosition - p);
                #else
                    float3 lightDirection = _LightDirection;
                #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(p, output.normalWS, lightDirection)));
                return output;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex StoneDepthVertex
            #pragma fragment StoneDepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex StoneDepthVertex
            #pragma fragment StoneDepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex StoneMetaVertex
            #pragma fragment StoneMetaFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            struct StoneMetaVaryings
            {
                float4 positionCS : SV_POSITION;
                half4 physicalInk : COLOR;
                float3 positionWS : TEXCOORD0;
            };
            StoneMetaVaryings StoneMetaVertex(StoneAttributes input)
            {
                StoneMetaVaryings output = (StoneMetaVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.physicalInk=input.physicalInk;
                output.positionCS = MetaVertexPosition(input.positionOS, input.staticLightmapUV, input.dynamicLightmapUV,
                                                       unity_LightmapST, unity_DynamicLightmapST);
                return output;
            }
            half4 StoneMetaFragment(StoneMetaVaryings input) : SV_Target
            {
                SurfaceData surface;
                StoneSurface(input.physicalInk, surface);
                MetaInput meta = (MetaInput)0;
                meta.Albedo = surface.albedo;
                meta.Emission = 0;
                return MetaFragment(meta);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
