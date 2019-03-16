using System.Collections;
using UnityEngine;

[CreateAssetMenu]
public class TextureData : UpdateableData {

    float savedMinHeight;
    float savedMaxHeight;

	public void ApplyToMaterial(Material material) {

        UpdateMeshHeights(material, savedMinHeight, savedMaxHeight);
	}

    public void UpdateMeshHeights(Material material, float minHeight, float maxHeight) {

        savedMinHeight = minHeight;
        savedMaxHeight = maxHeight;

        material.SetFloat("minHeight", minHeight);
        material.SetFloat("maxHeight", maxHeight);
    }

}
