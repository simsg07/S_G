using UnityEngine;

[DisallowMultipleComponent]
public sealed class TilemapPlayableAreaSeed : MonoBehaviour
{
    [SerializeField] private bool worldA = true;
    [SerializeField] private bool worldB = true;
    public bool WorldA => worldA;
    public bool WorldB => worldB;
}

[DisallowMultipleComponent]
public sealed class TilemapCollisionBakeBounds : MonoBehaviour
{
    [SerializeField] private GridLayout grid;
    [SerializeField] private Vector2 center;
    [SerializeField] private Vector2 size = new Vector2(30f, 15f);
    [SerializeField, Min(0)] private int padding;
    [SerializeField] private bool worldA = true;
    [SerializeField] private bool worldB = true;
    [SerializeField] private bool showGizmo = true;

    public GridLayout Grid => grid;
    public int Padding => Mathf.Max(0, padding);
    public bool WorldA => worldA;
    public bool WorldB => worldB;
    public Bounds WorldBounds => new Bounds(transform.TransformPoint(center), Vector3.Scale(new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 1f), transform.lossyScale));

    private void OnValidate() { size.x = Mathf.Max(0.01f, size.x); size.y = Mathf.Max(0.01f, size.y); padding = Mathf.Max(0, padding); }
    private void OnDrawGizmos() { if (!showGizmo) return; Gizmos.color = Color.yellow; Gizmos.DrawWireCube(WorldBounds.center, WorldBounds.size); }
}

[DisallowMultipleComponent]
public sealed class AlwaysBakeCollisionArea : MonoBehaviour
{
    [SerializeField] private Vector2 size = new Vector2(4f, 4f);
    public Bounds WorldBounds => new Bounds(transform.position, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 1f));
    private void OnDrawGizmosSelected() { Gizmos.color = Color.cyan; Gizmos.DrawWireCube(WorldBounds.center, WorldBounds.size); }
}

[DisallowMultipleComponent]
public sealed class NeverBakeCollisionArea : MonoBehaviour
{
    [SerializeField] private Vector2 size = new Vector2(4f, 4f);
    public Bounds WorldBounds => new Bounds(transform.position, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 1f));
    private void OnDrawGizmosSelected() { Gizmos.color = Color.red; Gizmos.DrawWireCube(WorldBounds.center, WorldBounds.size); }
}
