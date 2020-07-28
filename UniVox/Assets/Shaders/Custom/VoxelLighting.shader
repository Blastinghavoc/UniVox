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
            };

            struct v2f
            {
                float3 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            UNITY_DECLARE_TEX2DARRAY(_MainTex);
            //float4 _MainTex_ST;
            float GlobalLightLevel;
            float GlobalLightMinIntensity;
            float GlobalLightMaxIntensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;

                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = UNITY_SAMPLE_TEX2DARRAY(_MainTex, i.uv);
                
                float localSunLight = lerp(GlobalLightMinIntensity,GlobalLightMaxIntensity, GlobalLightLevel *i.color.a);
                float3 sunColour = float3(localSunLight,localSunLight,localSunLight) * col.xyz;
                float3 dynamicColour = i.color.xyz * col.xyz;
                //float3 combinedColour = clamp(sunColour+dynamicColour,0,1);
                float3 combinedColour = sunColour+dynamicColour;
                col.xyz = combinedColour;

                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
