public interface ITriggerableObject
{
    bool CanTrigger { get; }

    void TriggerObject();

    void ResetObject();
}
