using UnityEngine;

[DisallowMultipleComponent]
public class MonsterDebugInfo : MonoBehaviour
{
    [Header("Debug")]
    public bool debugMode;
    public float logInterval = 0.5f;
    public bool showGizmos = true;

    private float nextLogTime;

    public bool CanLog => debugMode && Time.time >= nextLogTime;

    public void MarkLogged()
    {
        nextLogTime = Time.time + Mathf.Max(0.05f, logInterval);
    }

    private void OnValidate()
    {
        logInterval = Mathf.Max(0.05f, logInterval);
    }
}
