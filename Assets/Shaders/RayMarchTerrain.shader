Shader "Bliss/RayMarchTerrain"
{
    Properties
    {
        _RayMarchMaxDistance("RayMarchDistance", float) = 1000.0
        _RayMarchStep("RayMarchStep", float) = 0.5
        _HighlightColor("HighlightColor", Color) = (1, 1, 1, 1)
        _ShadowColor("ShadowColor", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100

        ZWrite On Cull Off
        
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            //#pragma enable_d3d11_debug_symbols 
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #include "includes/Terrain.hlsl"
            #include "includes/LightingHelp.hlsl"

            #pragma vertex vert
            #pragma fragment frag
            
            struct Attributes
            {
                float3 position : POSITION;
                float2 uv: TEXCOORD0;
            };
            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            float _RayMarchMaxDistance;
            float _RayMarchStep;
            float4 _HighlightColor;
            float4 _ShadowColor;

            v2f vert(Attributes input)
            {
                v2f o;
                o.position = float4(input.position, 1.0);
                o.uv = input.uv;
                return o;
            }


            half4 frag(v2f i, out float depth : SV_Depth) : SV_Target
            {
                float2 uv = i.position.xy / _ScreenParams.xy;
                uv = (-1.0 + 2.0 * uv);
                uv.x *= _ScreenParams.x / _ScreenParams.y;

                float3 target = mul(unity_CameraInvProjection, float4(uv, 0.0, -1.0));
                target = mul(unity_CameraToWorld, float4(target, 1.0));

                float3 dir = normalize(target - _WorldSpaceCameraPos);
                float3 pos = _WorldSpaceCameraPos;
                float T;
                //if (Scene(pos, dir, T))
                if (RayMarchTerrain(pos, dir, _RayMarchStep, _RayMarchMaxDistance))
                {
                    float T = length(pos - _WorldSpaceCameraPos);
                    depth = (1.0 / T - _ZBufferParams.w) / _ZBufferParams.z;
                    float3 n = GetNormal(pos.xz);

                    float3 light, lightDir;
                    float distanceAtten, shadowAtten;
                    GetMainLight_float(pos, light, lightDir, distanceAtten, shadowAtten);

                    float intensity = min(max(0, dot(n, lightDir)) * /*distanceAtten * */shadowAtten, 1.0);

                    float3 color = lerp(_HighlightColor, _ShadowColor, 1.0-intensity);

                    ApplyFog(color, T);

                    return half4(color, 1.0);
                    //return half4(intensity, intensity, intensity, 1);
                    //return half4(distanceAtten, distanceAtten, distanceAtten, 1);
                    //return half4(n * 0.5 + 0.5, 1.0);
                }
                depth = 0.0;
                return _ShadowColor;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "DepthOnly" }
            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "includes/Terrain.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float3 position : POSITION;
                float2 uv: TEXCOORD0;
            };
            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(Attributes input)
            {
                v2f o;
                o.position = float4(input.position, 1.0);
                o.uv = input.uv;
                return o;
            }
            float _RayMarchMaxDistance;
            float _RayMarchStep;


            half4 frag(v2f i, out float depth : SV_Depth) : SV_Target
            {
                float2 uv = i.position.xy / _ScreenParams.xy;
                uv = (-1.0 + 2.0 * uv);
                uv.x *= _ScreenParams.x / _ScreenParams.y;

                float3 target = mul(unity_CameraInvProjection, float4(uv, 0.0, -1.0));
                target = mul(unity_CameraToWorld, float4(target, 1.0));

                float3 dir = normalize(target - _WorldSpaceCameraPos);
                float3 pos = _WorldSpaceCameraPos;
                float T;
                //if (Scene(pos, dir, T))
                if (RayMarchTerrain(pos, dir, _RayMarchStep, _RayMarchMaxDistance))
                {
                    float T = length(pos - _WorldSpaceCameraPos);
                    depth = (1.0 / T - _ZBufferParams.w) / _ZBufferParams.z;
                    return 0;
                }
                depth = 0.0;
                return 0;
            }
            ENDHLSL
        }
    }
}