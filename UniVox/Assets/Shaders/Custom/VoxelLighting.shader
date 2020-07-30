Shader "Unlit/VoxelLighting"
{
    //Based on https://github.com/b3agz/Code-A-Game-Like-Minecraft-In-Unity/blob/master/15-lighting-part-1/Assets/Scripts/StandardBlockShader.shader

    Properties
    {
        _MainTex ("Texture", 2DArray) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        //LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #pragma require 2darray//Reduire 2d array

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 uv : TEXCOORD0;
                float4 color : COLOR;
                half3 normal: NORMAL;
            };

            struct v2f
            {
                float3 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                half3 worldNormal : TEXCOORD1;
            };

            UNITY_DECLARE_TEX2DARRAY(_MainTex);
            float GlobalLightLevel;
            float GlobalLightMinIntensity;
            float GlobalLightMaxIntensity;
            float3 GlobalLightDirection;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);

                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            float inverseLerp(float lo, float hi, float val){
                float range = hi - lo;
                return (val - lo) / range;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = UNITY_SAMPLE_TEX2DARRAY(_MainTex, i.uv);
                
                float localSunIntensity = lerp(GlobalLightMinIntensity,GlobalLightMaxIntensity, GlobalLightLevel *i.color.a);
                float3 sunlightAmbient = 0.8* float3(localSunIntensity,localSunIntensity,localSunIntensity);
                float diffuseAmount = max(dot(i.worldNormal, GlobalLightDirection), 0.0);
                float3 sunlightDiffuse = 0.2 * diffuseAmount * sunlightAmbient;     
                float3 totalSunlight = sunlightDiffuse + sunlightAmbient;

                float3 dynamicLight = i.color.xyz;

                col.xyz = (clamp(totalSunlight+dynamicLight,0,1))*col.xyz;         

                //stupid AO approximation
                // float3 up = float3(0,1,0);
                // float dotProd = dot(i.worldNormal, up);
                // float scale = lerp(0.8,1, inverseLerp(-1,1,dotProd));
                // col.xyz = scale * col.xyz;       

                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }

    //for SSAO
    Fallback "Diffuse"
}
