using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StageExitTrigger))]
public sealed class StageExitTriggerEditor : Editor
{
    private static readonly string[] StandardEntrances =
    {
        "LeftEntrance", "RightEntrance", "UpperRightEntrance", "CenterRightEntrance", "LowerRightEntrance"
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        StageExitTrigger trigger = (StageExitTrigger)target;

        EditorGUILayout.LabelField("씬 출구 연결", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("connectionEnabled"), new GUIContent("연결 사용", "끄면 이 출구는 작동하지 않습니다."));
        SerializedProperty sceneProperty = serializedObject.FindProperty("targetScene");
        EditorGUILayout.PropertyField(sceneProperty, new GUIContent("이동할 씬", "Project 창의 SceneAsset을 지정하세요. 씬 이름 문자열은 자동 관리됩니다."));

        SceneAsset sceneAsset = sceneProperty.objectReferenceValue as SceneAsset;
        List<string> entrances = GetEntranceIds(sceneAsset);
        string currentEntrance = serializedObject.FindProperty("targetSpawnPointId").stringValue;
        if (!string.IsNullOrEmpty(currentEntrance) && !entrances.Contains(currentEntrance)) entrances.Add(currentEntrance);
        if (entrances.Count == 0) entrances.AddRange(StandardEntrances);
        int selected = Mathf.Max(0, entrances.IndexOf(currentEntrance));
        selected = EditorGUILayout.Popup(new GUIContent("도착 위치", "대상 씬에 저장된 Entrance ID입니다."), selected, entrances.ToArray());
        serializedObject.FindProperty("targetSpawnPointId").stringValue = entrances[selected];

        SerializedProperty interaction = serializedObject.FindProperty("useInteractionKey");
        interaction.boolValue = EditorGUILayout.Popup("작동 방식", interaction.boolValue ? 1 : 0, new[] { "닿으면 바로 이동", "상호작용 키를 누르면 이동" }) == 1;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredPlayerTag"), new GUIContent("플레이어 태그"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("useFade"), new GUIContent("페이드 사용", "프로젝트에 페이드 처리기가 연결된 경우 사용합니다."));
        if (interaction.boolValue)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("interactionKey"), new GUIContent("상호작용 키"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("onInteractionAvailable"), new GUIContent("안내 UI 표시 이벤트"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("onInteractionUnavailable"), new GUIContent("안내 UI 숨김 이벤트"));
        }

        EditorGUILayout.Space(6f);
        DrawTriggerCollider(trigger);
        serializedObject.ApplyModifiedProperties();

        if (GUILayout.Button("연결 검사")) ValidateKorean(trigger, sceneAsset, GetEntranceIds(sceneAsset));
        if (GUILayout.Button("선택한 출구 테스트")) TestSelectedExit(trigger);
        if (GUILayout.Button("Build Profile 씬 등록 확인")) CheckBuildRegistration(sceneAsset);

        EditorGUILayout.HelpBox("Transform은 씬에 저장되는 수동 배치 값입니다. 검사와 Inspector 변경은 위치를 이동시키지 않습니다.", MessageType.Info);
    }

    private static void DrawTriggerCollider(StageExitTrigger trigger)
    {
        BoxCollider collider = trigger.GetComponent<BoxCollider>();
        if (collider == null)
        {
            EditorGUILayout.HelpBox("Exit에 Box Collider가 없습니다.", MessageType.Error);
            return;
        }
        SerializedObject colliderData = new SerializedObject(collider);
        colliderData.Update();
        EditorGUILayout.LabelField("출구 Trigger 범위", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(colliderData.FindProperty("m_Center"), new GUIContent("중심"));
        EditorGUILayout.PropertyField(colliderData.FindProperty("m_Size"), new GUIContent("크기"));
        colliderData.ApplyModifiedProperties();
    }

    private static List<string> GetEntranceIds(SceneAsset scene)
    {
        var result = new List<string>();
        if (scene == null) return result;
        string path = AssetDatabase.GetAssetPath(scene);
        if (!File.Exists(path)) return result;
        string yaml = File.ReadAllText(path);
        foreach (Match match in Regex.Matches(yaml, @"spawnPointId:\s*([^\r\n]+)"))
        {
            string id = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(id) && !result.Contains(id)) result.Add(id);
        }
        return result.OrderBy(EntranceOrder).ThenBy(id => id).ToList();
    }

    private static int EntranceOrder(string id)
    {
        int index = System.Array.IndexOf(StandardEntrances, id);
        return index >= 0 ? index : 100;
    }

    private static void ValidateKorean(StageExitTrigger trigger, SceneAsset scene, List<string> entrances)
    {
        bool valid = true;
        if (scene == null) { Debug.LogError("[씬 연결] 이동할 씬이 선택되지 않았습니다.", trigger); valid = false; }
        else if (!IsRegistered(scene)) { Debug.LogError("[씬 연결] 해당 씬이 Build Profile에 등록되지 않았습니다.", trigger); valid = false; }
        if (!entrances.Contains(trigger.TargetSpawnPointId)) { Debug.LogError($"[씬 연결] 대상 씬에 {trigger.TargetSpawnPointId}가 없습니다.", trigger); valid = false; }
        BoxCollider box = trigger.GetComponent<BoxCollider>();
        if (box == null || !box.isTrigger) { Debug.LogError("[씬 연결] Exit Collider가 Trigger로 설정되지 않았습니다.", trigger); valid = false; }
        else
        {
            Vector3 center = trigger.transform.TransformPoint(box.center);
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, trigger.transform.lossyScale);
            Collider[] overlaps = Physics.OverlapBox(center, halfExtents, trigger.transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            if (overlaps.Any(item => item != null && item != box))
            {
                Debug.LogWarning("[씬 연결] Exit가 벽 또는 다른 충돌체 안에 묻혀 있을 수 있습니다. Scene 뷰에서 Trigger 범위를 확인하세요.", trigger);
            }
        }
        if (valid) Debug.Log("[씬 연결] 연결 설정이 정상입니다. Transform은 변경하지 않았습니다.", trigger);
    }

    private static void TestSelectedExit(StageExitTrigger trigger)
    {
        if (!Application.isPlaying) { Debug.LogWarning("[씬 연결] 출구 테스트는 Play Mode에서 실행하세요.", trigger); return; }
        trigger.ValidateSceneConnection();
    }

    private static void CheckBuildRegistration(SceneAsset scene)
    {
        if (scene == null) { Debug.LogError("[씬 연결] 이동할 씬이 선택되지 않았습니다."); return; }
        Debug.Log(IsRegistered(scene) ? "[씬 연결] 대상 씬이 Build Profile에 등록되어 있습니다." : "[씬 연결] 해당 씬이 Build Profile에 등록되지 않았습니다.", scene);
    }

    private static bool IsRegistered(SceneAsset scene)
    {
        string path = AssetDatabase.GetAssetPath(scene);
        return EditorBuildSettings.scenes.Any(item => item.enabled && item.path == path);
    }
}
