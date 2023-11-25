Shader "Bliss/RayMarchCloud"
{
    Properties
    {
        _Color("Color", Color) = (1, 1, 1, 1)
        _RayMarchMaxDistance("RayMarchDistance", float) = 10000.0
        _RayMarchStep("RayMarchStep", float) = 10
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100

        ZWrite Off Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        //Blend One OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            //#pragma enable_d3d11_debug_symbols 
            //#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            //#pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #include "includes/Cloud.hlsl"
            #include "includes/LightingHelp.hlsl"

            #pragma vertex vert
            #pragma fragment frag
            
            struct appdata
            {
                float3 position : POSITION;
                float2 uv: TEXCOORD0;
            };
            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f o;
                o.position = float4(input.position, 1.0);
                o.uv = input.uv;
                return o;
            }
            float _RayMarchMaxDistance;
            float _RayMarchStep;
            float4 _Color;

            void fog(inout float4 col, float t)
            {
                float3 ext = exp2(-t * 0.00025 * float3(1, 1.5, 4));
                //col.xyz = col.xyz * ext + (1.0 - ext) * float3(0.8431373, 0.8784314, 0.8745099); // 0.55
                col.w *= ext;
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 uv = i.position.xy / _ScreenParams.xy;
                uv = (-1.0 + 2.0 * uv);
                uv.x;

                float3 target = mul(unity_CameraInvProjection, float4(uv, 0.0, -1.0));
                target = mul(unity_CameraToWorld, float4(target, 1.0));

                float3 dir = normalize(target - _WorldSpaceCameraPos);
                float3 pos = _WorldSpaceCameraPos;
                float T = _RayMarchMaxDistance;
                float4 col = RayMarchClouds(_Color.xyz, GetMainLight().direction, pos, dir, _RayMarchStep, 0, _RayMarchMaxDistance, T);
                //ApplyFog(col.xyz, T);
                fog(col, T);
                return col;
            }
            ENDHLSL
        }
    }
}