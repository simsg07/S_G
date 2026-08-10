using System.Collections;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStunReceiver : MonoBehaviour, IStunnable
{
    [Header("Stun - References")]
    [Tooltip("Movement controller to pause during stun. Usually PlatformerPlayer3D; leave empty to auto-fill.")]
    [SerializeField] private MonoBehaviour movementController;
    [Header("Debug")]
    [Tooltip("Print stun start/end and control-lock details in the Console.")]
    [SerializeField] private bool debugMode;

    private Coroutine stunRoutine;
    private float stunEndTime;
    private bool movementWasEnabledBeforeStun = true;
    private bool usingControlLockMethod;
    private bool usingSpeedMultiplierMethod;
    private MethodInfo controlLockMethod;
    private MethodInfo speedMultiplierMethod;

    private void Awake()
    {
        CacheReferences();
    }

    public void Stun(float duration)
    {
        duration = Mathf.Max(0f, duration);
        CacheReferences();
        stunEndTime = Mathf.Max(stunEndTime, Time.time + duration);

        if (stunRoutine != null)
        {
            return;
        }

        if (movementController != null)
        {
            movementWasEnabledBeforeStun = movementController.enabled;
        }

        stunRoutine = StartCoroutine(StunRoutine(duration));
    }

    private void CacheReferences()
    {
        if (movementController == null)
        {
            movementController = FindMovementController();
            controlLockMethod = null;
            speedMultiplierMethod = null;
            usingControlLockMethod = false;
            usingSpeedMultiplierMethod = false;
        }
    }

    private MonoBehaviour FindMovementController()
    {
        PlatformerPlayer3D platformerPlayer = GetComponent<PlatformerPlayer3D>();
        if (platformerPlayer == null)
        {
            platformerPlayer = GetComponentInParent<PlatformerPlayer3D>();
        }

        if (platformerPlayer == null)
        {
            platformerPlayer = GetComponentInChildren<PlatformerPlayer3D>();
        }

        if (platformerPlayer != null)
        {
            return platformerPlayer;
        }

        MonoBehaviour[] behaviours = GetComponentsInParent<MonoBehaviour>();
        MonoBehaviour movement = FindMovementBehaviour(behaviours);
        if (movement != null)
        {
            return movement;
        }

        behaviours = GetComponentsInChildren<MonoBehaviour>();
        return FindMovementBehaviour(behaviours);
    }

    private MonoBehaviour FindMovementBehaviour(MonoBehaviour[] behaviours)
    {
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == this)
            {
                continue;
            }

            string typeName = behaviour.GetType().Name;
            if (behaviour is IDamageable)
            {
                continue;
            }

            if (typeName.Contains("Player") || typeName.Contains("Controller") || typeName.Contains("Movement"))
            {
                return behaviour;
            }
        }

        return null;
    }

    private IEnumerator StunRoutine(float duration)
    {
        if (debugMode)
        {
            string controllerName = movementController != null ? movementController.GetType().Name : "None";
            Debug.Log($"[PlayerStunReceiver] Stun started for {duration:0.00}s. Controller={controllerName}", this);
            if (movementController == null)
            {
                Debug.LogWarning("[PlayerStunReceiver] No movementController found. Assign a movement controller to apply stun.", this);
            }
        }

        usingControlLockMethod = TrySetControlLocked(true);
        usingSpeedMultiplierMethod = TrySetMoveSpeedMultiplier(0f);

        if (!usingControlLockMethod && !usingSpeedMultiplierMethod && movementController != null)
        {
            movementController.enabled = false;
        }

        if (debugMode)
        {
            Debug.Log($"[PlayerStunReceiver] ControlLock={usingControlLockMethod}, SpeedMultiplier={usingSpeedMultiplierMethod}", this);
            Debug.Log($"[PlayerStunReceiver] control locked {usingControlLockMethod}", this);
        }

        while (Time.time < stunEndTime)
        {
            yield return null;
        }

        if (usingControlLockMethod)
        {
            TrySetControlLocked(false);
            if (debugMode)
            {
                Debug.Log("[PlayerStunReceiver] control locked false", this);
            }
        }

        if (usingSpeedMultiplierMethod)
        {
            TrySetMoveSpeedMultiplier(1f);
            if (debugMode)
            {
                Debug.Log("[PlayerStunReceiver] speed multiplier restored to 1", this);
            }
        }

        if (!usingControlLockMethod && !usingSpeedMultiplierMethod && movementController != null)
        {
            movementController.enabled = movementWasEnabledBeforeStun;
        }

        if (debugMode)
        {
            Debug.Log("[PlayerStunReceiver] Stun ended.", this);
        }

        stunRoutine = null;
        stunEndTime = 0f;
    }

    private bool TrySetControlLocked(bool locked)
    {
        if (movementController == null)
        {
            return false;
        }

        if (controlLockMethod == null)
        {
            controlLockMethod = movementController.GetType().GetMethod(
                "SetControlLocked",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(bool) },
                null);
        }

        if (controlLockMethod == null)
        {
            controlLockMethod = movementController.GetType().GetMethod(
                "SetStunned",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(bool) },
                null);
        }

        if (controlLockMethod == null)
        {
            return false;
        }

        controlLockMethod.Invoke(movementController, new object[] { locked });
        return true;
    }

    private bool TrySetMoveSpeedMultiplier(float multiplier)
    {
        if (movementController == null)
        {
            return false;
        }

        if (speedMultiplierMethod == null)
        {
            speedMultiplierMethod = movementController.GetType().GetMethod(
                "SetExternalMoveSpeedMultiplier",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(float) },
                null);
        }

        if (speedMultiplierMethod == null)
        {
            return false;
        }

        speedMultiplierMethod.Invoke(movementController, new object[] { multiplier });

        if (debugMode)
        {
            Debug.Log($"[PlayerStunReceiver] PlayerController speed multiplier set to {multiplier:0.##}", this);
        }

        return true;
    }

}
