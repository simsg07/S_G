using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SceneConnectionsAuthoring))]
public sealed class SceneConnectionsAuthoringEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Entrance와 Exit의 Transform은 현재 씬에 저장됩니다. 아래 검사 버튼은 위치를 변경하지 않습니다.", MessageType.Info);
        if (GUILayout.Button("연결 검사")) FirstFiveStageConnectionSetup.ValidateConnections();
        if (GUILayout.Button("누락된 기본 구조 생성")) SceneConnectionsPrefabBuilder.CreateMissingForSelectedRoot();
        if (GUILayout.Button("Build Profile 씬 등록 확인")) FirstFiveStageConnectionSetup.ValidateConnections();
    }
}
