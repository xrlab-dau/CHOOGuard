Shader "ChooGuard/ConservativeSmoke"
{
    Properties
    {
        _FogColor ("Smoke color", Color) = (0.23,0.24,0.25,1)
        _CellDims ("Dimensions", Vector) = (1,1,1,0)
        _Layer ("Interface, rise, gradient", Vector) = (0.5,0,0,0)
        _Extinction ("Lower and upper per metre", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Front ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _FogColor, _CellDims, _Layer, _Extinction;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 position : SV_POSITION; float3 world : TEXCOORD0; float4 screen : TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o; o.position = UnityObjectToClipPos(v.vertex); o.world = mul(unity_ObjectToWorld,v.vertex).xyz;
                o.screen = ComputeScreenPos(o.position); return o;
            }
            // Restrict the physical-distance interval to value+slope*t >= 0.
            bool clipPlane(float value, float slope, inout float enter, inout float exit)
            {
                if (slope == 0) return value >= 0;
                float crossing = -value / slope;
                if (slope > 0) enter = max(enter,crossing); else exit = min(exit,crossing);
                return exit >= enter;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 ray = normalize(i.world - _WorldSpaceCameraPos);
                float3 origin = mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz * _CellDims.xyz;
                float3 direction = mul((float3x3)unity_WorldToObject,ray) * _CellDims.xyz;
                float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
                float eyeDirection = -mul(UNITY_MATRIX_V,float4(ray,0)).z;
                float enter = 0, exit = depth / max(eyeDirection,0.000001);
                float3 halfSize = _CellDims.xyz * .5;
                if ((direction.x == 0 && origin.x == halfSize.x) || (direction.z == 0 && origin.z == halfSize.z)) return 0;
                if (!clipPlane(origin.x+halfSize.x,direction.x,enter,exit) || !clipPlane(halfSize.x-origin.x,-direction.x,enter,exit) ||
                    !clipPlane(origin.z+halfSize.z,direction.z,enter,exit) || !clipPlane(halfSize.z-origin.z,-direction.z,enter,exit)) return 0;
                float floorAtOrigin = -halfSize.y + _Layer.y*.5 + _Layer.z*origin.x;
                float floorDirection = _Layer.z*direction.x;
                if (!clipPlane(origin.y-floorAtOrigin,direction.y-floorDirection,enter,exit) ||
                    !clipPlane(floorAtOrigin+_CellDims.y-_Layer.y-origin.y,floorDirection-direction.y,enter,exit)) return 0;
                float interfaceY = -halfSize.y + _Layer.x;
                float lowEnter = enter, lowExit = exit, highEnter = enter, highExit = exit;
                float lower = 0, upper = 0;
                if (direction.y == 0 && origin.y == interfaceY)
                { if (_Layer.x < _CellDims.y) upper = max(0,exit-enter); else lower = max(0,exit-enter); }
                else
                {
                    if (clipPlane(interfaceY-origin.y,-direction.y,lowEnter,lowExit)) lower = max(0,lowExit-lowEnter);
                    if (clipPlane(origin.y-interfaceY,direction.y,highEnter,highExit)) upper = max(0,highExit-highEnter);
                }
                float opticalDepth = lower*_Extinction.x + upper*_Extinction.y;
                return fixed4(_FogColor.rgb,1-exp(-max(0,opticalDepth)));
            }
            ENDCG
        }
    }
}
