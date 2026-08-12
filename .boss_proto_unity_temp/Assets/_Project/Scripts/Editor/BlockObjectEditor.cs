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
    private SerializedProperty worldRole;
    private SerializedProperty temporalProgressKey;
    private SerializedProperty debugPastDestroyed;
    private SerializedProperty debugCurrentDestroyed;

    private void OnEnable()
    {
        currentState = serializedObject.FindProperty("currentState");
        debugPersistentId = serializedObject.FindProperty("debugPersistentId");
        loadedPersistentDestroyed = serializedObject.FindProperty("loadedPersistentDestroyed");
        lastStateApplyReason = serializedObject.FindProperty("lastStateApplyReason");
        worldRole = serializedObject.FindProperty("worldRole");
        temporalProgressKey = serializedObject.FindProperty("temporalProgressKey");
        debugPastDestroyed = serializedObject.FindProperty("debugPastDestroyed");
        debugCurrentDestroyed = serializedObject.FindProperty("debugCurrentDestroyed");
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
            "lastStateApplyReason",
            "debugPastDestroyed",
            "debugCurrentDestroyed");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime Debug (Read Only)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(debugPersistentId, new GUIContent("Persistent Id"));
            EditorGUILayout.PropertyField(worldRole, new GUIContent("World Role"));
            EditorGUILayout.PropertyField(temporalProgressKey, new GUIContent("Temporal Progress Key"));
            EditorGUILayout.PropertyField(debugPastDestroyed, new GUIContent("PastDestroyed Saved"));
            EditorGUILayout.PropertyField(debugCurrentDestroyed, new GUIContent("CurrentDestroyed Saved"));
            EditorGUILayout.PropertyField(loadedPersistentDestroyed, new GUIContent("Loaded Persistent Destroyed"));
            EditorGUILayout.PropertyField(currentState, new GUIContent("Current WeakWall State"));
            EditorGUILayout.PropertyField(lastStateApplyReason, new GUIContent("Last State Apply Reason"));
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
