using UnityEngine;
using UnityEngine.Events;

public class FlashReactive3D : MonoBehaviour, IFlashReactive3D
{
    [SerializeField] private string interactionId;
    [SerializeField] private bool oneShot = true;
    [SerializeField] private bool restoreSavedState = true;
    [SerializeField] private GameObject[] revealObjects = new GameObject[0];
    [SerializeField] private GameObject[] hideObjects = new GameObject[0];
    [SerializeField] private Behaviour[] enableBehaviours = new Behaviour[0];
    [SerializeField] private Collider[] enableColliders = new Collider[0];
    [SerializeField] private UnityEvent onFlash;

    private bool activated;

    private void Start()
    {
        if (restoreSavedState && !string.IsNullOrWhiteSpace(interactionId))
        {
            activated = GameProgressSave3D.IsItemCollected(interactionId);
            ApplyActivatedState(activated);
        }
    }

    public bool OnCameraFlash(CameraAbilitySystem3D source)
    {
        if (oneShot && activated)
        {
            return false;
        }

        activated = true;
        ApplyActivatedState(true);

        if (!string.IsNullOrWhiteSpace(interactionId))
        {
            GameProgressSave3D.RecordItemCollected(interactionId);
        }

        onFlash?.Invoke();
        return true;
    }

    private void ApplyActivatedState(bool isActivated)
    {
        GameObject[] revealTargets = revealObjects ?? new GameObject[0];
        for (int i = 0; i < revealTargets.Length; i++)
        {
            if (revealTargets[i] != null)
            {
                revealTargets[i].SetActive(isActivated);
            }
        }

        GameObject[] hideTargets = hideObjects ?? new GameObject[0];
        for (int i = 0; i < hideTargets.Length; i++)
        {
            if (hideTargets[i] != null)
            {
                hideTargets[i].SetActive(!isActivated);
            }
        }

        Behaviour[] behaviours = enableBehaviours ?? new Behaviour[0];
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
            {
                behaviours[i].enabled = isActivated;
            }
        }

        Collider[] colliders = enableColliders ?? new Collider[0];
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = isActivated;
            }
        }
    }
}
