using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PlayerVisualSettings3D : MonoBehaviour
{
    private const float MinimumVisualScale = 0.05f;

    [Header("플레이어 이미지 크기")]
    [Tooltip("SpriteRenderer와 Animator가 들어 있는 시각 전용 루트입니다. Collider와 Rigidbody에는 영향을 주지 않습니다.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("플레이어 이미지의 X/Y 배율입니다. Player 루트와 Collider 크기는 변경하지 않습니다.")]
    [SerializeField] private Vector2 visualScale = Vector2.one;
    [Tooltip("켜면 visualScale의 X 값을 X/Y 공통 배율로 사용합니다.")]
    [SerializeField] private bool useUniformScale = true;
    [Tooltip("Player 중심을 기준으로 한 이미지 위치 보정값입니다. Z는 2.5D 렌더링 순서에 사용할 수 있습니다.")]
    [SerializeField] private Vector3 visualOffset = new Vector3(0.2f, 0.04f, -0.45f);

    [Header("좌우 반전")]
    [Tooltip("켜면 오른쪽을 바라볼 때 SpriteRenderer의 X 반전을 사용합니다. 이미지 배율의 절댓값은 유지됩니다.")]
    [SerializeField] private bool flipVisualX = true;

    [Header("Collider 설정 (이미지 배율과 별도)")]
    [Tooltip("Player 루트의 기존 BoxCollider입니다. 새 Collider를 생성하지 않습니다.")]
    [SerializeField] private BoxCollider playerCollider;
    [Tooltip("물리 판정 중심입니다. visualScale을 변경해도 자동 변경되지 않습니다.")]
    [SerializeField] private Vector3 colliderCenter = new Vector3(0.16f, 0f, 0f);
    [Tooltip("물리 판정 크기입니다. visualScale을 변경해도 자동 변경되지 않습니다.")]
    [SerializeField] private Vector3 colliderSize = new Vector3(1.12f, 2.4f, 1f);

    [Header("에디터 미리보기")]
    [Tooltip("Play Mode에 들어가지 않아도 Prefab/Scene View에서 이미지 크기와 위치를 적용합니다.")]
    [SerializeField] private bool previewInEditor = true;
    [Tooltip("설정 적용 정보를 Console에 출력합니다.")]
    [SerializeField] private bool debugMode;

    private SpriteRenderer visualRenderer;

    public Vector2 VisualScale => visualScale;
    public Vector3 VisualOffset => visualOffset;

    private void Awake()
    {
        CacheReferences();
        ApplySettings();
    }

    private void OnEnable()
    {
        CacheReferences();
        ApplySettings();
    }

    private void OnValidate()
    {
        ClampSettings();
        CacheReferences();
        if (previewInEditor || Application.isPlaying)
        {
            ApplySettings();
        }
    }

    public void ApplyFacing(float facingDirection)
    {
        CacheReferences();
        if (visualRenderer == null || Mathf.Abs(facingDirection) < 0.01f)
        {
            return;
        }

        bool facesRight = facingDirection > 0f;
        visualRenderer.flipX = flipVisualX ? facesRight : !facesRight;
    }

    [ContextMenu("Apply Visual And Collider Settings")]
    public void ApplySettings()
    {
        ClampSettings();
        CacheReferences();

        // Physics and movement always use an unscaled Player root.
        transform.localScale = Vector3.one;

        if (visualRoot != null)
        {
            float scaleX = Mathf.Max(MinimumVisualScale, visualScale.x);
            float scaleY = useUniformScale
                ? scaleX
                : Mathf.Max(MinimumVisualScale, visualScale.y);
            visualRoot.localScale = new Vector3(scaleX, scaleY, 1f);
            visualRoot.localPosition = visualOffset;
        }

        if (playerCollider != null)
        {
            playerCollider.center = colliderCenter;
            playerCollider.size = colliderSize;
        }

        if (debugMode)
        {
            Debug.Log($"[PlayerVisualSettings3D] visualScale={visualScale}, visualOffset={visualOffset}, colliderSize={colliderSize}", this);
        }
    }

    private void CacheReferences()
    {
        if (visualRoot == null)
        {
            Transform namedRoot = transform.Find("VisualRoot");
            visualRoot = namedRoot != null ? namedRoot : transform.Find("Visual");
        }

        if (playerCollider == null)
        {
            playerCollider = GetComponent<BoxCollider>();
        }

        if (visualRenderer == null && visualRoot != null)
        {
            visualRenderer = visualRoot.GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    private void ClampSettings()
    {
        visualScale.x = Mathf.Max(MinimumVisualScale, Mathf.Abs(visualScale.x));
        visualScale.y = Mathf.Max(MinimumVisualScale, Mathf.Abs(visualScale.y));
        colliderSize.x = Mathf.Max(0.01f, Mathf.Abs(colliderSize.x));
        colliderSize.y = Mathf.Max(0.01f, Mathf.Abs(colliderSize.y));
        colliderSize.z = Mathf.Max(0.01f, Mathf.Abs(colliderSize.z));
    }

    private void OnDrawGizmosSelected()
    {
        if (playerCollider == null)
        {
            playerCollider = GetComponent<BoxCollider>();
        }
        if (playerCollider == null) return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireCube(playerCollider.center, playerCollider.size);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
