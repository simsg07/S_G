using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public enum FocusingRingState
{
    Ready,
    Resetting,
    Cooldown,
    Disabled
}

[DisallowMultipleComponent]
[AddComponentMenu("_Project/Player/Focusing Ring Controller 3D")]
public sealed class FocusingRingController3D : MonoBehaviour
{
    [Header("Focusing Ring")]
    [SerializeField] private bool abilityAvailable = true;
    [Min(0f)]
    [SerializeField]
    [Tooltip("전체 초기화 중 제거와 재생성 사이의 unscaled 연출 대기 시간")]
    private float resetPresentationTime = 0.1f;
    [FormerlySerializedAs("cooldown")]
    [Min(0f)]
    [SerializeField]
    [Tooltip("전체 초기화 완료 후 다시 사용할 수 있을 때까지의 unscaled 대기 시간")]
    private float resetCooldownDuration = 0.5f;
    [Min(0f)]
    [SerializeField]
    [Tooltip("정상적인 카메라 모드에서 짧은 클릭을 기억하는 unscaled 시간")]
    private float resetInputBufferDuration = 0.2f;

    [Header("Spawner Discovery")]
    [SerializeField] private bool includeRegisteredSpawners = true;
    [SerializeField] private List<FocusingSpawner3D> explicitSpawners = new List<FocusingSpawner3D>();

    [Header("Runtime (Read Only)")]
    [SerializeField] private FocusingRingState state = FocusingRingState.Ready;
    [FormerlySerializedAs("cooldownRemaining")]
    [SerializeField] private float resetCooldownRemaining;

    [Header("Events")]
    [SerializeField] private UnityEvent resetStarted = new UnityEvent();
    [SerializeField] private UnityEvent resetCompleted = new UnityEvent();
    [SerializeField] private UnityEvent cooldownCompleted = new UnityEvent();

    private readonly List<FocusingSpawner3D> cachedSpawners = new List<FocusingSpawner3D>(32);
    private readonly List<FocusingSpawner3D> resetSnapshot = new List<FocusingSpawner3D>(32);
    private readonly List<IFocusingInPlaceResettable3D> cachedInPlaceResettables =
        new List<IFocusingInPlaceResettable3D>(16);
    private readonly List<IFocusingInPlaceResettable3D> inPlaceResetSnapshot =
        new List<IFocusingInPlaceResettable3D>(16);
    private PlayerDamageReceiver damageReceiver;
    private CameraAbilitySystem3D cameraAbilitySystem;
    private float resetCooldownUntil;
    private float bufferedResetRequestExpiresAt = -1f;
    private Coroutine resetRoutine;
    private bool resetInProgress;

    private static readonly FocusingSpawnerType[] SpawnOrder =
    {
        FocusingSpawnerType.PuzzleObject,
        FocusingSpawnerType.GravityObject,
        FocusingSpawnerType.DestructibleObject,
        FocusingSpawnerType.Monster
    };

    public FocusingRingState State => state;
    public float ResetCooldownRemaining => state == FocusingRingState.Cooldown ? Mathf.Max(0f, resetCooldownUntil - Time.unscaledTime) : 0f;
    public bool IsResetOnCooldown => ResetCooldownRemaining > 0f;
    public bool IsResetting => resetInProgress;
    public float CooldownRemaining => ResetCooldownRemaining;

    private void Awake()
    {
        damageReceiver = GetComponent<PlayerDamageReceiver>();
        cameraAbilitySystem = GetComponent<CameraAbilitySystem3D>();
        CacheSceneSpawners();
        RefreshEnabledState();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        // Other scene objects may register after this component's Awake because script order is undefined.
        CacheSceneSpawners();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        resetRoutine = null;
        resetInProgress = false;
        ClearBufferedResetRequest();
        resetSnapshot.Clear();
        inPlaceResetSnapshot.Clear();
        cachedInPlaceResettables.Clear();
    }

    private void Update()
    {
        if (state == FocusingRingState.Cooldown)
        {
            resetCooldownRemaining = ResetCooldownRemaining;
            if (resetCooldownRemaining <= 0f)
            {
                state = abilityAvailable ? FocusingRingState.Ready : FocusingRingState.Disabled;
                cooldownCompleted.Invoke();
            }
        }

        if (WasActivationPressed() && CanQueueResetRequest())
        {
            bufferedResetRequestExpiresAt = Time.unscaledTime + resetInputBufferDuration;
        }

        TryConsumeBufferedResetRequest();
    }

    public bool TryActivate()
    {
        if (resetInProgress || resetRoutine != null) return false;

        CacheSceneSpawners();
        BuildResetSnapshot();
        if (!CanActivate() || (resetSnapshot.Count == 0 && inPlaceResetSnapshot.Count == 0))
        {
            resetSnapshot.Clear();
            inPlaceResetSnapshot.Clear();
            return false;
        }

        resetInProgress = true;
        state = FocusingRingState.Resetting;
        resetRoutine = StartCoroutine(ResetRoutine());
        return true;
    }

    public void SetAbilityAvailable(bool available)
    {
        abilityAvailable = available;
        RefreshEnabledState();
    }

    public void RegisterSpawner(FocusingSpawner3D spawner)
    {
        if (spawner != null && !cachedSpawners.Contains(spawner)) cachedSpawners.Add(spawner);
    }

    public void CacheSceneSpawners()
    {
        cachedSpawners.Clear();
        cachedInPlaceResettables.Clear();
        for (int i = 0; i < explicitSpawners.Count; i++) RegisterSpawner(explicitSpawners[i]);

        if (includeRegisteredSpawners)
        {
            int registeredStart = cachedSpawners.Count;
            FocusingSpawner3D.CopyRegisteredTo(cachedSpawners);
            for (int i = cachedSpawners.Count - 1; i >= registeredStart; i--)
            {
                FocusingSpawner3D spawner = cachedSpawners[i];
                if (spawner == null || cachedSpawners.IndexOf(spawner) < i) cachedSpawners.RemoveAt(i);
            }
        }

        FocusingInPlaceResetRegistry3D.CopyRegisteredTo(cachedInPlaceResettables);

    }

    private bool CanActivate()
    {
        if (!abilityAvailable || state != FocusingRingState.Ready) return false;
        if (damageReceiver != null && damageReceiver.IsDead) return false;
        if (Time.timeScale <= 0f || FocusingRingBlocker3D.IsBlocked) return false;
        if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoadingScene) return false;
        if (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsLoading) return false;
        return HasResettableTarget();
    }

    private bool HasResettableTarget()
    {
        for (int i = 0; i < cachedSpawners.Count; i++)
        {
            FocusingSpawner3D spawner = cachedSpawners[i];
            if (spawner != null && spawner.IncludeInFullReset && !spawner.IsPermanentlyDisabled) return true;
        }
        for (int i = 0; i < cachedInPlaceResettables.Count; i++)
        {
            if (FocusingInPlaceResetRegistry3D.IsAlive(cachedInPlaceResettables[i])) return true;
        }
        return false;
    }

    private bool WasActivationPressed()
    {
        bool cameraModeActive = cameraAbilitySystem != null && cameraAbilitySystem.IsCameraModeActive;
        if (!cameraModeActive && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.middleButton.wasPressedThisFrame;
    }

    private bool CanQueueResetRequest()
    {
        if (!abilityAvailable || state != FocusingRingState.Ready || resetInProgress || resetRoutine != null) return false;
        if (damageReceiver != null && damageReceiver.IsDead) return false;
        if (Time.timeScale <= 0f || FocusingRingBlocker3D.IsBlocked) return false;
        if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoadingScene) return false;
        if (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsLoading) return false;
        return true;
    }

    private void TryConsumeBufferedResetRequest()
    {
        if (bufferedResetRequestExpiresAt < 0f) return;

        float currentTime = Time.unscaledTime;
        if (currentTime > bufferedResetRequestExpiresAt)
        {
            CacheSceneSpawners();
            if (!HasResettableTarget()) LogNoEligibleSpawners();
            ClearBufferedResetRequest();
            return;
        }

        if (!CanQueueResetRequest())
        {
            ClearBufferedResetRequest();
            return;
        }

        CacheSceneSpawners();
        if (!HasResettableTarget()) return;
        if (TryActivate()) ClearBufferedResetRequest();
    }

    private void ClearBufferedResetRequest()
    {
        bufferedResetRequestExpiresAt = -1f;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void LogNoEligibleSpawners()
    {
        Debug.LogWarning("[FocusingRing] Reset request received, but no eligible reset targets were registered.", this);
    }

    private IEnumerator ResetRoutine()
    {
        bool completed = false;
        try
        {
            resetStarted.Invoke();

            for (int i = 0; i < inPlaceResetSnapshot.Count; i++)
            {
                IFocusingInPlaceResettable3D resettable = inPlaceResetSnapshot[i];
                if (!FocusingInPlaceResetRegistry3D.IsAlive(resettable)) continue;
                try { resettable.ResetForFocusingRing(); }
                catch (System.Exception exception)
                {
                    if (resettable is Object context) Debug.LogException(exception, context);
                    else Debug.LogException(exception);
                }
            }

            for (int i = resetSnapshot.Count - 1; i >= 0; i--)
            {
                FocusingSpawner3D spawner = resetSnapshot[i];
                if (spawner == null) continue;
                try { spawner.PrepareForFullReset(); }
                catch (System.Exception exception) { Debug.LogException(exception, spawner); }
            }

            if (resetPresentationTime > 0f)
            {
                yield return new WaitForSecondsRealtime(resetPresentationTime);
            }
            else
            {
                yield return null;
            }

            for (int typeIndex = 0; typeIndex < SpawnOrder.Length; typeIndex++)
            {
                FocusingSpawnerType type = SpawnOrder[typeIndex];
                for (int i = 0; i < resetSnapshot.Count; i++)
                {
                    FocusingSpawner3D spawner = resetSnapshot[i];
                    if (spawner == null || spawner.SpawnerType != type || spawner.IsPermanentlyDisabled) continue;
                    try { spawner.CompleteFullReset(); }
                    catch (System.Exception exception) { Debug.LogException(exception, spawner); }
                }
            }

            resetCompleted.Invoke();
            completed = true;
        }
        finally
        {
            resetSnapshot.Clear();
            inPlaceResetSnapshot.Clear();
            resetRoutine = null;
            resetInProgress = false;
            if (completed)
            {
                resetCooldownUntil = Time.unscaledTime + resetCooldownDuration;
                resetCooldownRemaining = resetCooldownDuration;
                state = resetCooldownDuration > 0f ? FocusingRingState.Cooldown : FocusingRingState.Ready;
            }
            else
            {
                resetCooldownRemaining = 0f;
                state = abilityAvailable ? FocusingRingState.Ready : FocusingRingState.Disabled;
            }
        }
    }

    private void BuildResetSnapshot()
    {
        resetSnapshot.Clear();
        inPlaceResetSnapshot.Clear();
        for (int i = cachedSpawners.Count - 1; i >= 0; i--)
        {
            FocusingSpawner3D spawner = cachedSpawners[i];
            if (spawner == null)
            {
                cachedSpawners.RemoveAt(i);
                continue;
            }
            if (!spawner.IncludeInFullReset || spawner.IsPermanentlyDisabled) continue;
            if (!resetSnapshot.Contains(spawner)) resetSnapshot.Add(spawner);
        }

        for (int i = 0; i < cachedInPlaceResettables.Count; i++)
        {
            IFocusingInPlaceResettable3D resettable = cachedInPlaceResettables[i];
            if (!FocusingInPlaceResetRegistry3D.IsAlive(resettable)) continue;
            if (!inPlaceResetSnapshot.Contains(resettable)) inPlaceResetSnapshot.Add(resettable);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StopAllCoroutines();
        resetRoutine = null;
        resetInProgress = false;
        ClearBufferedResetRequest();
        resetSnapshot.Clear();
        inPlaceResetSnapshot.Clear();
        CacheSceneSpawners();
        RefreshEnabledState();
    }

    private void RefreshEnabledState()
    {
        state = abilityAvailable ? FocusingRingState.Ready : FocusingRingState.Disabled;
        resetCooldownRemaining = 0f;
    }

    private void OnValidate()
    {
        resetCooldownDuration = Mathf.Max(0f, resetCooldownDuration);
        resetPresentationTime = Mathf.Max(0f, resetPresentationTime);
        resetInputBufferDuration = Mathf.Max(0f, resetInputBufferDuration);
    }
}
