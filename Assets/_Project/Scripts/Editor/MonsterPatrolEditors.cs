#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(MonsterPatrolController))]
public sealed class MonsterPatrolControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (GUILayout.Button("순찰 경로 생성")) CreatePath();
        if (GUILayout.Button("포인트 추가")) AddPoint(null);
        if (GUILayout.Button("선택 포인트 뒤에 추가")) AddPoint(Selection.activeTransform);
        if (GUILayout.Button("마지막 포인트 삭제")) DeleteLastPoint();
        if (GUILayout.Button("포인트 번호 다시 정렬")) Renumber();
        if (GUILayout.Button("순찰 경로 검사")) ValidatePath();
    }

    private MonsterPatrolController Controller => (MonsterPatrolController)target;

    private void CreatePath()
    {
        if (Controller.PatrolPath != null)
        {
            EditorUtility.DisplayDialog("순찰 경로", "이미 연결된 경로가 있습니다. 기존 포인트는 변경하지 않았습니다.", "확인");
            return;
        }

        GameObject pathObject = new GameObject($"{Controller.name}_PatrolPath");
        Undo.RegisterCreatedObjectUndo(pathObject, "Create Monster Patrol Path");
        pathObject.transform.SetParent(Controller.transform.parent, true);
        pathObject.transform.position = Vector3.zero;
        MonsterPatrolPath path = Undo.AddComponent<MonsterPatrolPath>(pathObject);
        SerializedObject serialized = new SerializedObject(Controller);
        serialized.FindProperty("patrolPath").objectReferenceValue = path;
        serialized.ApplyModifiedProperties();
        CreatePoint(path, Controller.transform.position);
        CreatePoint(path, Controller.transform.position + Vector3.right * 2f);
        MarkDirty(path.gameObject);
        Selection.activeGameObject = pathObject;
    }

    private void AddPoint(Transform selected)
    {
        MonsterPatrolPath path = Controller.PatrolPath;
        if (path == null) { CreatePath(); return; }
        int insertIndex = path.PointCount;
        if (selected != null && selected.parent == path.transform) insertIndex = selected.GetSiblingIndex() + 1;
        Vector3 position = selected != null && selected.parent == path.transform
            ? selected.position + Vector3.right
            : (path.PointCount > 0 ? path.GetPoint(path.PointCount - 1).position + Vector3.right : Controller.transform.position);
        Transform point = CreatePoint(path, position);
        point.SetSiblingIndex(insertIndex);
        Renumber();
        Selection.activeTransform = point;
    }

    private static Transform CreatePoint(MonsterPatrolPath path, Vector3 position)
    {
        GameObject point = new GameObject("Point_00");
        Undo.RegisterCreatedObjectUndo(point, "Add Monster Patrol Point");
        point.transform.SetParent(path.transform, true);
        point.transform.position = position;
        return point.transform;
    }

    private void DeleteLastPoint()
    {
        MonsterPatrolPath path = Controller.PatrolPath;
        if (path == null || path.PointCount == 0) return;
        Undo.DestroyObjectImmediate(path.GetPoint(path.PointCount - 1).gameObject);
        MarkDirty(path.gameObject);
    }

    private void Renumber()
    {
        MonsterPatrolPath path = Controller.PatrolPath;
        if (path == null) return;
        for (int i = 0; i < path.PointCount; i++)
        {
            Transform point = path.GetPoint(i);
            Undo.RecordObject(point.gameObject, "Renumber Monster Patrol Points");
            point.name = $"Point_{i:00}";
        }
        MarkDirty(path.gameObject);
    }

    private void ValidatePath()
    {
        MonsterPatrolPath path = Controller.PatrolPath;
        if (path == null || path.PointCount < 2)
            Debug.LogWarning($"[{Controller.name}] 순찰 경로에는 최소 2개 포인트가 필요합니다.", Controller);
        else
            Debug.Log($"[{Controller.name}] 순찰 경로 검사 완료: {path.PointCount} points. 위치는 변경하지 않았습니다.", Controller);
    }

    private static void MarkDirty(GameObject gameObject)
    {
        EditorUtility.SetDirty(gameObject);
        if (gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
}

[CustomEditor(typeof(MonsterPatrolPath))]
public sealed class MonsterPatrolPathEditor : Editor
{
    private void OnSceneGUI()
    {
        MonsterPatrolPath path = (MonsterPatrolPath)target;
        if (!path.ShowPatrolGizmos) return;
        for (int i = 0; i < path.PointCount; i++)
        {
            Transform point = path.GetPoint(i);
            if (point == null) continue;
            Handles.color = new Color(1f, 0.72f, 0.12f, 1f);
            Handles.Label(point.position + Vector3.up * 0.25f, i.ToString());
        }
    }
}
#endif
