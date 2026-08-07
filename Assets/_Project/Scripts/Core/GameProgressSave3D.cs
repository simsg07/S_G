using System.Collections.Generic;
using UnityEngine;

public static class GameProgressSave3D
{
    private const string PlayerPrefsKey = "S_G_CameraMetroidvaniaProgress";

    private static SavePayload cachedPayload;

    public static CameraAbilityFlags GetUnlockedAbilities()
    {
        SavePayload payload = LoadPayload();
        CameraAbilityFlags abilities = CameraAbilityFlags.None;

        for (int i = 0; i < payload.unlockedAbilities.Count; i++)
        {
            if (System.Enum.TryParse(payload.unlockedAbilities[i], out CameraAbilityId ability))
            {
                abilities |= CameraAbilitySystem3D.ToFlag(ability);
            }
        }

        return abilities;
    }

    public static bool IsItemCollected(string itemId)
    {
        return Contains(LoadPayload().collectedItems, itemId);
    }

    public static bool IsDeviceActivated(string deviceId)
    {
        return Contains(LoadPayload().activatedDevices, deviceId);
    }

    public static bool IsAreaExplored(string areaId)
    {
        return Contains(LoadPayload().exploredAreas, areaId);
    }

    public static bool HiddenEndingUnlocked()
    {
        return LoadPayload().hiddenEndingUnlocked;
    }

    public static bool IsCheckpointActivated(string checkpointId)
    {
        return Contains(LoadPayload().activatedCheckpointIds, checkpointId);
    }

    public static bool IsPuzzlePermanentlyCompleted(string puzzleId)
    {
        return Contains(LoadPayload().completedPuzzleIds, puzzleId);
    }

    public static void RecordPuzzlePermanentlyCompleted(string puzzleId)
    {
        if (string.IsNullOrWhiteSpace(puzzleId)) return;
        SavePayload payload = LoadPayload();
        AddUnique(payload.completedPuzzleIds, puzzleId);
        payload.saveVersion = Mathf.Max(payload.saveVersion, 4);
        WritePayload(payload);
    }

    public static bool TryGetLastCheckpoint(out string sceneName, out string checkpointId)
    {
        SavePayload payload = LoadPayload();
        sceneName = payload.lastCheckpointScene;
        checkpointId = payload.lastCheckpointId;
        return !string.IsNullOrWhiteSpace(sceneName) && !string.IsNullOrWhiteSpace(checkpointId);
    }

    public static void RecordCheckpointActivated(string sceneName, string checkpointId)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(checkpointId))
        {
            Debug.LogWarning("[GameProgressSave3D] Checkpoint scene and ID are required. Save skipped.");
            return;
        }

        SavePayload payload = LoadPayload();
        AddUnique(payload.activatedCheckpointIds, checkpointId);
        payload.lastCheckpointScene = sceneName;
        payload.lastCheckpointId = checkpointId;
        payload.saveVersion = Mathf.Max(payload.saveVersion, 2);
        WritePayload(payload);
    }

    public static bool TryGetPersistentObjectState(string sceneName, string persistentId, out PersistentSceneObjectState state)
    {
        state = PersistentSceneObjectState.Exists;
        if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(persistentId)) return false;
        List<PersistentObjectRecord> records = LoadPayload().persistentObjectStates;
        for (int i = 0; i < records.Count; i++)
        {
            if (records[i] != null
                && string.Equals(records[i].sceneName, sceneName, System.StringComparison.Ordinal)
                && string.Equals(records[i].persistentId, persistentId, System.StringComparison.Ordinal))
            {
                state = records[i].savedState;
                return true;
            }
        }
        return false;
    }

    public static void RecordPersistentObjectState(string sceneName, string persistentId, PersistentSceneObjectState state)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(persistentId)) return;
        SavePayload payload = LoadPayload();
        PersistentObjectRecord record = payload.persistentObjectStates.Find(item =>
            item != null
            && string.Equals(item.sceneName, sceneName, System.StringComparison.Ordinal)
            && string.Equals(item.persistentId, persistentId, System.StringComparison.Ordinal));
        if (record != null && record.savedState == state && record.sceneName == sceneName) return;
        if (record == null)
        {
            record = new PersistentObjectRecord();
            payload.persistentObjectStates.Add(record);
        }
        record.sceneName = sceneName;
        record.persistentId = persistentId;
        record.savedState = state;
        payload.saveVersion = Mathf.Max(payload.saveVersion, 3);
        WritePayload(payload);
    }

    public static void SaveNow()
    {
        if (cachedPayload != null) PlayerPrefs.Save();
    }

    public static void RecordAbilityUnlocked(CameraAbilityId ability)
    {
        SavePayload payload = LoadPayload();
        AddUnique(payload.unlockedAbilities, ability.ToString());
        WritePayload(payload);
    }

    public static void RecordItemCollected(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        SavePayload payload = LoadPayload();
        AddUnique(payload.collectedItems, itemId);
        WritePayload(payload);
    }

    public static void RecordDeviceActivated(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        SavePayload payload = LoadPayload();
        AddUnique(payload.activatedDevices, deviceId);
        WritePayload(payload);
    }

    public static void RecordAreaExplored(string areaId)
    {
        if (string.IsNullOrWhiteSpace(areaId))
        {
            return;
        }

        SavePayload payload = LoadPayload();
        AddUnique(payload.exploredAreas, areaId);
        WritePayload(payload);
    }

    public static void SetCurrentWorld(ResearchWorldId world)
    {
        SavePayload payload = LoadPayload();
        payload.currentWorld = world;
        WritePayload(payload);
    }

    public static void SetHiddenEndingUnlocked(bool unlocked)
    {
        SavePayload payload = LoadPayload();
        payload.hiddenEndingUnlocked = unlocked;
        WritePayload(payload);
    }

    public static void ResetProgress()
    {
        cachedPayload = new SavePayload();
        PlayerPrefs.DeleteKey(PlayerPrefsKey);
        PlayerPrefs.Save();
    }

    private static SavePayload LoadPayload()
    {
        if (cachedPayload != null)
        {
            return cachedPayload;
        }

        string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            cachedPayload = new SavePayload();
            return cachedPayload;
        }

        cachedPayload = JsonUtility.FromJson<SavePayload>(json);
        if (cachedPayload == null)
        {
            cachedPayload = new SavePayload();
        }

        cachedPayload.EnsureLists();
        return cachedPayload;
    }

    private static void WritePayload(SavePayload payload)
    {
        payload.EnsureLists();
        cachedPayload = payload;
        PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(payload));
        PlayerPrefs.Save();
    }

    private static bool Contains(List<string> values, string value)
    {
        return !string.IsNullOrWhiteSpace(value) && values.Contains(value);
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (!values.Contains(value))
        {
            values.Add(value);
        }
    }

    [System.Serializable]
    private class SavePayload
    {
        public int saveVersion = 2;
        public List<string> unlockedAbilities = new List<string>();
        public List<string> collectedItems = new List<string>();
        public List<string> activatedDevices = new List<string>();
        public List<string> exploredAreas = new List<string>();
        public List<string> activatedCheckpointIds = new List<string>();
        public List<string> completedPuzzleIds = new List<string>();
        public string lastCheckpointScene = string.Empty;
        public string lastCheckpointId = string.Empty;
        public List<PersistentObjectRecord> persistentObjectStates = new List<PersistentObjectRecord>();
        public ResearchWorldId currentWorld = ResearchWorldId.WorldA;
        public bool hiddenEndingUnlocked;

        public void EnsureLists()
        {
            if (unlockedAbilities == null)
            {
                unlockedAbilities = new List<string>();
            }

            if (collectedItems == null)
            {
                collectedItems = new List<string>();
            }

            if (activatedDevices == null)
            {
                activatedDevices = new List<string>();
            }

            if (exploredAreas == null)
            {
                exploredAreas = new List<string>();
            }

            if (activatedCheckpointIds == null)
            {
                activatedCheckpointIds = new List<string>();
            }

            if (completedPuzzleIds == null)
            {
                completedPuzzleIds = new List<string>();
            }

            if (persistentObjectStates == null)
            {
                persistentObjectStates = new List<PersistentObjectRecord>();
            }

            saveVersion = Mathf.Max(saveVersion, 1);
        }
    }

    [System.Serializable]
    private class PersistentObjectRecord
    {
        public string sceneName = string.Empty;
        public string persistentId = string.Empty;
        public PersistentSceneObjectState savedState = PersistentSceneObjectState.Exists;
    }
}
