using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class MapGenerator : MonoBehaviour {

    public enum DrawMode { NoiseMap, Mesh, FalloffMap };
    public DrawMode drawMode;

    public TerrainData  terrainData;
    public NoiseData    noiseData;
    public TextureData  textureData;

    public Material     terrainMaterial;

    [Range(0, MeshGenerator.numSupportedChunkSizes - 1)]
    public int chunkSizeIndex;
    [Range(0, MeshGenerator.numSupportedFlatShadedChunkSizes- 1)]
    public int flatShadedChunkSizeIndex;

    // we'll multiply LOD by 2 to get increment - 2,4,6,..12
    [Range(0, MeshGenerator.numSupportedLODs - 1)]
    public int editorPreviewLOD;   // higher is simpler

    public bool autoUpdate;

    float[,] falloffMap;

    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = 
        new Queue<MapThreadInfo<MapData>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue =
        new Queue<MapThreadInfo<MeshData>>();

    void Awake() {
        textureData.ApplyToMaterial(terrainMaterial);
        textureData.UpdateMeshHeights(
            terrainMaterial, terrainData.minHeight, terrainData.maxHeight);
    }

    void OnValuesUpdated() {
        if (!Application.isPlaying) {   // TODO: investigate if_unity_editor directive
            DrawMapInEditor();
        }
    }

    void OnTextureValuesUpdated() {
        textureData.ApplyToMaterial(terrainMaterial);
    }

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
     * w <= 255             -- max width of square mesh
     * 
     * since i must be a factor of w-1, 
     * w = 241
     * w - 1 = 240  -- has factors of 2,4,6,8,10,12
     * 
     * When flat shading, we need more vertices, so we have to 
     * reduce the chunk size. The next best width is
     * w = 97
     * w - 1 = 96   -- has factors of 2,4,6,8,12
     */
    public int mapChunkSize {
        get {
            if (terrainData.useFlatShading) {
                return MeshGenerator.supportedFlatShadedChunkSizes[
                    flatShadedChunkSizeIndex] - 1;  // + 2 borders
            } else {
                return MeshGenerator.supportedChunkSizes[chunkSizeIndex] - 1;  // + 2 borders
            }
        }
    }

    public void DrawMapInEditor() {
        textureData.UpdateMeshHeights(
            terrainMaterial, terrainData.minHeight, terrainData.maxHeight);
        MapData mapData = GenerateMapData(Vector2.zero);

        MapDisplay display = FindObjectOfType<MapDisplay>();
        if (drawMode == DrawMode.NoiseMap) {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(
                mapData.heightMap)
           );
        }
        else if (drawMode == DrawMode.Mesh) {
            display.DrawMesh(
                MeshGenerator.GenerateTerrainMesh(
                    mapData.heightMap, terrainData.meshHeightMultiplier,
                    terrainData.meshHeightCurve, editorPreviewLOD, 
                    terrainData.useFlatShading
                )
            );
        }
        else if (drawMode == DrawMode.FalloffMap) {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(
                FalloffGenerator.GenerateFalloffMap(mapChunkSize))
           );
        }
    }

    public void RequestMapData(Vector2 center, Action<MapData> callBack) {
        textureData.UpdateMeshHeights(
            terrainMaterial, terrainData.minHeight, terrainData.maxHeight
        );

        ThreadStart threadStart = delegate {
            MapDataThread(center, callBack);
        };

        new Thread(threadStart).Start();
    }

    void MapDataThread(Vector2 center, Action<MapData> callback) {
        MapData mapData = GenerateMapData(center);
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
            mapData.heightMap, terrainData.meshHeightMultiplier, 
            terrainData.meshHeightCurve, lod, terrainData.useFlatShading
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

    MapData GenerateMapData(Vector2 center) {
        float[,] noiseMap = Noise.GenerateNoiseMap(
            mapChunkSize + 2 /* borders */, mapChunkSize + 2 /* borders */, 
            noiseData.seed, noiseData.noiseScale, noiseData.octaves, 
            noiseData.persistence, noiseData.lacunarity, 
            center + noiseData.offset, noiseData.normalizeMode
        );

        if (terrainData.useFalloff) {
            if (falloffMap == null) {
                falloffMap = FalloffGenerator.GenerateFalloffMap(mapChunkSize + 2);
            }
            for (int y = 0; y < mapChunkSize+ 2; y++) {
                for (int x = 0; x < mapChunkSize + 2; x++) {
                    noiseMap[x, y] = Mathf.Clamp01(noiseMap[x, y] - falloffMap[x, y]);
                }
            }
        }

        return new MapData(noiseMap);
    }

    void OnValidate() {

        if (terrainData != null) {
            terrainData.OnValuesUpdated -= OnValuesUpdated; // there can be 
            terrainData.OnValuesUpdated += OnValuesUpdated; // only one!
        }
        if (noiseData != null) {
            noiseData.OnValuesUpdated -= OnValuesUpdated;
            noiseData.OnValuesUpdated += OnValuesUpdated;
        }
        if (textureData != null) {
            textureData.OnValuesUpdated -= OnTextureValuesUpdated;
            textureData.OnValuesUpdated += OnTextureValuesUpdated;
        }
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

public struct MapData {
    public readonly float[,] heightMap;

    public MapData(float[,] heightMap) {
        this.heightMap = heightMap;
    }
}