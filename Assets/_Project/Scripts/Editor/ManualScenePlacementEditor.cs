using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(ManualScenePlacement))]
public sealed class ManualScenePlacementEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "이 오브젝트의 Transform은 씬 인스턴스 배치 데이터입니다. 자동 생성 도구는 기존 Transform을 변경하지 않습니다.",
            MessageType.Info);

        if (GUILayout.Button("Auto Place Selected Object"))
        {
            Selection.activeGameObject = ((ManualScenePlacement)target).gameObject;
            FirstFiveStageConnectionSetup.AutoPlaceSelectedObject();
        }

        if (GUILayout.Button("Save Current Entrance Position"))
        {
            SaveCurrentPosition();
        }

        if (GUILayout.Button("Validate Connections"))
        {
            FirstFiveStageConnectionSetup.ValidateConnections();
        }
    }

    private void SaveCurrentPosition()
    {
        ManualScenePlacement placement = (ManualScenePlacement)target;
        if (Application.isPlaying)
        {
            Debug.LogWarning("[ManualScenePlacement] Play Mode 위치는 저장하지 않습니다. Edit Mode에서 위치를 조정한 뒤 이 버튼을 사용하세요.", placement);
            return;
        }

        Undo.RecordObject(placement.transform, "Save Connection Object Position");
        PrefabUtility.RecordPrefabInstancePropertyModifications(placement.transform);
        EditorUtility.SetDirty(placement.transform);
        EditorSceneManager.MarkSceneDirty(placement.gameObject.scene);
        EditorSceneManager.SaveScene(placement.gameObject.scene);
        Debug.Log($"[ManualScenePlacement] Saved scene Transform for '{placement.name}'.", placement);
    }
}
