using UnityEditor;

[CustomEditor(typeof(CustomRenderPipelineAsset))]
public class CustomRenderPipelineAssetEditor : Editor
{
    SerializedProperty shadowCascades;
    SerializedProperty twoCascadesSplit;
    SerializedProperty fourCascadesSplit;

    void OnEnable()
    {
        shadowCascades = serializedObject.FindProperty("shadowCascades");
        fourCascadesSplit = serializedObject.FindProperty("fourCascadesSplit");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        switch (shadowCascades.enumValueIndex)
        {
            case 0: return;
            case 1:
                fourCascadesSplit.vector3Value = EditorGUILayout.Vector3Field("Four Cascades Split", fourCascadesSplit.vector3Value);
                break;
        }
        serializedObject.ApplyModifiedProperties();
    }
}
