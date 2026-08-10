#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BlockObject))]
public sealed class BlockObjectEditor : Editor
{
    private SerializedProperty currentState;
    private SerializedProperty debugPersistentId;
    private SerializedProperty loadedPersistentDestroyed;
    private SerializedProperty lastStateApplyReason;

    private void OnEnable()
    {
        currentState = serializedObject.FindProperty("currentState");
        debugPersistentId = serializedObject.FindProperty("debugPersistentId");
        loadedPersistentDestroyed = serializedObject.FindProperty("loadedPersistentDestroyed");
        lastStateApplyReason = serializedObject.FindProperty("lastStateApplyReason");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "currentState",
            "debugPersistentId",
            "loadedPersistentDestroyed",
            "lastStateApplyReason");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime Debug (Read Only)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(debugPersistentId, new GUIContent("Persistent Id"));
            EditorGUILayout.PropertyField(loadedPersistentDestroyed, new GUIContent("Loaded Persistent Destroyed"));
            EditorGUILayout.PropertyField(currentState, new GUIContent("Current WeakWall State"));
            EditorGUILayout.PropertyField(lastStateApplyReason, new GUIContent("Last State Apply Reason"));
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
