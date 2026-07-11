using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class WorldSyncedBoss3D : MonoBehaviour, IAttackable3D
{
    private static readonly Dictionary<string, BossState> BossStates = new Dictionary<string, BossState>();

    [SerializeField] private string bossId = "main_boss";
    [SerializeField] private int maxHealth = 20;
    [SerializeField] private GameObject[] worldAHazards = new GameObject[0];
    [SerializeField] private GameObject[] worldBHazards = new GameObject[0];
    [SerializeField] private UnityEvent onDefeated;

    private BossState state;

    public int CurrentHealth => state != null ? state.CurrentHealth : maxHealth;
    public int PatternIndex => state != null ? state.PatternIndex : 0;
    public float PatternTime => state != null ? state.PatternTime : 0f;

    private void Awake()
    {
        state = GetOrCreateState();
    }

    private void OnEnable()
    {
        WorldSystem3D.ActiveWorldChanged += HandleWorldChanged;
        RefreshHazards(WorldSystem3D.ActiveWorld);
    }

    private void OnDisable()
    {
        WorldSystem3D.ActiveWorldChanged -= HandleWorldChanged;
    }

    private void Update()
    {
        if (state != null && state.CurrentHealth > 0)
        {
            state.PatternTime += Time.deltaTime;
        }
    }

    public bool TakeAttack()
    {
        return TakeDamage(1);
    }

    public bool TakeDamage(int amount)
    {
        if (state == null || state.CurrentHealth <= 0 || amount <= 0)
        {
            return false;
        }

        state.CurrentHealth = Mathf.Max(0, state.CurrentHealth - amount);
        if (state.CurrentHealth == 0)
        {
            onDefeated?.Invoke();
        }

        return true;
    }

    public void AdvancePattern()
    {
        if (state == null)
        {
            return;
        }

        state.PatternIndex++;
        state.PatternTime = 0f;
    }

    private void HandleWorldChanged(ResearchWorldId previousWorld, ResearchWorldId nextWorld)
    {
        RefreshHazards(nextWorld);
    }

    private void RefreshHazards(ResearchWorldId world)
    {
        SetObjectsActive(worldAHazards, world == ResearchWorldId.WorldA);
        SetObjectsActive(worldBHazards, world == ResearchWorldId.WorldB);
    }

    private BossState GetOrCreateState()
    {
        string key = string.IsNullOrWhiteSpace(bossId) ? gameObject.name : bossId;
        if (!BossStates.TryGetValue(key, out BossState bossState))
        {
            bossState = new BossState(Mathf.Max(1, maxHealth));
            BossStates.Add(key, bossState);
        }

        return bossState;
    }

    private static void SetObjectsActive(GameObject[] objects, bool active)
    {
        GameObject[] targets = objects ?? new GameObject[0];
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                targets[i].SetActive(active);
            }
        }
    }

    private class BossState
    {
        public BossState(int maxHealth)
        {
            CurrentHealth = maxHealth;
        }

        public int CurrentHealth;
        public int PatternIndex;
        public float PatternTime;
    }
}
