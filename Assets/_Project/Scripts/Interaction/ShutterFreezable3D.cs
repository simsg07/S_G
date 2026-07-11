using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ShutterFreezable3D : MonoBehaviour, IShutterFreezable3D
{
    [SerializeField] private Rigidbody targetBody; // 셔터로 멈출 Rigidbody입니다. 비워두면 같은 오브젝트에서 자동으로 찾습니다.
    [SerializeField] private Behaviour[] behavioursToPause = new Behaviour[0]; // 셔터로 멈출 때 추가로 비활성화할 스크립트 목록입니다.
    [SerializeField] private bool pauseListedBehaviours = true; // behavioursToPause 목록의 스크립트를 셔터 정지 중 함께 멈출지 정합니다.
    [SerializeField] private bool pauseSiblingBehaviours = true; // 사진으로 멈출 때 같은 오브젝트에 붙은 이동/AI 스크립트도 함께 멈출지 정합니다.
    [SerializeField] private bool pauseChildBehaviours = false; // 사진으로 멈출 때 자식 오브젝트의 스크립트까지 함께 멈출지 정합니다.

    [SerializeField] private bool pinTransformWhileFrozen = true; // 셔터 정지 중 스크립트나 코루틴이 위치를 밀어도 사진을 찍은 위치에 고정할지 정합니다.
    [SerializeField] private bool restoreFrozenTransformOnRelease = true; // 정지가 풀릴 때 오브젝트를 마지막으로 고정된 위치에서 다시 움직이게 할지 정합니다.

    private readonly List<BehaviourState> pausedBehaviours = new List<BehaviourState>();

    private bool isFrozen;
    private bool hadBody;
    private bool wasKinematic;
    private bool usedGravity;
    private Vector3 storedLinearVelocity;
    private Vector3 storedAngularVelocity;
    private Transform frozenTransform;
    private Vector3 frozenPosition;
    private Quaternion frozenRotation;
    private Vector3 frozenLocalScale;
    private float freezeEndTime;

    private void Awake()
    {
        if (targetBody == null)
        {
            targetBody = GetComponent<Rigidbody>();
        }
    }

    private void Update()
    {
        if (isFrozen && Time.time >= freezeEndTime)
        {
            ReleaseFreeze();
        }
    }

    private void LateUpdate()
    {
        if (isFrozen)
        {
            PinFrozenTransform();
        }
    }

    public bool ApplyShutterFreeze(float duration, CameraAbilitySystem3D source)
    {
        if (duration <= 0f)
        {
            return false;
        }

        if (!isFrozen)
        {
            CaptureAndFreezeBody();
            PauseBehaviours();
            isFrozen = true;
        }

        freezeEndTime = Mathf.Max(freezeEndTime, Time.time + duration);
        return true;
    }

    private void CaptureAndFreezeBody()
    {
        if (targetBody == null)
        {
            targetBody = GetComponent<Rigidbody>();
        }

        hadBody = targetBody != null;
        frozenTransform = hadBody ? targetBody.transform : transform;
        CaptureFrozenTransform();

        if (!hadBody)
        {
            return;
        }

        wasKinematic = targetBody.isKinematic;
        usedGravity = targetBody.useGravity;
        storedLinearVelocity = targetBody.linearVelocity;
        storedAngularVelocity = targetBody.angularVelocity;

        targetBody.linearVelocity = Vector3.zero;
        targetBody.angularVelocity = Vector3.zero;
        targetBody.useGravity = false;
        targetBody.isKinematic = true;
    }

    private void CaptureFrozenTransform()
    {
        if (frozenTransform == null)
        {
            return;
        }

        frozenPosition = frozenTransform.position;
        frozenRotation = frozenTransform.rotation;
        frozenLocalScale = frozenTransform.localScale;
    }

    private void PinFrozenTransform()
    {
        if (!pinTransformWhileFrozen || frozenTransform == null)
        {
            return;
        }

        RestoreFrozenTransform();
    }

    private void RestoreFrozenTransform()
    {
        if (frozenTransform == null)
        {
            return;
        }

        if (hadBody && targetBody != null)
        {
            targetBody.position = frozenPosition;
            targetBody.rotation = frozenRotation;
        }
        else
        {
            frozenTransform.SetPositionAndRotation(frozenPosition, frozenRotation);
        }

        frozenTransform.localScale = frozenLocalScale;
    }

    private void PauseBehaviours()
    {
        pausedBehaviours.Clear();
        if (pauseSiblingBehaviours)
        {
            Behaviour[] siblingBehaviours = GetComponents<Behaviour>();
            for (int i = 0; i < siblingBehaviours.Length; i++)
            {
                PauseBehaviour(siblingBehaviours[i]);
            }
        }

        if (pauseChildBehaviours)
        {
            Behaviour[] childBehaviours = GetComponentsInChildren<Behaviour>();
            for (int i = 0; i < childBehaviours.Length; i++)
            {
                PauseBehaviour(childBehaviours[i]);
            }
        }

        if (!pauseListedBehaviours)
        {
            return;
        }

        Behaviour[] behaviours = behavioursToPause ?? new Behaviour[0];
        for (int i = 0; i < behaviours.Length; i++)
        {
            PauseBehaviour(behaviours[i]);
        }
    }

    private void PauseBehaviour(Behaviour behaviour)
    {
        if (behaviour == null || behaviour == this || !behaviour.enabled || ShouldKeepRunningWhileFrozen(behaviour))
        {
            return;
        }

        for (int i = 0; i < pausedBehaviours.Count; i++)
        {
            if (pausedBehaviours[i].Behaviour == behaviour)
            {
                return;
            }
        }

        pausedBehaviours.Add(new BehaviourState(behaviour, true));
        behaviour.enabled = false;
    }

    private bool ShouldKeepRunningWhileFrozen(Behaviour behaviour)
    {
        return behaviour is CameraMarkState3D || behaviour is CameraObjectTag3D;
    }

    private void ReleaseFreeze()
    {
        if (restoreFrozenTransformOnRelease)
        {
            RestoreFrozenTransform();
        }

        if (hadBody && targetBody != null)
        {
            targetBody.isKinematic = wasKinematic;
            targetBody.useGravity = usedGravity;
            TwoPointFiveDUtility3D.ConfigureRigidbodyForSideView(targetBody);

            if (!targetBody.isKinematic)
            {
                targetBody.linearVelocity = TwoPointFiveDUtility3D.ProjectVelocityToPlane(storedLinearVelocity);
                targetBody.angularVelocity = Vector3.zero;
            }
        }

        for (int i = 0; i < pausedBehaviours.Count; i++)
        {
            BehaviourState state = pausedBehaviours[i];
            if (state.Behaviour != null)
            {
                state.Behaviour.enabled = state.WasEnabled;
            }
        }

        pausedBehaviours.Clear();
        isFrozen = false;
        freezeEndTime = 0f;
        frozenTransform = null;
    }

    private readonly struct BehaviourState
    {
        public BehaviourState(Behaviour behaviour, bool wasEnabled)
        {
            Behaviour = behaviour;
            WasEnabled = wasEnabled;
        }

        public Behaviour Behaviour { get; }
        public bool WasEnabled { get; }
    }
}
