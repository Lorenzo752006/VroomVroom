using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoadMeshGenerator))]
public class RoadMeshGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        RoadMeshGenerator generator = (RoadMeshGenerator)target;
        EditorGUILayout.Space();

        if (GUILayout.Button("Rebuild Road Mesh"))
        {
            generator.Rebuild();
            EditorUtility.SetDirty(generator);
        }
    }
}
