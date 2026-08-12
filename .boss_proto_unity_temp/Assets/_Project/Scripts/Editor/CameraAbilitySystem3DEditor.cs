using UnityEditor;

[CustomEditor(typeof(CameraAbilitySystem3D))]
public sealed class CameraAbilitySystem3DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            bool readOnly = property.name.StartsWith("runtime");
            using (new EditorGUI.DisabledScope(readOnly || property.name == "m_Script"))
            {
                EditorGUILayout.PropertyField(property, true);
            }
        }
        serializedObject.ApplyModifiedProperties();
    }
}
