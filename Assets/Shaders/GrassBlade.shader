Shader "Bliss/GrassBlade"
{
    Properties
    {
        _HighlightColor("HighlightColor", Color) = (1, 1, 1, 1)
        _ShadowColor("ShadowColor", Color) = (0, 0, 0, 1)
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
            //#pragma enable_d3d11_debug_symbols 
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
                float4 vertex : SV_POSITION;
            };

            float4 _HighlightColor;
            float4 _ShadowColor;

            v2f vert (appdata v, uint instanceID: SV_InstanceID)
            {
                v2f o;
                //o.vertex = UnityObjectToClipPos(v.vertex);
                o.world_pos = mul(_Properties[instanceID].objectToWorldMat, v.vertex);
                o.vertex = mul(UNITY_MATRIX_VP, o.world_pos);
                o.normal = mul(_Properties[instanceID].objectToWorldRotateMat, float4(v.normal, 0));
                o.uv = v.uv;
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

                float3 color = lerp(_HighlightColor, _ShadowColor, 1.0 - intensity);
                float T = length(_WorldSpaceCameraPos - i.world_pos.xyz);
                ApplyFog(color, T);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
