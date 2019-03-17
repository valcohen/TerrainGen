using System.Collections;
using UnityEngine;

[CreateAssetMenu]
public class TerrainData : UpdateableData {

    public bool useFlatShading;
    public bool useFalloff;

    public float uniformScale = 2;
    public float meshHeightMultiplier;
    public AnimationCurve meshHeightCurve;

    public float minHeight {
        get {
            return uniformScale * meshHeightMultiplier *
                meshHeightCurve.Evaluate(0);
        }
    }

    public float maxHeight {
        get {
            return uniformScale * meshHeightMultiplier *
                meshHeightCurve.Evaluate(1);
        }
    }

}
