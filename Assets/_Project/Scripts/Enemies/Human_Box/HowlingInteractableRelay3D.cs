using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[AddComponentMenu("_Project/Puzzle/Howling Interactable Relay 3D")]
public sealed class HowlingInteractableRelay3D : MonoBehaviour, IHowlingInteractable3D
{
    [SerializeField] private UnityEvent onHowlingActivated = new UnityEvent();

    public void OnHowlingActivated(GameObject source)
    {
        if (isActiveAndEnabled) onHowlingActivated.Invoke();
    }
}
