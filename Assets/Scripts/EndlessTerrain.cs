using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour {

    const float viewerMoveThresholdForChunkUpdate = 25f;
    const float sqrViewerMoveThresholdForChunkUpdate = 
        viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;
    const float colliderGenerationDistanceThreshold = 5;

    public int          colliderLODIndex;
    public LODInfo[]    detailLevels;
    public static float maxViewDistance;

    public Transform    viewer;
    public Material     meshMaterial;

    public static Vector2 viewerPos;
    Vector2 viewerPosOld;
    static MapGenerator mapGenerator;
    int chunkSize;
    int chunksVisibleInViewDistance;

    Dictionary<Vector2, TerrainChunk> terrainChunkDict = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> visibleTerrainChunks = new List<TerrainChunk>();

    void Start() {
        mapGenerator = FindObjectOfType<MapGenerator>();

        // last elem in LOD array is least detailed
        maxViewDistance = detailLevels[detailLevels.Length - 1].visibleDistanceThreshold;
        chunkSize = mapGenerator.mapChunkSize - 1;
        chunksVisibleInViewDistance = Mathf.RoundToInt(
            maxViewDistance / chunkSize
        );

        UpdateVisibleChunks();
    }

    void Update() {
        viewerPos = new Vector2(viewer.position.x, viewer.position.z) / 
            mapGenerator.terrainData.uniformScale;

        if (viewerPos != viewerPosOld) {
            foreach (var chunk in visibleTerrainChunks) {
                chunk.UpdateCollisionMesh();
            }
        }

        if (  (viewerPosOld - viewerPos).sqrMagnitude 
            > sqrViewerMoveThresholdForChunkUpdate
        ) {
            viewerPosOld = viewerPos;
            UpdateVisibleChunks();
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
    void UpdateVisibleChunks() {
        HashSet<Vector2> alreadyUpdatedChunkCoords = new HashSet<Vector2>();

        for (int i = visibleTerrainChunks.Count - 1; i >= 0; i--) {
            alreadyUpdatedChunkCoords.Add(visibleTerrainChunks[i].coord);
            visibleTerrainChunks[i].UpdateTerrainChunk();
        }

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
                if (!alreadyUpdatedChunkCoords.Contains(viewedChunkCoord)) {
                    if (terrainChunkDict.ContainsKey(viewedChunkCoord)) {
                        terrainChunkDict[viewedChunkCoord].UpdateTerrainChunk();
                    }
                    else {
                        terrainChunkDict.Add(viewedChunkCoord, new TerrainChunk(
                            viewedChunkCoord, chunkSize, detailLevels, colliderLODIndex,
                            this.transform, meshMaterial
                        ));
                    }
                }

            }


        }
    }

    public class TerrainChunk {

        public Vector2 coord;

        GameObject meshObject;
        Vector2 position;
        Bounds bounds;

        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        MeshCollider meshCollider;

        LODInfo[] detailLevels;
        LODMesh[] lodMeshes;
        int colliderLODIndex;

        MapData mapData;
        bool mapDataReceived;

        int previousLODIndex = -1;
        bool hasSetCollider;

        public TerrainChunk(
            Vector2 coord, int size, LODInfo[] detailLevels, int colliderLODIndex,
            Transform parent, Material material
        ) {
            this.coord = coord;
            this.detailLevels = detailLevels;
            this.colliderLODIndex = colliderLODIndex;

            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            meshObject = new GameObject("Terrain Chunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();
            meshRenderer.material = material;

            meshObject.transform.position =
                positionV3 * mapGenerator.terrainData.uniformScale;
            meshObject.transform.parent = parent;
            meshObject.transform.localScale =
                Vector3.one * mapGenerator.terrainData.uniformScale;

            SetVisible(false);

            lodMeshes = new LODMesh[detailLevels.Length];
            for (int i = 0; i < detailLevels.Length; i++) {
                lodMeshes[i] = new LODMesh(detailLevels[i].lod);
                lodMeshes[i].updateCallback += UpdateTerrainChunk;
                if (i == colliderLODIndex) {
                    lodMeshes[i].updateCallback += UpdateCollisionMesh;
                }
            }

            mapGenerator.RequestMapData(position, OnMapDataReceived);
        }

        void OnMapDataReceived(MapData mapData) {
            this.mapData = mapData;
            mapDataReceived = true;

            UpdateTerrainChunk();
        }

        public void UpdateTerrainChunk() {
            if (!mapDataReceived) { return; }

            float viewerDistFromNearestEdge =
                Mathf.Sqrt(bounds.SqrDistance(viewerPos));
            bool wasVisible = IsVisible();
            bool visible = viewerDistFromNearestEdge <= maxViewDistance;

            if (visible) {
                int lodIndex = 0;

                // don't need to look at final elem as visible will be false,
                // viewerDistFromNearestEdge will be > maxViewDistance
                for (int i = 0; i < detailLevels.Length - 1; i++) {
                    if (viewerDistFromNearestEdge
                        > detailLevels[i].visibleDistanceThreshold
                    ) {
                        lodIndex = i + 1;
                    }
                    else {
                        break;
                    }
                }

                if (lodIndex != previousLODIndex) {
                    LODMesh lodMesh = lodMeshes[lodIndex];
                    if (lodMesh.hasMesh) {
                        previousLODIndex = lodIndex;
                        meshFilter.mesh = lodMesh.mesh;
                    }
                    else if (!lodMesh.hasRequestedMesh) {
                        lodMesh.RequestMesh(mapData);
                    }
                }

                visibleTerrainChunks.Add(this);
            }
            if (wasVisible != visible) {
                if (visible) {
                    visibleTerrainChunks.Add(this);
                } else {
                    visibleTerrainChunks.Remove(this);
                }
                SetVisible(visible);
            }
        }

        public void UpdateCollisionMesh() {
            if (hasSetCollider) { return; }

            float sqrDistanceFromViewerToEdge = bounds.SqrDistance(viewerPos);

            if (sqrDistanceFromViewerToEdge < detailLevels[colliderLODIndex].sqrVisibleDistanceThreshold) {
                if (! lodMeshes[colliderLODIndex].hasRequestedMesh) {
                    lodMeshes[colliderLODIndex].RequestMesh(mapData);
                }
            }

            if (sqrDistanceFromViewerToEdge < colliderGenerationDistanceThreshold) {
                if (lodMeshes[colliderLODIndex].hasMesh) {
                    meshCollider.sharedMesh = lodMeshes[colliderLODIndex].mesh;
                    hasSetCollider = true;
                }
            }
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
        public event System.Action updateCallback;

        public LODMesh(int lod) {
            this.lod = lod;
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
        [Range(0, MeshGenerator.numSupportedLODs - 1)]
        public int lod;
        public float visibleDistanceThreshold;

        public float sqrVisibleDistanceThreshold {
            get {
                return visibleDistanceThreshold * visibleDistanceThreshold;
            }
        }
    }
}
