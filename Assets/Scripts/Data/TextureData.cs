using System.Collections;
using UnityEngine;

[CreateAssetMenu]
public class TextureData : UpdateableData {

	public void ApplyToMaterial(Material material) {
	
	}

    public void UpdateMeshHeights(Material material, float minHeight, float maxHeight) {
        material.SetFloat("minHeight", minHeight);
        material.SetFloat("maxHeight", maxHeight);
    }

}
