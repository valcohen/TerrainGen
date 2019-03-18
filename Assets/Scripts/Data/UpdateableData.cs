using System.Collections;
using UnityEngine;

[CreateAssetMenu]
public class UpdateableData : ScriptableObject {

    public event System.Action OnValuesUpdated;
    public bool autoUpdate;

#if UNITY_EDITOR

    protected virtual void OnValidate() {
        if (autoUpdate) {
            // delay updating min/maxMeshHeight until after both the scripts 
            // *and* the shader compile, or else the values get dropped when the
            // shader compiles.
            UnityEditor.EditorApplication.update += NotifyOfUpdatedValues;
        }
    }

    public void NotifyOfUpdatedValues() {
        UnityEditor.EditorApplication.update -= NotifyOfUpdatedValues;
        if (OnValuesUpdated != null) {
            OnValuesUpdated();
        }
    }

#endif
}
