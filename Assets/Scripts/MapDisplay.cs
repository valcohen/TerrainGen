using System.Collections;
using UnityEngine;

public class MapDisplay : MonoBehaviour {

    public Renderer renderSurface;

    public void DrawTexture(Texture2D texture) {

        renderSurface.sharedMaterial.mainTexture = texture;
        renderSurface.transform.localScale = new Vector3(
            texture.width, 1, texture.height
        );
    }
}
