Shader "Bliss/Billboard"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            //"Queue" = "AlphaTest"
            //"RenderType" = "TransparentCutout"

            "Queue" = "Geometry"
            "RenderType" = "Opaque"
        }
        ZWrite On Cull Off

        Pass
        {
            HLSLPROGRAM
            //#pragma enable_d3d11_debug_symbols 
            //#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            //#pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            //#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            #include "includes/GrassRenderProperty.hlsl"
            //#include "includes/LightingHelp.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
    
            StructuredBuffer<GrassRenderProperty> _Properties;

            struct v2f
            {
                float2 uv : TEXCOORD0;
                //UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v, uint instanceID: SV_InstanceID)
            {
                v2f o;
                //o.vertex = UnityObjectToClipPos(v.vertex);
                GrassRenderProperty prop = _Properties[instanceID];
                float3 v0 = prop.v0;
                float4 pos = float4(v0.xyz + float3(0, 1, 0) * _GrassHeight * (1 - v.uv.y) + prop.right * (0.5 - v.uv.x), 1);
                o.vertex = mul(UNITY_MATRIX_VP, pos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                //UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // sample the texture
                half4 col = tex2D(_MainTex, i.uv);
                // apply fog
                //UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDHLSL
        }
    }
}
