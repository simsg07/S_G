using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FocusingSpawner3D))]
public sealed class FocusingSpawner3DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            bool isScript = iterator.propertyPath == "m_Script";
            bool isHiddenPolicy = iterator.propertyPath == "hiddenWorldSimulationPolicy";
            bool isMonster = serializedObject.FindProperty("spawnerType").enumValueIndex == (int)FocusingSpawnerType.Monster;
            using (new EditorGUI.DisabledScope(isScript || (isHiddenPolicy && !isMonster)))
                EditorGUILayout.PropertyField(iterator, true);

            if (isHiddenPolicy)
            {
                FocusingSpawner3D spawner = (FocusingSpawner3D)target;
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField("Applied World Summary", spawner.AppliedWorldSummary);
            }
        }
        serializedObject.ApplyModifiedProperties();
    }
}
