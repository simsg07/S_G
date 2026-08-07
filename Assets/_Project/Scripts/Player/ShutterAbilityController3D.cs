using UnityEngine;

internal enum ShutterExecutionResult3D
{
    SpecialTargetUnavailable,
    SpecialTargetSucceeded,
    InterventionUnavailable,
    MarkRulesBlocked,
    AlreadyFrozen,
    InterventionConsumeFailed,
    TargetRejected,
    SucceededWithExistingMark,
    SucceededDuringRemarkCooldown,
    SucceededWithNewMark
}

internal sealed class ShutterAbilityController3D
{
    private readonly ShutterMarkRegistry3D shutterMarkRegistry;
    private readonly CameraAbilityCooldowns3D cameraAbilityCooldowns;

    public ShutterAbilityController3D(
        ShutterMarkRegistry3D shutterMarkRegistry,
        CameraAbilityCooldowns3D cameraAbilityCooldowns)
    {
        this.shutterMarkRegistry = shutterMarkRegistry;
        this.cameraAbilityCooldowns = cameraAbilityCooldowns;
    }

    public ShutterExecutionResult3D TryExecute(
        IShutterFreezable3D target,
        Component targetComponent,
        CameraAbilitySystem3D source,
        CameraInterventionLimiter interventionLimiter,
        CameraTargetHighlightManager3D highlightManager,
        float currentTime,
        float freezeDuration,
        float shutterCooldown,
        float markDuration,
        float remarkCooldown,
        bool refreshFreezeWhileFrozen,
        bool refreshMarkOnShutter,
        bool allowFreezeWhileMarked,
        bool allowFreezeDuringRemarkCooldown)
    {
        if (target is ShutterTarget3D shutterTarget)
        {
            return ExecuteSpecialTarget(shutterTarget, highlightManager, shutterCooldown);
        }

        if (interventionLimiter != null && !interventionLimiter.CanUseIntervention)
        {
            return ShutterExecutionResult3D.InterventionUnavailable;
        }

        bool isMarked = shutterMarkRegistry.IsMarked(targetComponent, currentTime);
        bool isInRemarkCooldown = shutterMarkRegistry.IsInRemarkCooldown(targetComponent, currentTime);
        bool isFrozen = target is IShutterFreezeState3D freezeState && freezeState.IsShutterFrozen;

        if ((isMarked && !allowFreezeWhileMarked) || (isInRemarkCooldown && !allowFreezeDuringRemarkCooldown))
        {
            return ShutterExecutionResult3D.MarkRulesBlocked;
        }

        if (isFrozen && !refreshFreezeWhileFrozen)
        {
            return ShutterExecutionResult3D.AlreadyFrozen;
        }

        if (interventionLimiter != null && !interventionLimiter.TryConsumeIntervention("Freeze object"))
        {
            return ShutterExecutionResult3D.InterventionConsumeFailed;
        }

        if (!target.ApplyShutterFreeze(freezeDuration, source))
        {
            if (interventionLimiter != null)
            {
                interventionLimiter.RestoreCameraInterventions(1);
            }

            return ShutterExecutionResult3D.TargetRejected;
        }

        cameraAbilityCooldowns.StartShutter(shutterCooldown);
        if ((!isMarked && !isInRemarkCooldown) || (isMarked && refreshMarkOnShutter))
        {
            MarkTarget(
                targetComponent,
                target,
                highlightManager,
                currentTime,
                markDuration,
                remarkCooldown);
        }

        if (isMarked)
        {
            return ShutterExecutionResult3D.SucceededWithExistingMark;
        }

        return isInRemarkCooldown
            ? ShutterExecutionResult3D.SucceededDuringRemarkCooldown
            : ShutterExecutionResult3D.SucceededWithNewMark;
    }

    private ShutterExecutionResult3D ExecuteSpecialTarget(
        ShutterTarget3D shutterTarget,
        CameraTargetHighlightManager3D highlightManager,
        float shutterCooldown)
    {
        if (!shutterTarget.CanReceiveShutter())
        {
            return ShutterExecutionResult3D.SpecialTargetUnavailable;
        }

        shutterTarget.ApplyShutter();
        if (shutterTarget.IsMarked)
        {
            highlightManager.SetMark(
                shutterTarget,
                shutterTarget.VisualMarkEndTime,
                shutterTarget.VisualMarkEndTime);
        }
        else
        {
            highlightManager.ClearMark(shutterTarget);
        }

        cameraAbilityCooldowns.StartShutter(shutterCooldown);
        return ShutterExecutionResult3D.SpecialTargetSucceeded;
    }

    private void MarkTarget(
        Component targetComponent,
        IShutterFreezable3D target,
        CameraTargetHighlightManager3D highlightManager,
        float currentTime,
        float markDuration,
        float remarkCooldown)
    {
        if (!shutterMarkRegistry.Mark(
                targetComponent,
                target,
                currentTime,
                markDuration,
                remarkCooldown,
                out float markEnd,
                out float cooldownEnd))
        {
            return;
        }

        highlightManager.SetMark(targetComponent, markEnd, cooldownEnd);
    }
}
