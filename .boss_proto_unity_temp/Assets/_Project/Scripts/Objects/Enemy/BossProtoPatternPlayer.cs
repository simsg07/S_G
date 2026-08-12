using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class BossProtoPatternPlayer : MonoBehaviour
{
    [Header("Pattern Clips")]
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip pattern1Clip;
    [SerializeField] private AnimationClip pattern2Clip;
    [SerializeField] private AnimationClip pattern3Clip;
    [SerializeField] private AnimationClip pattern4Clip;

    [Header("Playback")]
    [SerializeField] private bool autoPlayPatterns = true;
    [SerializeField, Min(0f)] private float startDelay = 2f;
    [SerializeField, Min(0f)] private float patternInterval = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool debugMode;
    [SerializeField] private string currentCycleOrder = "Not shuffled yet";
    [SerializeField, Min(0)] private int currentPatternIndex;
    [SerializeField] private int lastPlayedPattern;
    [SerializeField] private bool isPatternPlaying;

    private readonly List<int> patternBag = new List<int>(4);
    private Animator animator;
    private Coroutine playbackRoutine;

    public string CurrentCycleOrder => currentCycleOrder;
    public int CurrentPatternIndex => currentPatternIndex;
    public int LastPlayedPattern => lastPlayedPattern;
    public bool IsPatternPlaying => isPatternPlaying;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        PlayIdle();
        if (Application.isPlaying && autoPlayPatterns)
        {
            playbackRoutine = StartCoroutine(PlaybackLoop());
        }
    }

    private void OnDisable()
    {
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }

        isPatternPlaying = false;
    }

    private IEnumerator PlaybackLoop()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        while (autoPlayPatterns)
        {
            PlayNextPattern();
            yield return new WaitUntil(() => !isPatternPlaying);

            if (patternInterval > 0f)
            {
                yield return new WaitForSeconds(patternInterval);
            }
        }

        playbackRoutine = null;
    }

    public void PlayNextPattern()
    {
        if (isPatternPlaying)
        {
            return;
        }

        if (patternBag.Count != 4 || currentPatternIndex >= patternBag.Count)
        {
            ShufflePatterns();
        }

        int pattern = patternBag[currentPatternIndex++];
        AnimationClip clip = GetPatternClip(pattern);
        if (clip == null)
        {
            Debug.LogError($"[BossProtoPatternPlayer] Pattern{pattern} has no Animation Clip.", this);
            isPatternPlaying = false;
            PlayIdle();
            return;
        }

        lastPlayedPattern = pattern;
        isPatternPlaying = true;
        animator.Play($"Pattern{pattern}", 0, 0f);
        Log($"현재 실행 패턴: Pattern{pattern} / 현재 사이클 진행: {currentPatternIndex}/4");
    }

    public void OnPatternAnimationFinished()
    {
        if (!isPatternPlaying)
        {
            return;
        }

        isPatternPlaying = false;
        PlayIdle();
    }

    public void ShufflePatterns()
    {
        patternBag.Clear();
        patternBag.Add(1);
        patternBag.Add(2);
        patternBag.Add(3);
        patternBag.Add(4);

        for (int i = patternBag.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (patternBag[i], patternBag[swapIndex]) = (patternBag[swapIndex], patternBag[i]);
        }

        if (lastPlayedPattern != 0 && patternBag[0] == lastPlayedPattern)
        {
            int swapIndex = Random.Range(1, patternBag.Count);
            (patternBag[0], patternBag[swapIndex]) = (patternBag[swapIndex], patternBag[0]);
        }

        currentPatternIndex = 0;
        currentCycleOrder = string.Join(", ", patternBag);
        Log($"새 사이클 순서: {currentCycleOrder}");
    }

    public void ConfigureClips(
        AnimationClip idle,
        AnimationClip pattern1,
        AnimationClip pattern2,
        AnimationClip pattern3,
        AnimationClip pattern4)
    {
        idleClip = idle;
        pattern1Clip = pattern1;
        pattern2Clip = pattern2;
        pattern3Clip = pattern3;
        pattern4Clip = pattern4;
    }

    private AnimationClip GetPatternClip(int pattern)
    {
        return pattern switch
        {
            1 => pattern1Clip,
            2 => pattern2Clip,
            3 => pattern3Clip,
            4 => pattern4Clip,
            _ => null,
        };
    }

    private void PlayIdle()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator != null && idleClip != null)
        {
            animator.Play("Idle", 0, 0f);
        }
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[BossProtoPatternPlayer] {message}", this);
        }
    }
}
