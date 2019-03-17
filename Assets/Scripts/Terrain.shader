﻿Shader "Custom/Terrain" {
	Properties {
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

        const static int maxColorCount = 8;
        const static float epsilon = 1E-4;  // add to avoid divide-by-zero errors

        int     baseColorCount;
        float3  baseColors[maxColorCount];
        float   baseStartHeights[maxColorCount];
        float   baseBlends[maxColorCount];

        float minHeight;
        float maxHeight;

        struct Input {
			float3 worldPos;
		};

        float inverseLerp(float a, float b, float value) {
            return saturate((value-a)/(b-a));  // clamp to 0..1
        }

		void surf (Input IN, inout SurfaceOutputStandard o) {

            float heightPct = inverseLerp(minHeight, maxHeight, IN.worldPos.y);

            for (int i = 0; i < baseColorCount; i++) {
                // 0 when pixel is 1/2 the base blend's value below start height,
                // 1 when pixel is 1/2 thr base blend's value above start height
                float drawStrength = inverseLerp(
                    -baseBlends[i]/2 - epsilon,
                    baseBlends[i]/2, 
                    (heightPct - baseStartHeights[i])
                );
                o.Albedo = o.Albedo * (1-drawStrength) // prevent black if drawStrength == 0
                         + baseColors[i] * drawStrength;
            }
        }
		ENDCG
	}
	FallBack "Diffuse"
}
