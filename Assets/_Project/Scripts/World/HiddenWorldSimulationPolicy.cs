using UnityEngine;

public enum HiddenWorldSimulationPolicy
{
    [InspectorName("Pause When Hidden")]
    PauseWhenHidden = 0,
    [InspectorName("Continue Monster Logic")]
    ContinueMonsterLogic = 1
}
