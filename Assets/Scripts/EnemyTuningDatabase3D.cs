using UnityEngine;

public enum EnemyDetectionProfile3D
{
    Melee,
    Ranged
}

[System.Serializable]
public class EnemyDetectionSettings3D
{
    [SerializeField] private string displayName = "Detection"; // 인스펙터에서 구분하기 위한 감지 설정 이름입니다.
    [SerializeField] private float detectionSize = 3.8f; // 해당 몬스터 타입의 정사각형 인식 범위 크기입니다.
    [SerializeField] private Color detectionColor = new Color(0.25f, 0.45f, 1f, 0.18f); // 인식 범위 미리보기 색상입니다.

    public float DetectionSize => Mathf.Max(0.1f, detectionSize);
    public Color DetectionColor => detectionColor;
}

[CreateAssetMenu(fileName = "EnemyTuningDatabase", menuName = "S_G/Enemy Tuning Database")]
public class EnemyTuningDatabase3D : ScriptableObject
{
    [SerializeField] private EnemyDetectionSettings3D meleeDetection = new EnemyDetectionSettings3D(); // 근거리 몬스터가 공통으로 사용할 인식 범위 설정입니다.
    [SerializeField] private EnemyDetectionSettings3D rangedDetection = new EnemyDetectionSettings3D(); // 원거리 몬스터가 공통으로 사용할 인식 범위 설정입니다.

    public EnemyDetectionSettings3D GetDetectionSettings(EnemyDetectionProfile3D profile)
    {
        return profile == EnemyDetectionProfile3D.Ranged ? rangedDetection : meleeDetection;
    }

    public float GetDetectionSize(EnemyDetectionProfile3D profile, float fallback)
    {
        EnemyDetectionSettings3D settings = GetDetectionSettings(profile);
        return settings != null ? settings.DetectionSize : Mathf.Max(0.1f, fallback);
    }

    public Color GetDetectionColor(EnemyDetectionProfile3D profile, Color fallback)
    {
        EnemyDetectionSettings3D settings = GetDetectionSettings(profile);
        return settings != null ? settings.DetectionColor : fallback;
    }
}
