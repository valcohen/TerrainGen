using System.Collections;
using UnityEngine;

[CreateAssetMenu]
public class NoiseData : ScriptableObject {

    public Noise.NormalizeMode normalizeMode;

    public float noiseScale;    // TODO: "textureScale" for non-noise sources?
    public int octaves;       // layers of noise
    [Range(0, 1)]
    public float persistence;   // decrease ampl -- pers is 0..1
    public float lacunarity;    // increase freq -- lacu s/b > 1

    public int seed;
    public Vector2 offset;


    void OnValidate() {
        if (lacunarity < 1) { lacunarity = 1; }
        if (octaves < 0) { octaves = 0; }
    }

}
