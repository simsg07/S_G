using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

public static class CameraToggleLeakValidationUtility
{
    private const int ToggleCycles = 60;
    private static readonly MethodInfo EnterMethod = typeof(CameraAbilitySystem3D).GetMethod("EnterCameraMode", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo ExitMethod = typeof(CameraAbilitySystem3D).GetMethod("ExitCameraMode", BindingFlags.Instance | BindingFlags.NonPublic);

    private static CameraAbilitySystem3D ability;
    private static Snapshot before;
    private static int phase;
    private static int cycles;
    private static int slowEnterEvents;
    private static int slowExitEvents;
    private static float originalTimeScale;
    private static float originalFixedDeltaTime;
    private static int findRetries;

    [MenuItem("_Project/Test/Camera Toggle Accumulation Test %#k")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[CameraToggleLeakTest] Enter Play Mode before running the test.");
            return;
        }

        Stop();
        ability = UnityEngine.Object.FindFirstObjectByType<CameraAbilitySystem3D>();
        if (ability == null && EditorApplication.isPlaying && findRetries++ < 300)
        {
            EditorApplication.delayCall += Run;
            return;
        }

        findRetries = 0;
        if (ability == null)
        {
            Debug.LogError("[CameraToggleLeakTest] CameraAbilitySystem3D was not found after waiting for scene bootstrap.");
            return;
        }

        if (EnterMethod == null || ExitMethod == null)
        {
            Debug.LogError("[CameraToggleLeakTest] Camera transition methods were not found.");
            return;
        }

        ExitMethod.Invoke(ability, new object[] { "Leak validation setup", false });
        originalTimeScale = Time.timeScale;
        originalFixedDeltaTime = Time.fixedDeltaTime;
        ability.ResetCameraTransitionDiagnostics();
        before = Snapshot.Capture();
        phase = 0;
        cycles = 0;
        slowEnterEvents = 0;
        slowExitEvents = 0;
        CameraAbilitySystem3D.CameraSlowMotionChanged += HandleSlowMotionChanged;
        EditorApplication.update += Tick;
        Debug.Log($"[CameraToggleLeakTest] Started {ToggleCycles} frame-separated ON/OFF cycles. Before: {before}");
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || ability == null)
        {
            Stop();
            return;
        }

        if (phase == 0)
        {
            EnterMethod.Invoke(ability, new object[] { "Leak validation" });
            phase = 1;
            return;
        }

        if (phase == 1)
        {
            ExitMethod.Invoke(ability, new object[] { "Leak validation", false });
            cycles++;
            if (cycles == 10 || cycles == 30 || cycles == 60)
            {
                Debug.Log($"[CameraToggleLeakTest] Snapshot {cycles}: {Snapshot.Capture()} / "
                    + $"Transitions={ability.DebugActualEnterCount}/{ability.DebugApplySlowMotionCount}/"
                    + $"{ability.DebugActualExitCount}/{ability.DebugRestoreSlowMotionCount} / "
                    + $"fixedDeltaTime={Time.fixedDeltaTime:0.########}");
            }
            phase = cycles >= ToggleCycles ? 2 : 0;
            return;
        }

        Snapshot after = Snapshot.Capture();
        bool resourcesStable = before.HasSameResourceCounts(after);
        bool eventCountsValid = slowEnterEvents == ToggleCycles && slowExitEvents == ToggleCycles;
        bool transitionCountsValid = ability.DebugActualEnterCount == ToggleCycles
            && ability.DebugApplySlowMotionCount == ToggleCycles
            && ability.DebugActualExitCount == ToggleCycles
            && ability.DebugRestoreSlowMotionCount == ToggleCycles;
        bool timeRestored = Mathf.Approximately(Time.timeScale, originalTimeScale)
            && Mathf.Approximately(Time.fixedDeltaTime, originalFixedDeltaTime);
        bool errorsStable = after.ConsoleErrors == before.ConsoleErrors;
        bool passed = resourcesStable && eventCountsValid && transitionCountsValid && timeRestored && errorsStable;

        string result = $"[CameraToggleLeakTest] {(passed ? "PASS" : "FAIL")} after {ToggleCycles} cycles. "
            + $"Before: {before} / After: {after} / SlowEvents={slowEnterEvents}/{slowExitEvents} / TimeRestored={timeRestored}";
        result += $" / Transitions={ability.DebugActualEnterCount}/{ability.DebugApplySlowMotionCount}/"
            + $"{ability.DebugActualExitCount}/{ability.DebugRestoreSlowMotionCount}"
            + $" / Blocked={ability.DebugDuplicateTransitionBlockCount}";
        if (passed) Debug.Log(result);
        else Debug.LogError(result);
        Stop();
    }

    private static void HandleSlowMotionChanged(bool active)
    {
        if (active) slowEnterEvents++;
        else slowExitEvents++;
    }

    private static void Stop()
    {
        EditorApplication.update -= Tick;
        CameraAbilitySystem3D.CameraSlowMotionChanged -= HandleSlowMotionChanged;
        ability = null;
    }

    private readonly struct Snapshot
    {
        public readonly int SceneObjects;
        public readonly int CameraFrames;
        public readonly int CameraLights;
        public readonly int GeneratedTextures;
        public readonly int EnabledCameras;
        public readonly int EnabledAudioListeners;
        public readonly int ConsoleErrors;
        public readonly long AllocatedMemory;

        private Snapshot(int sceneObjects, int cameraFrames, int cameraLights, int generatedTextures, int enabledCameras, int enabledAudioListeners, int consoleErrors, long allocatedMemory)
        {
            SceneObjects = sceneObjects;
            CameraFrames = cameraFrames;
            CameraLights = cameraLights;
            GeneratedTextures = generatedTextures;
            EnabledCameras = enabledCameras;
            EnabledAudioListeners = enabledAudioListeners;
            ConsoleErrors = consoleErrors;
            AllocatedMemory = allocatedMemory;
        }

        public static Snapshot Capture()
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            Light[] lights = Resources.FindObjectsOfTypeAll<Light>();
            Texture2D[] textures = Resources.FindObjectsOfTypeAll<Texture2D>();
            Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
            AudioListener[] listeners = Resources.FindObjectsOfTypeAll<AudioListener>();

            return new Snapshot(
                CountSceneObjects(objects),
                CountNamed(canvases, "Camera Ability Frame"),
                CountNamed(lights, "Camera Toggle Light"),
                CountGeneratedTextures(textures),
                CountEnabled(cameras),
                CountEnabled(listeners),
                GetConsoleErrorCount(),
                Profiler.GetTotalAllocatedMemoryLong());
        }

        public bool HasSameResourceCounts(Snapshot other)
        {
            return SceneObjects == other.SceneObjects
                && CameraFrames == other.CameraFrames
                && CameraLights == other.CameraLights
                && GeneratedTextures == other.GeneratedTextures
                && EnabledCameras == other.EnabledCameras
                && EnabledAudioListeners == other.EnabledAudioListeners;
        }

        public override string ToString()
        {
            return $"Objects={SceneObjects}, Frames={CameraFrames}, Lights={CameraLights}, Textures={GeneratedTextures}, "
                + $"Cameras={EnabledCameras}, AudioListeners={EnabledAudioListeners}, Errors={ConsoleErrors}, Memory={AllocatedMemory}";
        }

        private static int CountSceneObjects(GameObject[] objects)
        {
            int count = 0;
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null && objects[i].scene.IsValid()) count++;
            }
            return count;
        }

        private static int CountNamed<T>(T[] components, string targetName) where T : Component
        {
            int count = 0;
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component != null && component.gameObject.scene.IsValid() && component.name == targetName) count++;
            }
            return count;
        }

        private static int CountGeneratedTextures(Texture2D[] textures)
        {
            int count = 0;
            for (int i = 0; i < textures.Length; i++)
            {
                Texture2D texture = textures[i];
                if (texture != null && (texture.name == "Generated Camera Ring Texture" || texture.name == "Generated Camera Dot Texture")) count++;
            }
            return count;
        }

        private static int CountEnabled<T>(T[] components) where T : Behaviour
        {
            int count = 0;
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component != null && component.gameObject.scene.IsValid() && component.isActiveAndEnabled) count++;
            }
            return count;
        }

        private static int GetConsoleErrorCount()
        {
            Type logEntriesType = Type.GetType("UnityEditor.LogEntries,UnityEditor");
            MethodInfo method = logEntriesType?.GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null) return -1;
            object[] arguments = { 0, 0, 0 };
            method.Invoke(null, arguments);
            return (int)arguments[0];
        }
    }
}
