Shader "Custom/Terrain" {
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
        const static float  epsilon = 1e-4;  // add to avoid divide-by-zero errors

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

        UNITY_DECLARE_TEX2DARRAY(baseTextures);

        struct Input {
			float3 worldPos;
            float3 worldNormal;
		};

        float inverseLerp(float a, float b, float value) {
            return saturate((value-a)/(b-a));  // clamp to 0..1
        }

        float3 triplanar(float3 worldPos, float scale, float3 blendAxes, int textureIndex) {
            float3 scaledWorldPos = worldPos / scale;
            float3 xProjection = UNITY_SAMPLE_TEX2DARRAY(
                baseTextures, float3(scaledWorldPos.y, scaledWorldPos.z, textureIndex)
            ) * blendAxes.x;
            float3 yProjection = UNITY_SAMPLE_TEX2DARRAY(
                baseTextures, float3(scaledWorldPos.x, scaledWorldPos.z, textureIndex)
            ) * blendAxes.y;
            float3 zProjection = UNITY_SAMPLE_TEX2DARRAY(
                baseTextures, float3(scaledWorldPos.x, scaledWorldPos.y, textureIndex)
            ) * blendAxes.z;

            return xProjection + yProjection + zProjection;
        }

		void surf (Input IN, inout SurfaceOutputStandard o) {

            float heightPct = inverseLerp(minHeight, maxHeight, IN.worldPos.y);

            float3 blendAxes = abs(IN.worldNormal); // normals are 0..1 away..facing
            // ensure blendAxes.x + y + z = 1 to prevent overbrightness 
            // if any one channel exceeds 1
            blendAxes /= blendAxes.x + blendAxes.y + blendAxes.z;   

            for (int i = 0; i < layerCount; i++) {
                // 0 when pixel is 1/2 the base blend's value below start height,
                // 1 when pixel is 1/2 thr base blend's value above start height
                float drawStrength = inverseLerp(
                    -baseBlends[i]/2 - epsilon,
                    baseBlends[i]/2, 
                    (heightPct - baseStartHeights[i])
                );

                float3 baseColor    = baseColors[i] * baseColorStrength[i];
                float3 textureColor = triplanar(IN.worldPos, baseTextureScales[i], blendAxes, i)
                                    * (1 - baseColorStrength[i]);

                o.Albedo = o.Albedo * (1-drawStrength) // prevent black if drawStrength == 0
                         + (baseColor + textureColor) * drawStrength;

            }
        }
		ENDCG
	}
	FallBack "Diffuse"
}
