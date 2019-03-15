using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour {

    const float viewerMoveThresholdForChunkUpdate = 25f;
    const float sqrViewerMoveThresholdForChunkUpdate = 
        viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;

    public LODInfo[] detailLevels;
    public static float maxViewDistance;

    public Transform viewer;
    public Material meshMaterial;

    public static Vector2 viewerPos;
    Vector2 viewerPosOld;
    static MapGenerator mapGenerator;
    int chunkSize;
    int chunksVisibleInViewDistance;

    Dictionary<Vector2, TerrainChunk> terrainChunkDict = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

    void Start() {
        mapGenerator = FindObjectOfType<MapGenerator>();

        // last elem in LOD array is least detailed
        maxViewDistance = detailLevels[detailLevels.Length - 1].visibleDistanceThreshold;
        chunkSize = MapGenerator.mapChunkSize - 1;
        chunksVisibleInViewDistance = Mathf.RoundToInt(
            maxViewDistance / chunkSize
        );

        updateVisibleChunks();
    }

    void Update() {
        viewerPos = new Vector2(viewer.position.x, viewer.position.z);

        if (  (viewerPosOld - viewerPos).sqrMagnitude 
            > sqrViewerMoveThresholdForChunkUpdate
        ) {
            viewerPosOld = viewerPos;
            updateVisibleChunks();
        }
    }

    /*
     * position: location of center of chunk relative to current chunk
     * coordinates: offset of chunk relative to current chunk (pos/chunkSize)
     * 
     * +---------+---------+---------+---------+---------+
     * |         |         |         |         |         
     * |         | -240:0  |   0:0   | 240:0   |    position
     * |         |   -1:0  |   0:0   |   1:0   |    coord: pos / chunkSize
     * |         |         |         |         |         
     * +---------+---------+---------+---------+---------+
     * |         |         |         |         |         |
     * |         |         |   0:-240| 240:-240|         |
     * |         |         |   0:-1  |   1:-1  |         |
     * |         |         |         |         |         |
     * +---------+---------+---------+---------+---------+
     */
    void updateVisibleChunks() {

        for (int i = 0; i < terrainChunksVisibleLastUpdate.Count; i++) {
            terrainChunksVisibleLastUpdate[i].SetVisible(false);
        }
        terrainChunksVisibleLastUpdate.Clear();

        int currentChunkCoordX = Mathf.RoundToInt(viewerPos.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPos.y / chunkSize);

        for (int yOffset = -chunksVisibleInViewDistance;
             yOffset <= chunksVisibleInViewDistance;
             yOffset++
        ) {
            for (int xOffset = -chunksVisibleInViewDistance;
                xOffset <= chunksVisibleInViewDistance;
                xOffset++
            ) {
                Vector2 viewedChunkCoord = new Vector2(
                    currentChunkCoordX + xOffset,
                    currentChunkCoordY + yOffset
                );

                if (terrainChunkDict.ContainsKey(viewedChunkCoord)) {
                    terrainChunkDict[viewedChunkCoord].UpdateTerrainChunk();
                } else {
                    terrainChunkDict.Add(viewedChunkCoord, new TerrainChunk(
                        viewedChunkCoord, chunkSize, detailLevels, 
                        this.transform, meshMaterial
                    )); 
                }

            }


        }
    }

    public class TerrainChunk {

        GameObject  meshObject;
        Vector2     position;
        Bounds      bounds;

        MeshRenderer meshRenderer;
        MeshFilter   meshFilter;

        LODInfo[] detailLevels;
        LODMesh[] lodMeshes;

        MapData mapData;
        bool mapDataReceived;

        int previousLODIndex = -1;

        public TerrainChunk(
            Vector2 coord, int size, LODInfo[] detailLevels,
            Transform parent, Material material
        ) {
            this.detailLevels = detailLevels;

            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            meshObject = new GameObject("Terrain Chunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshRenderer.material = material;

            meshObject.transform.position = positionV3;
            meshObject.transform.parent = parent;

            SetVisible(false);

            lodMeshes = new LODMesh[detailLevels.Length];
            for (int i = 0; i < detailLevels.Length; i++) {
                lodMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateTerrainChunk);
            }

            mapGenerator.RequestMapData(position, OnMapDataReceived);
        }

        void OnMapDataReceived(MapData mapData) {
            this.mapData = mapData;
            mapDataReceived = true;

            Texture2D texture = TextureGenerator.TextureFromColorMap(
                mapData.colorMap,
                MapGenerator.mapChunkSize, MapGenerator.mapChunkSize
            );
            meshRenderer.material.mainTexture = texture;

            UpdateTerrainChunk();
        }

        public void UpdateTerrainChunk() {
            if (!mapDataReceived) { return; }

            float viewerDistFromNearestEdge = 
                Mathf.Sqrt(bounds.SqrDistance(viewerPos));
            bool visible = viewerDistFromNearestEdge <= maxViewDistance;

            if (visible) {
                int lodIndex = 0;

                // don't need to look at final elem as visible will be false,
                // viewerDistFromNearestEdge will be > maxViewDistance
                for (int i = 0; i < detailLevels.Length - 1; i++) {
                    if (  viewerDistFromNearestEdge 
                        > detailLevels[i].visibleDistanceThreshold
                    ) {
                        lodIndex = i + 1; 
                    } else {
                        break;
                    }
                }

                if (lodIndex != previousLODIndex) {
                    LODMesh lodMesh = lodMeshes[lodIndex];
                    if (lodMesh.hasMesh) {
                        previousLODIndex = lodIndex;
                        meshFilter.mesh = lodMesh.mesh;
                    } else if (! lodMesh.hasRequestedMesh) {
                        lodMesh.RequestMesh(mapData);
                    }
                }

                terrainChunksVisibleLastUpdate.Add(this);
            }

            SetVisible(visible);
        }

        public void SetVisible(bool visible) {
            meshObject.SetActive(visible);
        }

        public bool IsVisible() {
            return meshObject.activeSelf;
        }
    }

    class LODMesh {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;
        int lod;
        System.Action updateCallback;

        public LODMesh(int lod, System.Action updateCallback) {
            this.lod = lod;
            this.updateCallback = updateCallback;
        }

        void OnMeshDataReceived(MeshData meshData) {
            mesh = meshData.CreateMesh();
            hasMesh = true;

            updateCallback();
        }

        public void RequestMesh(MapData mapData) {
            hasRequestedMesh = true;
            mapGenerator.RequestMeshData(mapData, lod, OnMeshDataReceived);
        }
    }

    [System.Serializable]
    public struct LODInfo {
        public int lod;
        public float visibleDistanceThreshold;
    }
}
