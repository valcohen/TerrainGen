using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour {

    public const float maxViewDistance = 300;
    public Transform viewer;

    public static Vector2 viewerPos;
    int chunkSize;
    int chunksVisibleInViewDistance;

    Dictionary<Vector2, TerrainChunk> terrainChunkDict = new Dictionary<Vector2, TerrainChunk>();
    List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

    void Start() {
        chunkSize = MapGenerator.mapChunkSize - 1;
        chunksVisibleInViewDistance = Mathf.RoundToInt(
            maxViewDistance / chunkSize
        );
    }

    void Update() {
        viewerPos = new Vector2(viewer.position.x, viewer.position.z);
        updateVisibleChunks();
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
                    if (terrainChunkDict[viewedChunkCoord].IsVisible()) {
                        terrainChunksVisibleLastUpdate.Add(
                            terrainChunkDict[viewedChunkCoord]
                        );
                    }
                } else {
                    terrainChunkDict.Add(viewedChunkCoord, new TerrainChunk(
                        viewedChunkCoord, chunkSize
                    )); 
                }

            }


        }
    }

    public class TerrainChunk {

        GameObject meshObject;
        Vector2 position;
        Bounds bounds;

        public TerrainChunk(Vector2 coord, int size) {
            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            meshObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
            meshObject.transform.position = positionV3;
            meshObject.transform.localScale = Vector3.one * size / 10f; // Plane is 10*10 

            SetVisible(false);
        }

        public void UpdateTerrainChunk() {
            float viewerDistFromNearestEdge = 
                Mathf.Sqrt(bounds.SqrDistance(viewerPos));
            bool visible = viewerDistFromNearestEdge <= maxViewDistance;

            SetVisible(visible);
        }

        public void SetVisible(bool visible) {
            meshObject.SetActive(visible);
        }

        public bool IsVisible() {
            return meshObject.activeSelf;
        }
    }

}
