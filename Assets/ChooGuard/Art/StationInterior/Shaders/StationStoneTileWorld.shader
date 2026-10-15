// OFFLINE CANDIDATE. Photographic appearance fit; 0.6 m tile pitch is not measured.
// URP 17.3.0 package APIs checked locally. Unity shader compilation is still required.
Shader "ChooGuard/StationStoneTileWorld"
{
    Properties
    {
        [MainColor] _StoneTint("Stone tint", Color) = (0.46,0.47,0.45,1)
        _GroutTint("Grout tint", Color) = (0.24,0.25,0.23,1)
        _TileSize("Tile pitch, metres (provisional appearance fit)", Float) = 0.6
        _GroutWidth("Total grout width, metres (appearance fit)", Float) = 0.003
        _TileVariation("Tile-to-tile variation", Range(0,0.2)) = 0.045
        _GrainContrast("Granite grain contrast", Range(0,0.4)) = 0.12
        _GrainScale("Fine grain cells per metre", Float) = 700
        _Smoothness("Stone smoothness", Range(0,1)) = 0.69
        _SmoothnessVariation("Smoothness variation", Range(0,0.2)) = 0.055
        _GroutSmoothness("Grout smoothness", Range(0,1)) = 0.18
        _AxisU("Hall U axis, world direction", Vector) = (1,0,0,0)
        _AxisV("Hall V axis, world direction", Vector) = (0,0,1,0)
        _OriginWS("Shared hall origin, world metres", Vector) = (0,0,0,0)
        _OffsetMetres("U/V alignment offset, metres", Vector) = (0,0,0,0)
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
            float4 _StoneTint, _GroutTint;
            float4 _AxisU, _AxisV, _OriginWS, _OffsetMetres;
            float _TileSize, _GroutWidth, _TileVariation, _GrainContrast;
            float _GrainScale, _Smoothness, _SmoothnessVariation, _GroutSmoothness;
            float _Cull;
        CBUFFER_END

        struct StoneAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
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

        float StoneHash(float2 p)
        {
            // No texture or downloaded image. Stable deterministic world-space variation.
            float3 q = frac(float3(p.xyx) * 0.1031);
            q += dot(q, q.yzx + 33.33);
            return frac((q.x + q.y) * q.z);
        }
        float StoneValueNoise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            float2 w = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(StoneHash(i), StoneHash(i + float2(1,0)), w.x),
                        lerp(StoneHash(i + float2(0,1)), StoneHash(i + float2(1,1)), w.x), w.y);
        }
        float StoneFilteredNoise(float2 p)
        {
            float footprint = max(length(ddx(p)), length(ddy(p)));
            float detail = 1.0 - smoothstep(0.35, 1.25, footprint);
            return lerp(0.5, StoneValueNoise(p), detail);
        }
        float2 StoneWorldCoordinates(float3 positionWS)
        {
            // Orthonormalize so affine source-plan skew cannot turn square tiles into diamonds.
            float3 u = _AxisU.xyz;
            u = dot(u,u) > 0.000001 ? normalize(u) : float3(1,0,0);
            float3 v = _AxisV.xyz - u * dot(_AxisV.xyz, u);
            float3 fallback = abs(u.y) < 0.95 ? float3(0,1,0) : float3(0,0,1);
            v = dot(v,v) > 0.000001 ? normalize(v) : normalize(cross(u, fallback));
            float3 p = positionWS - _OriginWS.xyz;
            return float2(dot(p,u), dot(p,v)) + _OffsetMetres.xy;
        }
        void StoneSurface(float3 positionWS, out SurfaceData surface)
        {
            float2 q = StoneWorldCoordinates(positionWS);
            float pitch = max(_TileSize, 0.05);
            float groutWidth = clamp(_GroutWidth, 0.0001, pitch * 0.1);
            float2 cell = q / pitch;
            float2 f = frac(cell);
            float2 edge = min(f, 1.0 - f) * pitch;
            float distanceToSeam = min(edge.x, edge.y);
            float aa = max(max(length(ddx(q)), length(ddy(q))), 0.00005);
            float grout = 1.0 - smoothstep(groutWidth * 0.5 - aa,
                                          groutWidth * 0.5 + aa, distanceToSeam);
            // Fade distant/subpixel grids to their true area coverage, avoiding moire.
            float fraction = groutWidth / pitch;
            float coverage = 1.0 - (1.0 - fraction) * (1.0 - fraction);
            float distant = smoothstep(pitch * 0.15, pitch * 0.6, aa);
            grout = lerp(grout, coverage, distant);
            float tile = StoneHash(floor(cell));
            tile = lerp(tile, 0.5, distant);
            float grain = StoneFilteredNoise(q * max(_GrainScale,1.0));
            float mineral = StoneFilteredNoise(q * max(_GrainScale,1.0) * 0.27 + float2(19.7,7.1));
            float stoneTone = 1.0 + (tile - 0.5) * _TileVariation * 2.0
                                  + (grain - 0.5) * _GrainContrast * 2.0
                                  + (mineral - 0.5) * _GrainContrast * 0.45;
            float roughnessField = StoneFilteredNoise(q * 9.0 + float2(3.9,8.4));
            float stoneSmoothness = saturate(_Smoothness + (roughnessField - 0.5) * _SmoothnessVariation * 2.0);
            surface = (SurfaceData)0;
            surface.albedo = saturate(lerp(_StoneTint.rgb * stoneTone, _GroutTint.rgb, grout));
            surface.metallic = 0.0h; // Granite is a dielectric, even when polished.
            surface.specular = half3(0,0,0); // Metallic workflow obtains dielectric F0 from URP.
            surface.smoothness = lerp(stoneSmoothness, _GroutSmoothness, grout);
            surface.normalTS = half3(0,0,1); // No invented geometry / displacement / micro-normal depth.
            surface.occlusion = 1.0h; // Scene lighting/SSAO supplies actual occlusion.
            surface.emission = half3(0,0,0);
            surface.alpha = 1.0h;
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
            StoneSurface(input.positionWS, surface);
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
                float3 positionWS : TEXCOORD0;
            };
            StoneMetaVaryings StoneMetaVertex(StoneAttributes input)
            {
                StoneMetaVaryings output = (StoneMetaVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = MetaVertexPosition(input.positionOS, input.staticLightmapUV, input.dynamicLightmapUV,
                                                       unity_LightmapST, unity_DynamicLightmapST);
                return output;
            }
            half4 StoneMetaFragment(StoneMetaVaryings input) : SV_Target
            {
                SurfaceData surface;
                StoneSurface(input.positionWS, surface);
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
