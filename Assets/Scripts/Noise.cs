using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Noise {

    public enum NormalizeMode { Local, Global };

    public static float[,] GenerateNoiseMap(
        int mapWidth, int mapHeight, int seed, float scale,
        int octaves, float persistence, float lacunarity, Vector2 offset,
        NormalizeMode normalizeMode
    ) {
        float[,] noiseMap = new float[mapWidth, mapHeight];

        // sample each octave from a different location in the noise domain
        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves];

        float maxPossibleHeight = 0;
        float amplitude = 1;
        float frequency = 1;

        for (int i = 0; i < octaves; i++) {
            // limit to 100K, as higher input returns same noise
            float offsetX = prng.Next(-100000, 100000) + offset.x; 
            float offsetY = prng.Next(-100000, 100000) - offset.y;

            octaveOffsets[i] = new Vector2(offsetX, offsetY);

            maxPossibleHeight += amplitude;
            amplitude *= persistence;
        }

        if (scale <= 0) {
            scale = 0.0001f;
        }

        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;

        // used to scale from the center
        float halfWidth = mapWidth / 2f;
        float halfHeight = mapHeight / 2f;

        for (int y = 0; y < mapHeight; y++) {
            for (int x = 0; x < mapWidth; x++) {

                amplitude = 1;
                frequency = 1;
                float noiseHeight = 0;

                for (int i = 0; i < octaves; i++) {
                    float sampleX = (x - halfWidth + octaveOffsets[i].x) / scale * frequency;
                    float sampleY = (y - halfHeight + octaveOffsets[i].y) / scale * frequency;

                    // PerlinNoise returns 0..1
                    // To get negative noise values, mul by 2, then subtract 1
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistence;   // decrease ampl -- pers is 0..1
                    frequency *= lacunarity;    // increase freq -- lacu s/b > 1
                }

                if (noiseHeight > maxLocalNoiseHeight) {
                    maxLocalNoiseHeight = noiseHeight;
                } else if (noiseHeight < minLocalNoiseHeight) {
                    minLocalNoiseHeight = noiseHeight;
                }
                noiseMap[x, y] = noiseHeight;
            }
        }

        for (int y = 0; y < mapHeight; y++) {
            for (int x = 0; x < mapWidth; x++) {
                if (normalizeMode == NormalizeMode.Local) {
                    // normalize noiseHeight: InverseLerp returns 0..1
                    noiseMap[x, y] = Mathf.InverseLerp(
                        minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y]
                    );
                } else {
                    // estimate min/max NoiseHeight across multiple chunks
                    float normalizedHeight = (noiseMap[x, y] + 1)
                        / (2f * maxPossibleHeight);
                    // reverse (sample * 2 - 1) op that gave negative values 

                    noiseMap[x, y] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);
                }
            }
        }

        return noiseMap;
    }
}
