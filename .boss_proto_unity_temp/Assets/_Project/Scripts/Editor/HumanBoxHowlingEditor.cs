#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HumanBoxHowling))]
public sealed class HumanBoxHowlingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        HumanBoxHowling howling = (HumanBoxHowling)target;
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.FloatField("Effective Radius", howling.EffectiveHowlingRadius);
        }

        if (howling.RangeMode == HowlingRangeMode.MatchPlayerDetectionRange)
            EditorGUILayout.HelpBox("Custom Radius is unused while Range Mode matches Player Detection Range.", MessageType.Info);
    }
}
#endif
