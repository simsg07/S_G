using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public enum BoxDespawnMode
{
    Destroy,
    Disable
}

[DisallowMultipleComponent]
public sealed class DestructibleBox3D : MonoBehaviour, IDamageable
{
    [Header("파괴 설정")]
    [SerializeField, Tooltip("끄면 모든 피해를 무시합니다.")] private bool isDestructible = true;
    [SerializeField, Min(1), Tooltip("Box의 최대 체력입니다.")] private int maxHealth = 1;
    [SerializeField, Tooltip("Play Mode에서 확인하는 현재 체력입니다.")] private int currentHealth;
    [SerializeField, Tooltip("체력이 0이 되면 제거 절차를 실행합니다.")] private bool destroyAtZeroHealth = true;
    [SerializeField, Tooltip("실제 파괴 또는 비활성화 중 제거 방식을 선택합니다.")] private BoxDespawnMode despawnMode = BoxDespawnMode.Destroy;
    [SerializeField, Min(0f), Tooltip("제거 전 대기 시간입니다.")] private float despawnDelay;
    [SerializeField, Tooltip("파괴 Animator 트리거를 사용합니다.")] private bool useDestroyAnimation;
    [SerializeField] private Animator animator;
    [SerializeField] private string destroyTrigger = "Break";

    [Header("이벤트")]
    public UnityEvent onDespawned;

    private bool despawning;
    private Collider[] colliders;
    private SpawnedObjectLifecycle lifecycle;
    [SerializeField] private PersistentSceneObject3D persistentState;

    public bool CanTakeDamage => isDestructible && !despawning && currentHealth > 0;

    private void Awake()
    {
        colliders = GetComponentsInChildren<Collider>(true);
        lifecycle = GetComponent<SpawnedObjectLifecycle>();
        currentHealth = Mathf.Max(1, maxHealth);
    }

    private void OnEnable()
    {
        despawning = false;
        currentHealth = Mathf.Max(1, maxHealth);
        if (colliders != null)
        {
            foreach (Collider boxCollider in colliders)
            {
                if (boxCollider != null) boxCollider.enabled = true;
            }
        }
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        despawnDelay = Mathf.Max(0f, despawnDelay);
        if (!Application.isPlaying) currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(new DamageInfo(damage, null, gameObject, transform.position, Vector3.zero, DamageType.Generic));
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (!CanTakeDamage || damageInfo.damageAmount <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damageInfo.damageAmount);
        if (currentHealth == 0 && destroyAtZeroHealth)
        {
            StartCoroutine(DespawnRoutine());
        }
    }

    private IEnumerator DespawnRoutine()
    {
        if (despawning) yield break;
        despawning = true;

        foreach (Collider boxCollider in colliders)
        {
            if (boxCollider != null) boxCollider.enabled = false;
        }

        if (useDestroyAnimation && animator != null && !string.IsNullOrEmpty(destroyTrigger))
        {
            animator.SetTrigger(destroyTrigger);
        }

        if (despawnDelay > 0f) yield return new WaitForSeconds(despawnDelay);

        onDespawned?.Invoke();
        if (persistentState == null) persistentState = GetComponent<PersistentSceneObject3D>();
        persistentState?.MarkDestroyed();
        if (lifecycle == null) lifecycle = GetComponent<SpawnedObjectLifecycle>();
        if (lifecycle != null) lifecycle.NotifyGameplayDespawn();

        if (despawnMode == BoxDespawnMode.Disable) gameObject.SetActive(false);
        else Destroy(gameObject);
    }
}
