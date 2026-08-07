using UnityEngine;

internal sealed class CameraDiagnostics3D
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public int EnterRequestCount { get; private set; }
    public int ActualEnterCount { get; private set; }
    public int ApplySlowMotionCount { get; private set; }
    public int ExitRequestCount { get; private set; }
    public int ActualExitCount { get; private set; }
    public int RestoreSlowMotionCount { get; private set; }
    public int DuplicateTransitionBlockCount { get; private set; }

    public void RecordEnterRequest()
    {
        EnterRequestCount++;
    }

    public void RecordActualEnter()
    {
        ActualEnterCount++;
    }

    public void RecordApplySlowMotion()
    {
        ApplySlowMotionCount++;
    }

    public void RecordExitRequest()
    {
        ExitRequestCount++;
    }

    public void RecordActualExit()
    {
        ActualExitCount++;
    }

    public void RecordRestoreSlowMotion()
    {
        RestoreSlowMotionCount++;
    }

    public void RecordDuplicateTransitionBlock()
    {
        DuplicateTransitionBlockCount++;
    }

    public void Reset()
    {
        EnterRequestCount = 0;
        ActualEnterCount = 0;
        ApplySlowMotionCount = 0;
        ExitRequestCount = 0;
        ActualExitCount = 0;
        RestoreSlowMotionCount = 0;
        DuplicateTransitionBlockCount = 0;
    }
#endif

    public void LogCameraMode(bool enabled, string message, Object context)
    {
        if (enabled)
        {
            Debug.Log($"[CameraMode] {message}", context);
        }
    }

    public void LogCameraTransition(bool enabled, string transition, string reason, Object context)
    {
        if (enabled)
        {
            Debug.Log($"[CameraMode] {transition} ({reason})", context);
        }
    }

    public void LogShutter(bool enabled, string message, Object context)
    {
        if (enabled)
        {
            Debug.Log($"[Shutter] {message}", context);
        }
    }
}
