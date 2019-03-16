﻿using System.Collections;
using UnityEngine;

[CreateAssetMenu]
public class TerrainData : UpdateableData {

    public bool useFlatShading;
    public bool useFalloff;

    public float uniformScale = 2;
    public float meshHeightMultiplier;
    public AnimationCurve meshHeightCurve;



}
