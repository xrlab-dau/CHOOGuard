Shader "CHOOGuard/ReconstructionVertexColor"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 position : SV_POSITION; fixed4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(v2f, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position=UnityObjectToClipPos(input.vertex);
                output.color=input.color;
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                // glTF/FBX COLOR_0 is linear. Gamma projects need an explicit display conversion;
                // linear projects use the render target's normal linear-to-display conversion.
                fixed3 color=input.color.rgb;
                #if defined(UNITY_COLORSPACE_GAMMA)
                    color=LinearToGammaSpace(color);
                #endif
                return fixed4(color,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
