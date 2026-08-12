using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MonsterDummyDatabase", menuName = "S_G/Monster Dummy Database")]
public class MonsterDummyDatabase3D : ScriptableObject
{
    private const string ResourceName = "MonsterDummyDatabase";
    private static MonsterDummyDatabase3D cachedDatabase;

    [SerializeField] private List<MonsterDummyProfile3D> monsters = new List<MonsterDummyProfile3D>();

    public IReadOnlyList<MonsterDummyProfile3D> Monsters => monsters;

    public static MonsterDummyDatabase3D Load()
    {
        if (cachedDatabase == null)
        {
            cachedDatabase = Resources.Load<MonsterDummyDatabase3D>(ResourceName);
        }

        return cachedDatabase;
    }
}

[System.Serializable]
public class MonsterDummyProfile3D
{
    public string id = "dummy_monster";
    public string displayName = "Dummy Monster";
    public MonsterDummyKind3D kind = MonsterDummyKind3D.Stationary;
    public ResearchWorldId world = ResearchWorldId.WorldA;
    public bool existsInBothWorlds;
    public Vector3 position = Vector3.zero;
    public Vector3 size = new Vector3(0.8f, 0.8f, 0.8f);
    public Color color = new Color(0.75f, 0.45f, 0.95f, 1f);
    public int health = 1;
    public bool canBeFrozen = true;
    public bool reactsToFlash = true;
    public bool canRelay;
}

public enum MonsterDummyKind3D
{
    Stationary,
    Patrol,
    Hazard
}
