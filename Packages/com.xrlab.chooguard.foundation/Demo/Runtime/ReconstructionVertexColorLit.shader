Shader "CHOOGuard/ReconstructionVertexColorLit"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Back
        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows addshadow
        #pragma target 3.0
        struct Input { fixed4 vertexColor; };
        void vert(inout appdata_full vertex, out Input output)
        {
            UNITY_INITIALIZE_OUTPUT(Input,output);
            output.vertexColor=vertex.color;
        }
        void surf(Input input,inout SurfaceOutputStandard output)
        {
            fixed3 color=input.vertexColor.rgb;
            #if defined(UNITY_COLORSPACE_GAMMA)
                color=LinearToGammaSpace(color);
            #endif
            output.Albedo=color;
            output.Metallic=0;
            output.Smoothness=.25;
            output.Occlusion=1;
            output.Alpha=1;
        }
        ENDCG
    }
    Fallback Off
}
