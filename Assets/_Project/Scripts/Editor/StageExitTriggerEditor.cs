using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StageExitTrigger))]
public class StageExitTriggerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        StageExitTrigger trigger = (StageExitTrigger)target;
        if (GUILayout.Button("Validate Scene Connection"))
        {
            trigger.ValidateSceneConnection();
            EditorUtility.SetDirty(trigger);
        }

        EditorGUILayout.HelpBox(
            "각 출구마다 nextSceneName과 targetSpawnPointId를 따로 설정하세요.\n" +
            "targetSpawnPointId는 이동할 대상 씬의 PlayerSpawnPoint.spawnPointId와 같아야 합니다.",
            MessageType.Info);
    }
}
