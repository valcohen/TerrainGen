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
 *      draw Δ clockwise:
 *      [i] Δ i, i+w+1, i+w  [i]__
 *        |\                    \ | 
 *        |_\                    \| Δ i+w+1, i, i+1
 */

public static class MeshGenerator {

    public static MeshData GenerateTerrainMesh(
        float[,] heightMap,
        float heightMultiplier,
        AnimationCurve _heightCurve,
        int levelOfDetail   // higher is simpler
    ) {
        AnimationCurve heightCurve = new AnimationCurve(_heightCurve.keys);
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

        int meshSimplificationIncrement = levelOfDetail == 0
                                        ? 1
                                        : levelOfDetail * 2;
        int verticesPerLine = (width - 1) / meshSimplificationIncrement + 1;

        MeshData meshData = new MeshData(verticesPerLine, verticesPerLine);
        int vertexIndex = 0;

        for (int y = 0; y < height; y += meshSimplificationIncrement) {
            for (int x = 0; x < width; x += meshSimplificationIncrement) {

                meshData.vertices[vertexIndex] = new Vector3(
                    topLeftX + x, 
                    heightCurve.Evaluate(heightMap[x,y]) * heightMultiplier, 
                    topLeftZ - y
                );
                meshData.uvs[vertexIndex] = new Vector2(
                    x / (float) width,
                    y / (float) height
                );  // normalize to 0..1

                // ignore right & bottom edges
                if (x < (width - 1) && y < (height - 1) ) {
                    meshData.AddTriangle(
                        vertexIndex,                        //    i   
                        vertexIndex + verticesPerLine + 1,  //     |\                
                        vertexIndex + verticesPerLine       // i+w |_\ i+w+1             
                    );
                    // we can start 2nd tri at i instead of i+w+1
                    meshData.AddTriangle(
                        vertexIndex,                        //    i __  i+1
                        vertexIndex + 1,                    //      \ | 
                        vertexIndex + verticesPerLine + 1   //       \| i+w+1
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
    public int[]     triangles;
    public Vector2[] uvs;

    int triangleIndex;

    public MeshData(int meshWidth, int meshHeight) {
        vertices    = new Vector3[meshWidth * meshHeight];
        uvs         = new Vector2[meshWidth * meshHeight];
        triangles   = new int[(meshWidth - 1) * (meshHeight - 1) * 6];
    }

    public void AddTriangle(int a, int b, int c) {
        triangles[triangleIndex]     = a;
        triangles[triangleIndex + 1] = b;
        triangles[triangleIndex + 2] = c;
        triangleIndex += 3;
    }

    Vector3[] CalculateNormals() {
        Vector3[] vertexNormals = new Vector3[vertices.Length];
        int triangleCount = triangles.Length / 3;

        for (int i = 0; i < triangleCount; i++) {
            int normalTriangleIndex = i * 3;    // index of tri i in the triangles array
            int vertexIndexA = triangles[normalTriangleIndex];
            int vertexIndexB = triangles[normalTriangleIndex + 1];
            int vertexIndexC = triangles[normalTriangleIndex + 2];

            Vector3 triangleNormal = SufaceNormalFromIndices(
                vertexIndexA, vertexIndexB, vertexIndexC
            );
            vertexNormals[vertexIndexA] += triangleNormal;
            vertexNormals[vertexIndexB] += triangleNormal;
            vertexNormals[vertexIndexC] += triangleNormal;

        }

        for (int i = 0; i < vertexNormals.Length; i++) {
            vertexNormals[i].Normalize();
        }

        return vertexNormals;
    }

    Vector3 SufaceNormalFromIndices(int indexA, int indexB, int indexC) {
        Vector3 pointA = vertices[indexA];
        Vector3 pointB = vertices[indexB];
        Vector3 pointC = vertices[indexC];

        /*
         *  cross-product:
         * 
         *     ^ ab x ac
         *     |
         *     |_   
         *  A  |\|_____ B
         *     \|  ab
         *   ac \ 
         *       \
         *      C
         */
        Vector3 sideAB = pointB - pointA;
        Vector3 sideAC = pointC - pointA;
        return Vector3.Cross(sideAB, sideAC).normalized;
    }

    public Mesh CreateMesh () {
        Mesh mesh = new Mesh();
        mesh.vertices   = vertices;
        mesh.triangles  = triangles;
        mesh.uv         = uvs;
        mesh.normals    = CalculateNormals();

        return mesh;
    }
}
