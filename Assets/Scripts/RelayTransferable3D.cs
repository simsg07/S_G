using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WorldVariant3D))]
public class RelayTransferable3D : MonoBehaviour, IRelayTransferable3D
{
    [SerializeField] private bool mustExistInCurrentWorld = true;
    [SerializeField] private float relayCooldown = 0.25f;

    private WorldVariant3D worldVariant;
    private float nextRelayTime;

    private void Awake()
    {
        worldVariant = GetComponent<WorldVariant3D>();
    }

    public bool TryRelayToWorld(ResearchWorldId targetWorld, CameraAbilitySystem3D source)
    {
        if (Time.time < nextRelayTime)
        {
            return false;
        }

        if (worldVariant == null)
        {
            worldVariant = GetComponent<WorldVariant3D>();
        }

        if (worldVariant == null)
        {
            return false;
        }

        if (mustExistInCurrentWorld && !worldVariant.ExistsInBothWorlds && worldVariant.ActiveWorld != WorldSystem3D.ActiveWorld)
        {
            return false;
        }

        worldVariant.SetActiveWorld(targetWorld);
        WorldSystem3D.EnsureInstance().RefreshWorldObjects();
        nextRelayTime = Time.time + relayCooldown;
        return true;
    }
}
