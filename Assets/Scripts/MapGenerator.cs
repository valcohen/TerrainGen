using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class MapGenerator : MonoBehaviour {

    public enum DrawMode { NoiseMap, ColorMap, Mesh };
    public DrawMode drawMode;

    /*
     * Set chunk width to support multiple mesh LOD 
     * w = width, i = increment
     *
     * w = 9:  0  1  2  3  4  5  6  7  8
     * i = 1:  *  *  *  *  *  *  *  *  *   8 vertices
     * 1 = 2:  *     *     *     *     *   5
     * 1 = 4:  *           *           *   3
     *
     * i = factor of (w-1)
     * i = 1,2,4,8
     *
     * number of vertices per line:
     *
     *      (w-1) 
     * v =  ----- + 1
     *        i
     *
     * v = w * h            -- if i = 1, total verts v in mesh
     * v <= 255^2 (65025)   -- unity limit on verts in a mesh
     * w <= 255             -- msx width of square mesh
     * 
     * since i must be a factor of w-1, 
     * w = 241
     * w - 1 = 240  -- has factors of 2,4,6,8,10,12
     */
    public const int mapChunkSize = 241;
    [Range(0,6)]    // we'll multiply LOD by 2 to get increment - 2,4,6,..12
    public int editorPreviewLOD;   // higher is simpler
    public float noiseScale;    // TODO: "textureScale" for non-noise sources?

    public int octaves;
    [Range(0,1)]
    public float persistence;
    public float lacunarity;

    public int seed;
    public Vector2 offset;

    public float meshHeightMultiplier;
    public AnimationCurve meshHeightCurve;

    public bool autoUpdate;

    public TerrainType[] regions;

    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = 
        new Queue<MapThreadInfo<MapData>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue =
        new Queue<MapThreadInfo<MeshData>>();

    public void DrawMapInEditor() {
        MapData mapData = GenerateMapData();

        MapDisplay display = FindObjectOfType<MapDisplay>();
        if (drawMode == DrawMode.NoiseMap) {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(
                mapData.heightMap)
           );
        }
        else if (drawMode == DrawMode.ColorMap) {
            display.DrawTexture(TextureGenerator.TextureFromColorMap(
                mapData.colorMap, mapChunkSize, mapChunkSize)
            );
        }
        else if (drawMode == DrawMode.Mesh) {
            display.DrawMesh(
                MeshGenerator.GenerateTerrainMesh(
                    mapData.heightMap, meshHeightMultiplier,
                    meshHeightCurve, editorPreviewLOD
                ),
                TextureGenerator.TextureFromColorMap(
                    mapData.colorMap, mapChunkSize, mapChunkSize
                )
            );
        }
    }

    public void RequestMapData(Action<MapData> callBack) {
        ThreadStart threadStart = delegate {
            MapDataThread(callBack);
        };

        new Thread(threadStart).Start();
    }

    void MapDataThread(Action<MapData> callback) {
        MapData mapData = GenerateMapData();
        lock (mapDataThreadInfoQueue) {
            mapDataThreadInfoQueue.Enqueue(
                new MapThreadInfo<MapData>(callback, mapData)
            );
        }
    }

    public void RequestMeshData(MapData mapData, int lod, Action<MeshData> callback) {
        ThreadStart threadStart = delegate {
            MeshDataThread(mapData, lod, callback);
        };

        new Thread(threadStart).Start();
    }

    void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback) {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(
            mapData.heightMap,
            meshHeightMultiplier,
            meshHeightCurve,
            lod
        );
        lock (meshDataThreadInfoQueue) {
            meshDataThreadInfoQueue.Enqueue(
                new MapThreadInfo<MeshData>(callback, meshData)
            );
        }
    }

    void Update() {
        if (mapDataThreadInfoQueue.Count > 0) {
            for (int i = 0; i < mapDataThreadInfoQueue.Count; i++) {
                MapThreadInfo<MapData> threadInfo = 
                    mapDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.callbackParam);
            }
        }

        if (meshDataThreadInfoQueue.Count > 0) {
            for (int i = 0; i < meshDataThreadInfoQueue.Count; i++) {
                MapThreadInfo<MeshData> threadInfo =
                    meshDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.callbackParam);
            }
        }
    }

    MapData GenerateMapData() {
        float[,] noiseMap = Noise.GenerateNoiseMap(
            mapChunkSize, mapChunkSize, seed, noiseScale,
            octaves, persistence, lacunarity, offset
        );

        Color[] colorMap = new Color[mapChunkSize * mapChunkSize];
        for (int y = 0; y < mapChunkSize; y++) {
            for (int x = 0; x < mapChunkSize; x++) {
                float currentHeight = noiseMap[x, y];
                for (int i = 0; i < regions.Length; i++) {
                    if (currentHeight <= regions[i].height) {
                        colorMap[y * mapChunkSize + x] = regions[i].color;
                        break;
                    }
                }
            }
        }

        return new MapData(noiseMap, colorMap);
    }

    void OnValidate() {
        if (lacunarity < 1) { lacunarity    = 1; }
        if (octaves < 0)    { octaves       = 0; }
    }

    struct MapThreadInfo<T> {
        public readonly Action<T>   callback;
        public readonly T           callbackParam;

        public MapThreadInfo(Action<T> callback, T callbackParam) {
            this.callback       = callback;
            this.callbackParam  = callbackParam;
        }
    }
}

[System.Serializable]
public struct TerrainType {
    public string name;
    public float height;
    public Color color;
}

public struct MapData {
    public readonly float[,] heightMap;
    public readonly Color[] colorMap;

    public MapData(float[,] heightMap, Color[] colorMap) {
        this.heightMap = heightMap;
        this.colorMap  = colorMap;
    }
}