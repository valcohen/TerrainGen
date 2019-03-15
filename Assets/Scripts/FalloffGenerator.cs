﻿using System.Collections;
using UnityEngine;

public static class FalloffGenerator {

    public static float[,] GenerateFalloffMap(int size) {
        float[,] map = new float[size, size];

        for (int i = 0; i < size; i++) {
            for (int j = 0; j < size; j++) {
                float x = i / (float)size * 2 - 1;  // 0..1 -> -1..1
                float y = j / (float)size * 2 - 1;

                // of x,y, which is closest to edge of square?
                float value = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                map[i, j] = value;
            }
        }

        return map;
    }
}
