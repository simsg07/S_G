using UnityEngine;

internal sealed class CameraWorldTargetStateController3D
{
    private bool cameraTargetScanActive;
    private float nextCameraTargetRefreshTime;

    public void Tick(
        bool cameraModeActive,
        CameraWorldSwitcher switcher,
        float currentUnscaledTime,
        float refreshInterval)
    {
        if (cameraModeActive)
        {
            if (switcher == null)
            {
                return;
            }

            cameraTargetScanActive = true;
            if (currentUnscaledTime >= nextCameraTargetRefreshTime)
            {
                nextCameraTargetRefreshTime = currentUnscaledTime + Mathf.Max(0.02f, refreshInterval);
                switcher.RefreshVisibleTargets();
            }
        }
        else if (cameraTargetScanActive)
        {
            cameraTargetScanActive = false;
            nextCameraTargetRefreshTime = 0f;
            if (switcher != null)
            {
                switcher.ClearTargetStates();
            }
        }
    }

    public void Clear(CameraWorldSwitcher switcher)
    {
        cameraTargetScanActive = false;
        nextCameraTargetRefreshTime = 0f;
        if (switcher != null)
        {
            switcher.ClearTargetStates();
        }
    }
}
