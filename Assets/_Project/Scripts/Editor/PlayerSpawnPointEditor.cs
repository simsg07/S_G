using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerSpawnPoint))]
public class PlayerSpawnPointEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        PlayerSpawnPoint spawnPoint = (PlayerSpawnPoint)target;
        if (GUILayout.Button("Validate Spawn Point"))
        {
            spawnPoint.ValidateSpawnPoint();
            EditorUtility.SetDirty(spawnPoint);
        }

        EditorGUILayout.HelpBox(
            "spawnPointId는 같은 씬 안에서 중복되지 않아야 합니다.\n" +
            "StageExitTrigger.targetSpawnPointId와 정확히 같은 문자열을 사용하세요.",
            MessageType.Info);
    }
}
