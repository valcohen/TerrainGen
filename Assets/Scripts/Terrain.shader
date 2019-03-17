﻿Shader "Custom/Terrain" {
	Properties {
        testTexture("Texture", 2D) = "white"{}
        testScale("Scale", Float) = 1
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

        const static int    maxLayerCount = 8;
        const static float  epsilon = 1E-4;  // add to avoid divide-by-zero errors

        int     layerCount;
        float3  baseColors[maxLayerCount];
        float   baseStartHeights[maxLayerCount];
        float   baseBlends[maxLayerCount];
        float   baseColorStrength[maxLayerCount];
        float   baseTextureScales[maxLayerCount];

        float minHeight;
        float maxHeight;

        sampler2D testTexture;
        float testScale;

        struct Input {
			float3 worldPos;
            float3 worldNormal;
		};

        float inverseLerp(float a, float b, float value) {
            return saturate((value-a)/(b-a));  // clamp to 0..1
        }

		void surf (Input IN, inout SurfaceOutputStandard o) {

            float heightPct = inverseLerp(minHeight, maxHeight, IN.worldPos.y);

            for (int i = 0; i < layerCount; i++) {
                // 0 when pixel is 1/2 the base blend's value below start height,
                // 1 when pixel is 1/2 thr base blend's value above start height
                float drawStrength = inverseLerp(
                    -baseBlends[i]/2 - epsilon,
                    baseBlends[i]/2, 
                    (heightPct - baseStartHeights[i])
                );
                o.Albedo = o.Albedo * (1-drawStrength) // prevent black if drawStrength == 0
                         + baseColors[i] * drawStrength;

                // triplanar mapping
                float3 scaledWorldPos = IN.worldPos / testScale;
                float3 blendAxes = abs(IN.worldNormal); // normals are 0..1 away..facing
                // ensure blendAxes.x + y + z = 1 to prevent overbrightness 
                // if any one channel exceeds 1
                blendAxes /= blendAxes.x + blendAxes.y + blendAxes.z;   
                float3 xProjection = tex2D(testTexture, scaledWorldPos.yz) * blendAxes.x;
                float3 yProjection = tex2D(testTexture, scaledWorldPos.xz) * blendAxes.y;
                float3 zProjection = tex2D(testTexture, scaledWorldPos.xy) * blendAxes.z;

                // o.Albedo = xProjection + yProjection + zProjection;
            }
        }
		ENDCG
	}
	FallBack "Diffuse"
}
