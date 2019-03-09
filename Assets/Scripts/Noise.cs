using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Noise {

    public static float [,] GenerateNoiseMap(
        int mapWidth, int mapHeight, int seed, float scale,
        int octaves, float persistence, float lacunarity, Vector2 offset
    ) {
        float[,] noiseMap = new float[mapWidth, mapHeight];

        // sample each octave from a different location in the noise domain
        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves]; 
        for (int i = 0; i < octaves; i++) {
            // limit to 100K, as higher input returns same noise
            float offsetX = prng.Next(-100000, 100000) + offset.x; 
            float offsetY = prng.Next(-100000, 100000) + offset.y;

            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }

        if (scale <= 0) {
            scale = 0.0001f;
        }

        float maxNoiseHeight = float.MinValue;
        float minNoiseHeight = float.MaxValue;

        for (int y = 0; y < mapHeight; y++) {
            for (int x = 0; x < mapWidth; x++) {

                float amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;

                for (int i = 0; i < octaves; i++) {
                    float sampleX = x / scale * frequency + octaveOffsets[i].x;
                    float sampleY = y / scale * frequency + octaveOffsets[i].y;

                    // PerlinNoise returns 0..1
                    // To get negative noise values, mul by 2, then subtract 1
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistence;   // decrease ampl -- pers is 0..1
                    frequency *= lacunarity;    // increase freq -- lacu s/b > 1
                }

                if (noiseHeight > maxNoiseHeight) {
                    maxNoiseHeight = noiseHeight;
                } else if (noiseHeight < minNoiseHeight) {
                    minNoiseHeight = noiseHeight;
                }
                noiseMap[x, y] = noiseHeight;
            }
        }

        for (int y = 0; y < mapHeight; y++) {
            for (int x = 0; x < mapWidth; x++) {
                // normalize noiseHeight: InverseLerp returns 0..1
                noiseMap[x, y] = Mathf.InverseLerp(minNoiseHeight, maxNoiseHeight, noiseMap[x, y]);
            }
        }

        return noiseMap;
    }
}
