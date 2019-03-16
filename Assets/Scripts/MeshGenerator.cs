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
        float[,]        heightMap,
        float           heightMultiplier,
        AnimationCurve  _heightCurve,
        int             levelOfDetail,  // higher is simpler
        bool            useFlatShading
    ) {
        AnimationCurve heightCurve = new AnimationCurve(_heightCurve.keys);

        int meshSimplificationIncrement = levelOfDetail == 0
                                        ? 1
                                        : levelOfDetail * 2;

        /*
         *     -1   -2   -3   -4   -5   -n : border vertices
         * 
         *     -6   [0]  [1]  [3]  -7   [n]: mesh vertices
         * 
         *     -8   [3]  [4]  [5]  -9
         * 
         *    -10   [6]  [7]  [8] -11
         * 
         *    -12  -13  -14  -15  -16
         * 
         *    |     |__ mesh ___|   |
         *    |         size        |   meshSize = borderedSize - 2
         *    |___ bordered size ___|
         */

        int borderedSize  = heightMap.GetLength(0);
        int meshSize = borderedSize - 2 * meshSimplificationIncrement;
        int meshSizeUnsimplified = borderedSize - 2;

        /*
         * center the mesh on the screen:
         *   *   *   *
         *  -1   0   1
         * 
         *      (width - 1)     (width - 1)
         *  x = ----------- y = -----------
         *          -2               2
         */
        float topLeftX = (meshSizeUnsimplified - 1) / -2f;
        float topLeftZ = (meshSizeUnsimplified - 1) / +2f;

        int verticesPerLine = (meshSize - 1) / meshSimplificationIncrement + 1;

        MeshData meshData = new MeshData(verticesPerLine, useFlatShading);

        int[,] vertexIndicesMap = new int[borderedSize, borderedSize];
        int meshVertexIndex     = 0;
        int borderVertexIndex   = -1;

        for (int y = 0; y < borderedSize; y += meshSimplificationIncrement) {
            for (int x = 0; x < borderedSize; x += meshSimplificationIncrement) {
                bool isBorderVertex = y == 0 || y == borderedSize - 1
                                   || x == 0 || x == borderedSize - 1;

                if (isBorderVertex) {
                    vertexIndicesMap[x, y] = borderVertexIndex;
                    borderVertexIndex--;
                } else {
                    vertexIndicesMap[x, y] = meshVertexIndex;
                    meshVertexIndex++;
                }
            }
        }

        for (int y = 0; y < borderedSize; y += meshSimplificationIncrement) {
            for (int x = 0; x < borderedSize; x += meshSimplificationIncrement) {
                int vertexIndex = vertexIndicesMap[x, y];

                /*
                 * Make sure uvs still properly centered by subtracting 
                 * meshSimplificationIncrement. 
                 * On left edge of the map, we want percent to eval to 0,
                 * but when x = 0 it will be part of border, 
                 * which is being left out of the mesh.
                 * The value after x = 0 is x + meshsimplification increment
                 */
                Vector2 percent = new Vector2(
                    (x - meshSimplificationIncrement) / (float)meshSize,
                    (y - meshSimplificationIncrement) / (float)meshSize
                );  // normalize to 0..1

                float height = heightCurve.Evaluate(heightMap[x, y]) * heightMultiplier;
                Vector3 vertexPosition = new Vector3(
                    topLeftX + percent.x * meshSizeUnsimplified, 
                    height, 
                    topLeftZ - percent.y * meshSizeUnsimplified
                );

                meshData.AddVertex(vertexPosition, percent, vertexIndex);

                /*
                 * ignore right & bottom edges
                 *       
                 *   (x;y) a *--* b (x+i;y)   i = meshSimplificationIncrement
                 *           |\ |
                 *           | \|
                 * (x;y+i) c *--* d (x+i;y+i)  
                 * 
                 * Δ adc, Δ dab (abd?)
                 */
                if (x < (borderedSize - 1) && y < (borderedSize - 1) ) {
                    int a = vertexIndicesMap[x, y];
                    int b = vertexIndicesMap[x + meshSimplificationIncrement, y];
                    int c = vertexIndicesMap[x, y + meshSimplificationIncrement];
                    int d = vertexIndicesMap[x + meshSimplificationIncrement,
                                             y + meshSimplificationIncrement];
                    meshData.AddTriangle(a, d, c);
                    meshData.AddTriangle(d, a, b);
                }

                vertexIndex++;
            }
        }

        meshData.Finalize();

        return meshData;
    }
}

public class MeshData {
    Vector3[] vertices;
    int[]     triangles;
    Vector2[] uvs;
    Vector3[] bakedNormals;

    Vector3[] borderVertices;
    int[] borderTriangles;

    int triangleIndex;
    int borderTriangleIndex;

    bool useFlatShading;

    public MeshData(int verticesPerLine, bool useFlatSHading) {
        this.useFlatShading = useFlatSHading;

        vertices    = new Vector3[verticesPerLine * verticesPerLine];
        uvs         = new Vector2[verticesPerLine * verticesPerLine];
        triangles   = new int[(verticesPerLine - 1) * (verticesPerLine - 1) * 6];

        borderVertices = new Vector3[verticesPerLine * 4 /* borders */ + 4 /*corners*/];
        // hold indices of the 6 vertices that make up the two trianges per square;
        // number of squares in the border = 4 * verticesPerLine in the mesh
        borderTriangles = new int[6 * 4 * verticesPerLine];    
    }

    public void AddVertex(Vector3 vertexPosition, Vector2 uv, int vertexIndex) {
        if (vertexIndex < 0) {  // border vertex
            // border indices start at -1; invert and add 1 to start at 0
            borderVertices[-vertexIndex - 1] = vertexPosition;
        } else {
            vertices[vertexIndex] = vertexPosition;
            uvs[vertexIndex] = uv;
        }
    }

    public void AddTriangle(int a, int b, int c) {
        if (a < 0 || b < 0 || c < 0) {  // border triangle
            borderTriangles[borderTriangleIndex] = a;
            borderTriangles[borderTriangleIndex + 1] = b;
            borderTriangles[borderTriangleIndex + 2] = c;
            borderTriangleIndex += 3;
        } else {
            triangles[triangleIndex] = a;
            triangles[triangleIndex + 1] = b;
            triangles[triangleIndex + 2] = c;
            triangleIndex += 3;
        }
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

        int borderTriangleCount = borderTriangles.Length / 3;
        for (int i = 0; i < borderTriangleCount; i++) {
            int normalTriangleIndex = i * 3;
            int vertexIndexA = borderTriangles[normalTriangleIndex];
            int vertexIndexB = borderTriangles[normalTriangleIndex + 1];
            int vertexIndexC = borderTriangles[normalTriangleIndex + 2];

            Vector3 triangleNormal = SufaceNormalFromIndices(
                vertexIndexA, vertexIndexB, vertexIndexC
            );
            if (vertexIndexA >= 0) { vertexNormals[vertexIndexA] += triangleNormal; }
            if (vertexIndexB >= 0) { vertexNormals[vertexIndexB] += triangleNormal; }
            if (vertexIndexC >= 0) { vertexNormals[vertexIndexC] += triangleNormal; }
        }

        for (int i = 0; i < vertexNormals.Length; i++) {
            vertexNormals[i].Normalize();
        }

        return vertexNormals;
    }

    Vector3 SufaceNormalFromIndices(int indexA, int indexB, int indexC) {
        Vector3 pointA = indexA < 0 ? borderVertices[-indexA - 1] : vertices[indexA];
        Vector3 pointB = indexB < 0 ? borderVertices[-indexB - 1] : vertices[indexB];
        Vector3 pointC = indexC < 0 ? borderVertices[-indexC - 1] : vertices[indexC];

        /*
         * cross-product:  ^ ab x ac
         *                 |
         *                 |_   
         *               A |\|_____ B
         *                 \|  ab
         *               ac \ 
         *                   \
         *                   C 
         */
        Vector3 sideAB = pointB - pointA;
        Vector3 sideAC = pointC - pointA;
        return Vector3.Cross(sideAB, sideAC).normalized;
    }

    public void Finalize() {
        if ( useFlatShading ) {
            FlatShading();
        } else {
            BakeNormals();
        }
    }

    void BakeNormals() {
        bakedNormals = CalculateNormals();
    }

    /*
     * Create new vertices so each tri has its own set, rather than sharing
     * with adjacent triangles. With its own 3 vertices, each triangle can now 
     * orient their normals in the same direction, so lighting is applied 
     * equally across the triangle.
     */
    void FlatShading() {
        Vector3[] flatShadedVertices = new Vector3[triangles.Length];
        Vector2[] flatShadedUVs = new Vector2[triangles.Length];

        for (int i = 0; i < triangles.Length; i++) {
            flatShadedVertices[i] = vertices[triangles[i]];
            flatShadedUVs[i] = uvs[triangles[i]];
            triangles[i] = i;   // index of new flat-shaded vertex
        }

        vertices = flatShadedVertices;
        uvs = flatShadedUVs;
    }

    public Mesh CreateMesh () {
        Mesh mesh = new Mesh();
        mesh.vertices   = vertices;
        mesh.triangles  = triangles;
        mesh.uv         = uvs;
        if (useFlatShading) {
            mesh.RecalculateNormals();  // built-in
        } else {
            mesh.normals = bakedNormals;
        }

        return mesh;
    }
}
