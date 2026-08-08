using UnityEngine;

public enum DirectPlacedMonsterKind3D
{
    HumanBox,
    EyeballFly
}

[CreateAssetMenu(fileName = "DirectPlacedMonsterSpawnCatalog3D",
    menuName = "_Project/Puzzle/Direct Placed Monster Spawn Catalog 3D")]
public sealed class DirectPlacedMonsterSpawnCatalog3D : ScriptableObject
{
    [SerializeField] private GameObject humanBoxPrefab;
    [SerializeField] private GameObject eyeballFlyPrefab;

    public GameObject GetPrefab(DirectPlacedMonsterKind3D kind)
    {
        return kind == DirectPlacedMonsterKind3D.HumanBox ? humanBoxPrefab : eyeballFlyPrefab;
    }
}
