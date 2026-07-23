using UnityEditor;
using UnityEngine;

/// <summary>
/// Compatibility entry point for the removed Tilemap-bounds collision workflow.
/// It never creates, resizes, or deletes a collider automatically.
/// </summary>
public static class ImportedMap3DCollisionBuilder
{
    [MenuItem("Tools/Project/Legacy Tilemap Collision Builder (Disabled)")]
    public static void ShowDisabledMessage()
    {
        Debug.LogWarning(
            "[Map Collision] Tilemap Bounds 기반 자동 충돌 생성은 비활성화되었습니다. " +
            "수동 Collider는 변경되지 않습니다. 새로 교체하려면 'Rebuild First Five Border Collisions' 메뉴를 명시적으로 사용하세요.");
    }
}
