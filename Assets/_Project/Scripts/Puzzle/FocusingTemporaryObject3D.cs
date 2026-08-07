using UnityEngine;

[DisallowMultipleComponent]
public sealed class FocusingTemporaryObject3D : MonoBehaviour
{
    [SerializeField] private FocusingSpawner3D owner;

    private void Awake()
    {
        if (owner == null) owner = GetComponentInParent<FocusingSpawner3D>();
        if (owner != null) owner.RegisterTemporaryObject(gameObject);
    }
}
