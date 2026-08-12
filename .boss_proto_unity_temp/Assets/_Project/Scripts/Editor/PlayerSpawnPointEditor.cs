using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerSpawnPoint))]
public sealed class PlayerSpawnPointEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("플레이어 도착 위치", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("spawnPointId"), new GUIContent("Entrance ID", "같은 씬 안에서 중복되지 않는 도착 위치 ID입니다."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("facingMode"), new GUIContent("플레이어 방향"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("showGizmos"), new GUIContent("스폰 위치 미리보기"));
        serializedObject.ApplyModifiedProperties();

        PlayerSpawnPoint point = (PlayerSpawnPoint)target;
        if (GUILayout.Button("테스트 스폰")) TestSpawn(point);
        if (GUILayout.Button("Entrance 검사")) Validate(point);
        EditorGUILayout.HelpBox("빈 Transform 역할만 하며 Collider나 Renderer가 필요하지 않습니다. Edit Mode에서 Move Tool로 배치하고 씬을 저장하세요.", MessageType.Info);
    }

    private static void TestSpawn(PlayerSpawnPoint point)
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Entrance] 테스트 스폰은 Play Mode에서 실행하세요.", point); return; }
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) { Debug.LogError("[Entrance] Player 태그의 플레이어를 찾지 못했습니다.", point); return; }
        player.transform.position = point.transform.position;
        if (player.TryGetComponent(out Rigidbody body)) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        Physics.SyncTransforms();
        Debug.Log($"[Entrance] 플레이어를 {point.SpawnPointId} 위치로 이동했습니다.", point);
    }

    private static void Validate(PlayerSpawnPoint point)
    {
        PlayerSpawnPoint[] duplicates = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(item => item.Matches(point.SpawnPointId)).ToArray();
        if (duplicates.Length > 1) Debug.LogError($"[Entrance] Entrance ID가 중복되었습니다: {point.SpawnPointId}", point);
        Collider[] overlaps = Physics.OverlapSphere(point.transform.position, 0.15f, ~0, QueryTriggerInteraction.Collide);
        if (overlaps.Any(item => item.isTrigger)) Debug.LogWarning("[Entrance] 스폰 위치가 출구 Trigger 안에 있습니다.", point);
        if (overlaps.Any(item => !item.isTrigger)) Debug.LogWarning("[Entrance] 스폰 위치가 바닥 또는 벽 Collider 내부에 있을 수 있습니다.", point);
        if (duplicates.Length <= 1 && overlaps.Length == 0) Debug.Log("[Entrance] Entrance 설정이 정상입니다. Transform은 변경하지 않았습니다.", point);
    }
}
