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
        public List<string> unlockedAbilities = new List<string>();
        public List<string> collectedItems = new List<string>();
        public List<string> activatedDevices = new List<string>();
        public List<string> exploredAreas = new List<string>();
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
        }
    }
}
