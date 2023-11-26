Shader "Bliss/RayMarchTerrain"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _RayMarchMaxDistance("RayMarchDistance", float) = 1000.0
        _RayMarchStep("RayMarchStep", float) = 0.5
        _HighlightColor("HighlightColor", Color) = (1, 1, 1, 1)
        _ShadowColor("ShadowColor", Color) = (0, 0, 0, 1)
        _GrassColor("GrassColor", Color) = (0, 0.8, 0, 1)
        _GrassFadeDist("GrassFadeDist", float) = 40
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100

        ZWrite On Cull Off
        ZTest Less
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            //#pragma enable_d3d11_debug_symbols 
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _RAY_MARCH_SHADOW
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #include "includes/Terrain.hlsl"
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
            
            float _RayMarchMaxDistance;
            float _RayMarchStep;
            float4 _HighlightColor;
            float4 _ShadowColor;

            v2f vert(appdata input)
            {
                v2f o;
                o.position = float4(input.position, 1.0);
                o.uv = input.uv;
                return o;
            }

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _GrassColor;
            float _GrassFadeDist;

            half4 frag(v2f i, out float depth : SV_Depth) : SV_Target
            {
                float2 uv = i.position.xy / _ScreenParams.xy;
                uv = (-1.0 + 2.0 * uv);
                uv.x;

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

#if _RAY_MARCH_SHADOW
                    // don't need shadow from terrain because it's very flat
                    //float3 pos2 = pos; float foo;
                    //pos2.y += 0.01;
                    //if (RayMarchTerrain(pos2, lightDir, _RayMarchStep, 100))
                    //{
                    //    rayMarchShadowAtten = 0;
                    //}
                    float3 epos = pos; epos.y += 4.8;
                    shadowAtten *= smoothstep(-1, 0.6, CloudsShadow(epos, lightDir));
#endif
                    //return half4(shadowAtten, shadowAtten, shadowAtten, 1);

                    float intensity = saturate(max(0, dot(n, lightDir)) * /*distanceAtten * */shadowAtten);
                    float3 color = _HighlightColor * light;
                    color = lerp(color, _ShadowColor, 1.0-intensity);
                    float2 uv = pos.xz - frac(pos.xz / _MainTex_ST.xy);
                    color *= tex2D(_MainTex, uv);

                    float grassBlend = saturate(exp2(-T / _GrassFadeDist));
                    color = lerp(_GrassColor, color, grassBlend);

                    ApplyFog(color, T);

                    return half4(color, 1.0);
                    //return half4(intensity, intensity, intensity, 1);
                    //return half4(distanceAtten, distanceAtten, distanceAtten, 1);
                    //return half4(n * 0.5 + 0.5, 1.0);
                }

                depth = 0.0;
                return half4(0, 0, 0, 0);
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
                //float3 target = mul(unity_CameraInvProjection, float4(i.position.xy, 0.0, 1.0));
                float3 target = 0;
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