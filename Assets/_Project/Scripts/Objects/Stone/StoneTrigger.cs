using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class StoneTrigger : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("이 Trigger가 떨어뜨릴 StoneTrap입니다. 씬에 배치된 Stone 인스턴스를 연결합니다.")]
    public StoneTrap targetStone;

    [Header("Trigger Settings")]
    [Tooltip("Player가 Trigger 영역에 들어오면 StoneTrap.TriggerDrop()을 호출합니다.")]
    public bool triggerOnPlayerEnter = true;
    [Tooltip("한 번만 발동합니다. 테스트 중 반복 발동이 필요하면 끄거나 ResetTrigger()를 호출합니다.")]
    public bool triggerOnce = true;
    [Tooltip("비워두면 Player 태그로 감지합니다. 지정하면 playerLayerMask에 포함된 레이어도 Player로 판단합니다.")]
    public LayerMask playerLayerMask;

    [Header("Debug")]
    public bool debugMode = true;

    private bool triggered;
    private bool warnedMissingStone;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        WarnIfStoneMissing();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!triggerOnPlayerEnter || (triggerOnce && triggered) || other == null || !IsPlayer(other))
        {
            return;
        }

        if (targetStone == null)
        {
            WarnIfStoneMissing();
            return;
        }

        triggered = true;
        targetStone.TriggerDrop();
        Log($"Triggered by {other.name}");
    }

    public void ResetTrigger()
    {
        triggered = false;
    }

    private bool IsPlayer(Collider other)
    {
        bool layerMatches = playerLayerMask.value != 0 &&
            (playerLayerMask.value & (1 << other.gameObject.layer)) != 0;
        if (layerMatches)
        {
            return true;
        }

        Transform current = other.transform;
        while (current != null)
        {
            if (current.CompareTag("Player"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void WarnIfStoneMissing()
    {
        if (targetStone != null || warnedMissingStone)
        {
            return;
        }

        warnedMissingStone = true;
        Debug.LogWarning("[StoneTrigger] Target Stone is not assigned.", this);
    }

    private void EnsureTriggerCollider()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[StoneTrigger] {message}", this);
        }
    }
}
