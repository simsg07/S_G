using UnityEditor;
using UnityEngine;

// Deprecated compatibility editor.
// Tile Palette is now visual-only. This editor remains only to satisfy Unity's
// compile cache / old references after the auto Tilemap collider workflow was
// deprecated.
[CustomEditor(typeof(TilemapTo3DBoxColliderBaker))]
public class TilemapTo3DBoxColliderBakerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.HelpBox(
            "Deprecated: Tilemap auto collider baking is disabled. Tile Palette is visual-only. Use Floor_Collision / Wall_Tile / Block_Tile prefabs for gameplay collision.",
            MessageType.Warning);

        TilemapTo3DBoxColliderBaker baker = (TilemapTo3DBoxColliderBaker)target;

        if (GUILayout.Button("Validate Baker Setup (Deprecated)"))
        {
            baker.ValidateBakerSetup();
        }

        if (GUILayout.Button("Rebuild Colliders (Disabled)"))
        {
            baker.RebuildColliders();
        }
    }
}
