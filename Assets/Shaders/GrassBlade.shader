Shader "Bliss/GrassBlade"
{
    Properties
    {
        _GrassTopColor("Top Color", Color) = (0.18, 0.6, 0.12, 1)
        _GrassCenterColor("Center Color", Color) = (0.14, 0.45, 0.091, 1.0)
        _GrassEdgeColor("Edge Color", Color) = (0.05, 0.18, 0.04, 1)
        _GrassShadowColor("Shadow Color", Color) = (0.8, 0.8, 0.8, 1)
    }
        SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        ZWrite On

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma enable_d3d11_debug_symbols 
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #include "includes/GrassRenderProperty.hlsl"
            #include "includes/LightingHelp.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            StructuredBuffer<GrassRenderProperty> _Properties;

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 world_pos : TEXCOORD1;
                float4 normal : TEXCOORD2;
                float4 color : TEXCOORD3;
                float4 vertex : SV_POSITION;
            };

            float4 _GrassTopColor;
            float4 _GrassCenterColor;
            float4 _GrassEdgeColor;
            float4 _GrassShadowColor;


            v2f vert(appdata v, uint instanceID: SV_InstanceID)
            {
                v2f o;
                //o.vertex = UnityObjectToClipPos(v.vertex);
                GrassRenderProperty prop = _Properties[instanceID];
                float3 v0 = prop.v0_world;
                v0 += prop.right * (v.uv.x - 0.5) * _GrassWidth;
                float3 v1 = v0;
                v1.y += _GrassHeight * prop.v1andv2_local.x;
                float3 v2 = prop.v0_world;
                v2.y += _GrassHeight * prop.v1andv2_local.z;
                v2.xz += _GrassWidth * prop.v1andv2_local.yw;
                // B(t) = (1−t)^2 ∗ v0 + 2 ∗ t ∗ (1−t) ∗ v1 + t^2 ∗ v2
                float oneMinusT = 1 - v.uv.y;
                o.world_pos = float4(oneMinusT * oneMinusT * v0 + 2 * v.uv.y * oneMinusT * v1 + v.uv.y * v.uv.y * v2, 1);

                //o.world_pos = float4(v0 + float3(0, 1, 0) * _GrassHeight * v.uv.y + prop.right * (v.uv.x - 0.5) * _GrassWidth, 1);
                o.vertex = mul(UNITY_MATRIX_VP, o.world_pos);
                o.normal.xyz = normalize(cross(float3(0, 1, 0), prop.right));
                o.uv = v.uv;
                o.color = prop.color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.normal.xyz);
                //return (n * 0.5 + 0.5, 1);
                float3 light, lightDir;
                float distanceAtten, shadowAtten;
                GetMainLight_float(i.world_pos.xyz, light, lightDir, distanceAtten, shadowAtten);

                float intensity = min(max(0, dot(n, lightDir)) * /*distanceAtten * */shadowAtten, 1.0);

                //float3 color = _HighlightColor;
                float top = max(0, min(1, 1 - i.uv.y));
                float edge = max(0, min(abs(0.5 - i.uv.x) * 2, 1));
                float3 color = lerp(_GrassTopColor.xyz, lerp(_GrassEdgeColor.xyz, _GrassCenterColor.xyz, edge), top);
                float3 shadowCol = _GrassShadowColor.xyz * color;
                color = lerp(color, shadowCol, 1.0 - intensity);

                color *= i.color;
                float T = length(_WorldSpaceCameraPos - i.world_pos.xyz);
                ApplyFog(color, T);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
