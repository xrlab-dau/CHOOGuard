#ifndef CHOOGUARD_STATION_LIT_PASSES_INCLUDED
#define CHOOGUARD_STATION_LIT_PASSES_INCLUDED

// Wrappers around the URP 17.3 Lit passes for the official station model:
// 1. Its materials render both faces (Cull Off) on single-sided geometry. URP Lit keeps the front normal on the back
//    face, so every wall seen from behind was lit from the wrong side and rendered black. Back faces flip the normal
//    (and tangent, keeping the bitangent) before shading.
// 2. Most of its untextured meshes have no usable UVs. With _BOXMAP the vertex stage projects world metres onto the
//    plane of the dominant normal axis (the faces are flat with split vertices, so each face gets one projection) and
//    builds a matching tangent; _BaseMap_ST then sets metres per texture repeat.

void StationBoxMap(float3 positionOS, float3 normalOS, inout float4 tangentOS, inout float2 texcoord)
{
#if defined(_BOXMAP)
    float3 p = TransformObjectToWorld(positionOS);
    float3 n = TransformObjectToWorldNormal(normalOS);
    float3 a = abs(n);
    float3 t, b;
    if (a.y >= a.x && a.y >= a.z) { t = float3(1, 0, 0); b = float3(0, 0, 1); }
    else if (a.x >= a.z) { t = float3(0, 0, n.x >= 0 ? -1 : 1); b = float3(0, 1, 0); }
    else { t = float3(n.z >= 0 ? 1 : -1, 0, 0); b = float3(0, 1, 0); }
    texcoord = float2(dot(p, t), dot(p, b));
    float w = dot(cross(n, t), b) >= 0 ? 1 : -1;
    tangentOS = float4(TransformWorldToObjectDir(t), w * GetOddNegativeScale());
#endif
}

#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    #define STATION_FLIP_TANGENT(input) input.tangentWS.xyz = -input.tangentWS.xyz;
#else
    #define STATION_FLIP_TANGENT(input)
#endif
#define STATION_FLIP(input, face) if (!IS_FRONT_VFACE(face, true, false)) { input.normalWS = -input.normalWS; STATION_FLIP_TANGENT(input) }

#if defined(STATION_PASS_FORWARD)
Varyings StationLitPassVertex(Attributes input)
{
    StationBoxMap(input.positionOS.xyz, input.normalOS, input.tangentOS, input.texcoord);
    return LitPassVertex(input);
}

void StationLitPassFragment(
    Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    STATION_FLIP(input, face)
    LitPassFragment(input, outColor
#ifdef _WRITE_RENDERING_LAYERS
        , outRenderingLayers
#endif
    );
}
#endif

#if defined(STATION_PASS_GBUFFER)
Varyings StationLitGBufferPassVertex(Attributes input)
{
    StationBoxMap(input.positionOS.xyz, input.normalOS, input.tangentOS, input.texcoord);
    return LitGBufferPassVertex(input);
}

GBufferFragOutput StationLitGBufferPassFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC)
{
    STATION_FLIP(input, face)
    return LitGBufferPassFragment(input);
}
#endif

#if defined(STATION_PASS_DEPTHNORMALS)
Varyings StationDepthNormalsVertex(Attributes input)
{
    StationBoxMap(input.positionOS.xyz, input.normal, input.tangentOS, input.texcoord);
    return DepthNormalsVertex(input);
}

void StationDepthNormalsFragment(
    Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC
    , out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    STATION_FLIP(input, face)
    DepthNormalsFragment(input, outNormalWS
#ifdef _WRITE_RENDERING_LAYERS
        , outRenderingLayers
#endif
    );
}
#endif

#endif
