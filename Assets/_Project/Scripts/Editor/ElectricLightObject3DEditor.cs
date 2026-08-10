#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(ElectricLightObject3D))]
public sealed class ElectricLightObject3DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "currentState",
            "currentHP",
            "lastDamageResult",
            "gameplayLightActive");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime Debug (Read Only)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("currentState"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("currentHP"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lastDamageResult"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gameplayLightActive"));
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
