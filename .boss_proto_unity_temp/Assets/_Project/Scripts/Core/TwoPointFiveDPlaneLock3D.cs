using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class TwoPointFiveDPlaneLock3D : MonoBehaviour
{
    [SerializeField] private float gameplayPlaneZ = TwoPointFiveDUtility3D.GameplayPlaneZ; // 이 오브젝트가 고정될 2.5D Z축 위치입니다.
    [SerializeField] private bool lockTransformZ = true; // Transform 위치의 Z축을 고정할지 정합니다.
    [SerializeField] private bool configureRigidbody = true; // Rigidbody가 있으면 Freeze Position Z와 Freeze Rotation을 자동 적용할지 정합니다.

    private Rigidbody body;

    private void Awake()
    {
        ApplyLock();
    }

    private void OnEnable()
    {
        ApplyLock();
    }

    private void OnValidate()
    {
        ApplyLock();
    }

    private void FixedUpdate()
    {
        if (Application.isPlaying)
        {
            ApplyLock();
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            ApplyLock();
        }
    }

    private void ApplyLock()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        if (configureRigidbody && body != null)
        {
            TwoPointFiveDUtility3D.ConfigureRigidbodyForSideView(body, gameplayPlaneZ);
            return;
        }

        if (lockTransformZ)
        {
            TwoPointFiveDUtility3D.ClampTransformToPlane(transform, gameplayPlaneZ);
        }
    }
}
