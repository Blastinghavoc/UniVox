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

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // support for Unity fog
            #pragma multi_compile_fog

            #pragma require 2darray

            #include "UnityCG.cginc"//Include basic Unity shader definitions

            struct appdata//Per-vertex data
            {
                float4 vertex : POSITION;
                float3 uv : TEXCOORD0;
                float4 color : COLOR;
                half3 normal: NORMAL;
            };

            struct v2f//Vertex to fragment shader data
            {
                float3 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                half3 worldNormal : TEXCOORD1;
            };

            UNITY_DECLARE_TEX2DARRAY(_MainTex);
            //Global light variables
            float GlobalLightLevel;
            float GlobalLightMinIntensity;
            float GlobalLightMaxIntensity;
            float3 GlobalLightDirection;

            //Vertex shader, sets up data for fragment shader
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

            //Fragment shader
            fixed4 frag (v2f i) : SV_Target
            {
                // Sample the texture
                fixed4 col = UNITY_SAMPLE_TEX2DARRAY(_MainTex, i.uv);
                
                //Use vertex colour alpha to interporlate global sunlight value
                float localSunIntensity = lerp(GlobalLightMinIntensity,GlobalLightMaxIntensity, GlobalLightLevel *i.color.a);
                float3 sunIntensityVector = float3(localSunIntensity,localSunIntensity,localSunIntensity);
                //Ambient sunlight
                float3 sunlightAmbient = 0.8* sunIntensityVector;
                //Directional sunlight
                float diffuseAmount = max(dot(i.worldNormal, GlobalLightDirection), 0.0);
                float3 sunlightDiffuse = 0.2 * diffuseAmount * sunIntensityVector;     

                float3 totalSunlight = sunlightDiffuse + sunlightAmbient;

                float3 dynamicLight = i.color.xyz;

                //Clamp final light colour and blend with texture colour
                col.xyz = (clamp(totalSunlight+dynamicLight,0,1))*col.xyz;             

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
