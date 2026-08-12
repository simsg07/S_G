#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RopeLengthController3D))]
public sealed class RopeLengthController3DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        RopeLengthController3D controller = (RopeLengthController3D)target;
        if (controller.HasBoxConnection && controller.BoxAnchorError > controller.AnchorErrorTolerance)
        {
            EditorGUILayout.HelpBox(
                $"BoxTopAnchor is {controller.BoxAnchorError:0.####} units from the Box Sprite top center " +
                $"(tolerance {controller.AnchorErrorTolerance:0.####}).",
                MessageType.Warning);
        }
    }
}
#endif
