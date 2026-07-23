using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow3D : MonoBehaviour
{
    [SerializeField] private Transform target; // 카메라가 따라갈 대상입니다. 비워두면 플레이어를 자동으로 찾습니다.
    [SerializeField] private Vector3 offset = new Vector3(0f, 1f, -10f); // 대상 기준 카메라 위치 차이입니다.
    [SerializeField] private float followSpeed = 8f; // 카메라가 대상을 따라가는 부드러움 속도입니다.

    [SerializeField] private float orthographicSize = 5.2f; // 2.5D 사이드뷰에서 사용할 카메라 Orthographic 크기입니다.

    private Camera targetCamera;

    private void Awake()
    {
        ConfigureSideViewCamera();
    }

    private void OnValidate()
    {
        ConfigureSideViewCamera();
    }

    private void LateUpdate()
    {
        ConfigureSideViewCamera();

        if (target == null)
        {
            FindPlayerTarget();
        }

        if (target == null)
        {
            return;
        }

        Vector3 targetPosition = target.position + offset;
        targetPosition.z = offset.z;
        if (!Application.isPlaying || followSpeed <= 0f)
        {
            transform.position = targetPosition;
            transform.rotation = Quaternion.identity;
            return;
        }

        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, t);
        transform.rotation = Quaternion.identity;
    }

    private void FindPlayerTarget()
    {
        PlatformerPlayer3D player = FindFirstObjectByType<PlatformerPlayer3D>();
        if (player != null)
        {
            target = player.transform;
        }
    }

    public void SnapToTarget(Transform newTarget)
    {
        target = newTarget;
        if (target == null)
        {
            return;
        }

        Vector3 targetPosition = target.position + offset;
        targetPosition.z = offset.z;
        transform.SetPositionAndRotation(targetPosition, Quaternion.identity);
        ConfigureSideViewCamera();
    }

    private void ConfigureSideViewCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        TwoPointFiveDUtility3D.ConfigureSideViewCamera(targetCamera, orthographicSize);
    }
}
