Shader "ChooGuard/StationFire/CharResidue"
{
    Properties { _FireCenter("Fire centre",Vector)=(0,0,0,1) _CharStrength("Char",Float)=0.2 _Agent("Powder",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _FireCenter; float _CharStrength; float _Agent;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;};
            V vert(A i)
            {
                V o;o.world=TransformObjectToWorld(i.positionOS.xyz)+TransformObjectToWorldNormal(i.normalOS)*.002;
                o.positionCS=TransformWorldToHClip(o.world);return o;
            }
            half4 frag(V i):SV_Target
            {
                float mask=1-smoothstep(.22,.62,distance(i.world,_FireCenter.xyz));
                float grain=frac(sin(dot(i.world.xz,float2(91.7,37.3)))*43758.5);
                float powder=saturate(_Agent*8)*smoothstep(.28,.8,grain);
                return half4(lerp(float3(.035,.022,.013),float3(.8,.77,.67),powder),mask*saturate(max(_CharStrength,powder)));
            }
            ENDHLSL
        }
    }
}
