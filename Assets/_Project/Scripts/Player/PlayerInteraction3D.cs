using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlatformerPlayer3D))]
public class PlayerInteraction3D : MonoBehaviour
{
    [SerializeField] private Key interactKey = Key.F; // 플레이어가 주변 오브젝트와 상호작용하는 키입니다.
    [SerializeField] private float interactRange = 1.05f; // 플레이어 기준 상호작용 판정을 띄우는 거리입니다.
    [SerializeField] private Vector3 interactBoxSize = new Vector3(1.1f, 1.1f, 1f); // 상호작용 대상 탐색 박스 크기입니다.
    [SerializeField] private LayerMask interactMask = ~0; // 상호작용 가능한 오브젝트를 찾을 레이어 범위입니다.

    private readonly Collider[] interactionHits = new Collider[16];

    private PlatformerPlayer3D movement;

    private void Awake()
    {
        movement = GetComponent<PlatformerPlayer3D>();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || interactKey == Key.None || !keyboard[interactKey].wasPressedThisFrame)
        {
            return;
        }

        TryInteract();
    }

    private void TryInteract()
    {
        Vector3 direction = GetInteractionDirection();
        Vector3 center = transform.position + direction * interactRange;
        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            interactBoxSize * 0.5f,
            interactionHits,
            Quaternion.identity,
            interactMask,
            QueryTriggerInteraction.Collide
        );

        IInteractable3D bestInteractable = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = interactionHits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            IInteractable3D interactable = FindInteractable(hit);
            if (interactable == null)
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(hit.bounds.center - transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestInteractable = interactable;
            }
        }

        bestInteractable?.TryInteract(gameObject);
    }

    private Vector3 GetInteractionDirection()
    {
        if (movement == null)
        {
            movement = GetComponent<PlatformerPlayer3D>();
        }

        if (movement != null && Mathf.Abs(movement.VerticalLookInput) > 0.01f)
        {
            return movement.VerticalLookInput > 0f ? Vector3.up : Vector3.down;
        }

        float facing = movement != null ? movement.FacingDirection : 1f;
        return facing < 0f ? Vector3.left : Vector3.right;
    }

    private static IInteractable3D FindInteractable(Collider hit)
    {
        MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IInteractable3D interactable)
            {
                return interactable;
            }
        }

        return null;
    }
}
