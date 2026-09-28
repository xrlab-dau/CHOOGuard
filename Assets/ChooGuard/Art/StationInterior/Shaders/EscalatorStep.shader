// EscalatorStep.shader
// -----------------------------------------------------------------------------
// 출처 / Attribution
//   아이디어 출처: AdultLink/TexturePanner (https://github.com/AdultLink/TexturePanner)
//                 라이선스 MIT, 원본은 Built-in RP surface shader (UV 패닝).
//   본 파일은 원본 코드를 복사한 것이 아니라, "UV 를 시간에 따라 스크롤한다"는
//   아이디어만 가져와 Unity 6000.3 / URP 17.3.0 용으로 새로 수기 작성한
//   ShaderLab + HLSL 구현이다. (Shader Graph 아님)
//   차이점: URP Forward 패스 + UniversalFragmentPBR 사용, SRP Batcher 호환
//           CBUFFER 구성, 오브젝트 스케일과 무관한 "월드 미터 기준" 스크롤.
//
// 용도: 부산역 역사 에스컬레이터 디딤판(서브메시 2 : 타일#025) / 핸드레일
//       (서브메시 1) 처럼, 메시는 정지해 있고 표면만 진행 방향으로 흐르는 표현.
//       진행 방향은 오브젝트 로컬 +Z 이며 UV 의 V 축에 매핑되어 있다고 가정한다.
// -----------------------------------------------------------------------------
Shader "CHOOGuard/Escalator/Step"
{
    Properties
    {
        [MainTexture] _BaseMap       ("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor     ("Base Color", Color) = (1,1,1,1)
        [Normal]      _BumpMap       ("Normal Map", 2D) = "bump" {}
                      _BumpScale     ("Normal Scale", Float) = 1.0
                      _ScrollSpeed   ("Scroll Speed (m/s)", Float) = 0.5
                      _TexMetres     ("Texture Repeat Length (m)", Float) = 0.4
                      _Direction     ("Direction (+1 Up / -1 Down)", Float) = 1.0
                      _Smoothness    ("Smoothness", Range(0,1)) = 0.35
                      _Metallic      ("Metallic", Range(0,1)) = 0.8
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Opaque"
            "RenderPipeline"  = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "Queue"           = "Geometry"
        }
        LOD 300

        // ------------------------------------------------------------------
        // 공용 CBUFFER : SRP Batcher 호환을 위해 모든 머티리얼 속성이
        // 반드시 UnityPerMaterial 안에, 모든 패스에서 동일 순서로 선언된다.
        // ------------------------------------------------------------------
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BumpMap_ST;
            half4  _BaseColor;
            half   _BumpScale;
            float  _ScrollSpeed;
            float  _TexMetres;
            float  _Direction;
            half   _Smoothness;
            half   _Metallic;
        CBUFFER_END

        // 오브젝트 로컬 +Z 축의 월드 길이 = 진행 방향 스케일.
        float EscalatorScaleZ()
        {
            return length(float3(UNITY_MATRIX_M._m02, UNITY_MATRIX_M._m12, UNITY_MATRIX_M._m22));
        }

        // 스케일 무관 월드-미터 기준 UV.
        // v = uv.y * (오브젝트 Z 길이 / 텍스처 1회 반복 길이)  ... 타일링
        //   + _Time.y * (속도 / 반복 길이) * 방향                ... 스크롤
        float2 EscalatorUV(float2 uv)
        {
            float metres = max(_TexMetres, 1e-4);
            float2 st    = uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
            st.y = st.y * (EscalatorScaleZ() / metres)
                 + _Time.y * (_ScrollSpeed / metres) * _Direction;
            return st;
        }
        ENDHLSL

        // ==================================================================
        // Universal Forward
        // ==================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   EscalatorVert
            #pragma fragment EscalatorFrag

            // URP 라이팅 키워드
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            // Forward+/Deferred+ 의 클러스터 조명·반사 프로브 아틀라스(URP 6.1+ 이름. 옛 _FORWARD_PLUS 는 파이프라인이 켜지 않는다).
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                half3  normalWS    : TEXCOORD2;
                half4  tangentWS   : TEXCOORD3;   // w = sign
                half4  fogFactorAndVertexLight : TEXCOORD4;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 5);
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings EscalatorVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS   = nrmInputs.normalWS;
                output.tangentWS  = half4(nrmInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());

                // 스크롤 UV 는 정점에서 계산해도 선형 보간이 유지된다.
                output.uv = EscalatorUV(input.uv);

                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                #if !defined(LIGHTMAP_ON)
                    // URP 17: OUTPUT_SH4 는 probeOcclusion 인자를 추가로 요구한다.
                    // 정점 SH 는 SampleSHVertex 로 직접 구한다 (매크로 시그니처 변동에 영향받지 않음).
                    output.vertexSH = SampleSHVertex(output.normalWS.xyz);
                #endif

                half3 vertexLight = VertexLighting(posInputs.positionWS, nrmInputs.normalWS);
                half  fogFactor   = ComputeFogFactor(posInputs.positionCS.z);
                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
                return output;
            }

            half4 EscalatorFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo      = albedo.rgb;
                surface.alpha       = albedo.a;
                surface.metallic    = _Metallic;
                surface.smoothness  = _Smoothness;
                surface.normalTS    = normalTS;
                surface.occlusion   = 1.0h;
                surface.emission    = 0.0h;
                surface.specular    = 0.0h;
                surface.clearCoatMask       = 0.0h;
                surface.clearCoatSmoothness = 0.0h;

                InputData inputData = (InputData)0;
                inputData.positionWS      = input.positionWS;
                inputData.positionCS      = input.positionCS;
                half3 viewDirWS           = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half  sgn                 = input.tangentWS.w;
                half3 bitangent           = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                half3x3 tangentToWorld    = half3x3(input.tangentWS.xyz, bitangent, input.normalWS.xyz);
                inputData.tangentToWorld  = tangentToWorld;
                inputData.normalWS        = TransformTangentToWorld(normalTS, tangentToWorld);
                inputData.normalWS        = NormalizeNormalPerPixel(inputData.normalWS);
                inputData.viewDirectionWS = viewDirWS;
                inputData.shadowCoord     = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord        = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
                inputData.vertexLighting  = input.fogFactorAndVertexLight.yzw;
                inputData.bakedGI         = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask      = SAMPLE_SHADOWMASK(input.lightmapUV);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a   = 1.0h;
                return color;
            }
            ENDHLSL
        }

        // ==================================================================
        // ShadowCaster
        // ==================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ==================================================================
        // DepthOnly
        // ==================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ==================================================================
        // DepthNormals (SSAO / Decal 대응)
        // ==================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            struct DNAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DNVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half4  tangentWS  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DNVaryings DepthNormalsVert(DNAttributes input)
            {
                DNVaryings output = (DNVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                VertexNormalInputs nrmInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS  = nrmInputs.normalWS;
                output.tangentWS = half4(nrmInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv        = EscalatorUV(input.uv);
                return output;
            }

            half4 DepthNormalsFrag(DNVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half  sgn      = input.tangentWS.w;
                half3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent, input.normalWS.xyz);
                half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));
                return half4(NormalizeNormalPerPixel(normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
