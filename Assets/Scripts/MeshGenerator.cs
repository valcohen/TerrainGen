using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 *      triangles array (indices of verts used by each triangle):
 *      0,4,3, 4,0,1, 1,5,4, 5,1,2 ...
 * 
 *      [0]  __[1]  __ 2          
 *        |\ \ | |\ \ |   # of verts = w * h
 *        |_\ \| |_\ \|   # of verts used by tris = (w-1)(h-1) * (2*3)
 *      [3]  __[4]  __ 5                 (2*3 = 2 tris * 3 verts each)
 *        |\ \ | |\ \ |
 *        |_\ \| |_\ \|     create tris only for [v] vertices, 
 *       6      7      8    not for right or bottom edges:
 * 
 *        i   __ i+1    i+2...
 *         |\ \ |
 *         |_\ \|
 *      i+w   i+w+1  i+w+2...
 * 
 *      [i] i, i+w+1, i+w    [i]__
 *        |\                    \ | 
 *        |_\                    \| i+w+1, i, i+1
 */

public static class MeshGenerator {

    public static MeshData GenerateTerrainMesh(float[,] heightMap) {
        int width  = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);

        /*
         * center the mesh on the screen:
         *   *   *   *
         *  -1   0   1
         * 
         *      (width - 1)     (width - 1)
         *  x = ----------- y = -----------
         *          -2               2
         */
        float topLeftX = (width  - 1) / -2f;
        float topLeftZ = (height - 1) / +2f;


        MeshData meshData = new MeshData(width, height);
        int vertexIndex = 0;

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {

                meshData.vertices[vertexIndex] = new Vector3(
                    topLeftX + x, heightMap[x,y], topLeftZ - y
                );
                meshData.uvs[vertexIndex] = new Vector2(
                    x / (float) width,
                    y / (float) height
                );  // normalize to 0..1

                // ignore right & bottom edges
                if (x < (width -1) && y < (height - 1) ) {
                    meshData.AddTriangle(
                        vertexIndex, 
                        vertexIndex + width + 1,
                        vertexIndex + width
                    );
                    // TODO: what if we start at i instead of i+w+1?
                    meshData.AddTriangle(
                        vertexIndex + width + 1,
                        vertexIndex,
                        vertexIndex + 1
                    );
                }

                vertexIndex++;
            }
        }

        return meshData;
    }
}

public class MeshData {
    public Vector3[] vertices;
    public int[] triangles;
    public Vector2[] uvs;

    int triangleIndex;

    public MeshData(int meshWidth, int meshHeight) {
        vertices  = new Vector3[meshWidth * meshHeight];
        uvs = new Vector2[meshWidth * meshHeight];
        triangles = new int[(meshWidth - 1) * (meshHeight - 1) * 6];
    }

    public void AddTriangle(int a, int b, int c) {
        triangles[triangleIndex]     = a;
        triangles[triangleIndex + 1] = b;
        triangles[triangleIndex + 2] = c;
        triangleIndex += 3;
    }

    public Mesh CreateMesh () {
        Mesh mesh = new Mesh();
        mesh.vertices   = vertices;
        mesh.triangles  = triangles;
        mesh.uv         = uvs;
        mesh.RecalculateNormals();

        return mesh;
    }
}
