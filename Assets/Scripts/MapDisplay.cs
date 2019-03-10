﻿using System;
using System.Collections;
using UnityEngine;

public class MapDisplay : MonoBehaviour {

    public Renderer     renderSurface;
    public MeshFilter   meshFilter;
    public MeshRenderer meshRenderer;

    public void DrawTexture(Texture2D texture) {

        renderSurface.sharedMaterial.mainTexture = texture;
        renderSurface.transform.localScale = new Vector3(
            texture.width, 1, texture.height
        );
    }

    public void DrawMesh(MeshData meshData, Texture2D texture) {
        meshFilter.sharedMesh = meshData.CreateMesh();
        meshRenderer.sharedMaterial.mainTexture = texture;
    }
}
