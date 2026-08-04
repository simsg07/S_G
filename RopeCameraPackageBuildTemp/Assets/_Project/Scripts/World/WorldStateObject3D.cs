using System;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class WorldStateObject3D : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private bool previewInEditMode; // 에디터에서 현재 월드 상태를 미리 적용할지 정합니다.
    [SerializeField] private bool includeChildRenderers = true; // 자식 렌더러까지 월드별 표시/색상 상태를 적용할지 정합니다.
    [SerializeField] private bool includeChildColliders = true; // 자식 콜라이더까지 월드별 충돌 상태를 적용할지 정합니다.
    [SerializeField] private bool includeChildBehaviours = false; // 자식 스크립트까지 월드별 작동 상태를 적용할지 정합니다.
    [SerializeField] private Renderer[] extraRenderers = new Renderer[0]; // 자동 검색 외에 추가로 상태를 적용할 렌더러입니다.
    [SerializeField] private Collider[] extraColliders = new Collider[0]; // 자동 검색 외에 추가로 상태를 적용할 콜라이더입니다.
    [SerializeField] private Behaviour[] extraBehaviours = new Behaviour[0]; // 자동 검색 외에 추가로 작동 여부를 적용할 스크립트입니다.
    [SerializeField] private GameObject[] extraObjects = new GameObject[0]; // 월드별 활성 상태를 같이 적용할 부가 오브젝트입니다.
    [SerializeField] private WorldObjectState3D worldA = WorldObjectState3D.CreateDefault(Color.white); // 월드 A에서 이 오브젝트가 가질 상태입니다.
    [SerializeField] private WorldObjectState3D worldB = WorldObjectState3D.CreateDefault(Color.white); // 월드 B에서 이 오브젝트가 가질 상태입니다.

    private Renderer[] cachedRenderers = new Renderer[0];
    private Collider[] cachedColliders = new Collider[0];
    private Behaviour[] cachedBehaviours = new Behaviour[0];
    private Vector3 baseLocalPosition;
    private Vector3 baseLocalScale;
    private bool baseTransformCached;

    private void Awake()
    {
        CacheBaseTransform(false);
        CacheTargets();
    }

    private void OnEnable()
    {
        CacheBaseTransform(false);
        CacheTargets();
        WorldSystem3D.ActiveWorldChanged += HandleWorldChanged;
        Apply(WorldSystem3D.ActiveWorld);
    }

    private void OnDisable()
    {
        WorldSystem3D.ActiveWorldChanged -= HandleWorldChanged;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CacheBaseTransform(true);
        }

        CacheTargets();
        if (!Application.isPlaying && ShouldSkipEditorPreviewForWorldSwitchable())
        {
            return;
        }

        Apply(WorldSystem3D.ActiveWorld);
    }

    public void Apply(ResearchWorldId world)
    {
        if (!Application.isPlaying && !previewInEditMode)
        {
            return;
        }

        WorldObjectState3D state = GetState(world);
        bool enabledInWorld = state.enabledInWorld;

        ApplyTransformState(state);
        ApplyRendererState(state, enabledInWorld);
        ApplyColliderState(state, enabledInWorld);
        ApplyBehaviourState(state, enabledInWorld);
        ApplyExtraObjectState(enabledInWorld);
    }

    public WorldObjectState3D GetState(ResearchWorldId world)
    {
        return world == ResearchWorldId.WorldA ? worldA : worldB;
    }

    private void HandleWorldChanged(ResearchWorldId previousWorld, ResearchWorldId nextWorld)
    {
        Apply(nextWorld);
    }

    private bool ShouldSkipEditorPreviewForWorldSwitchable()
    {
        return TryGetComponent(out WorldSwitchable switchable)
            && switchable.ShowInEditor
            && switchable.EditorPreviewMode == WorldSwitchableEditorPreviewMode.AlwaysVisible;
    }

    private void CacheBaseTransform(bool force)
    {
        if (baseTransformCached && !force)
        {
            return;
        }

        baseLocalPosition = transform.localPosition;
        baseLocalScale = transform.localScale;
        baseTransformCached = true;
    }

    private void CacheTargets()
    {
        cachedRenderers = includeChildRenderers
            ? MergeTargets(GetComponentsInChildren<Renderer>(true), extraRenderers)
            : MergeTargets(GetComponents<Renderer>(), extraRenderers);

        cachedColliders = includeChildColliders
            ? MergeTargets(GetComponentsInChildren<Collider>(true), extraColliders)
            : MergeTargets(GetComponents<Collider>(), extraColliders);

        Behaviour[] autoBehaviours = includeChildBehaviours
            ? GetComponentsInChildren<Behaviour>(true)
            : GetComponents<Behaviour>();
        cachedBehaviours = MergeBehaviours(autoBehaviours, extraBehaviours);
    }

    private void ApplyTransformState(WorldObjectState3D state)
    {
        Vector3 nextPosition = baseLocalPosition;
        if (state.useLocalPositionOffset)
        {
            nextPosition += state.localPositionOffset;
        }

        if (state.doorOpen)
        {
            nextPosition += state.doorOpenLocalOffset;
        }

        transform.localPosition = nextPosition;
        TwoPointFiveDUtility3D.ClampTransformToPlane(transform);

        if (state.useLocalScaleOverride)
        {
            transform.localScale = state.localScaleOverride;
        }
        else
        {
            transform.localScale = baseLocalScale;
        }
    }

    private void ApplyRendererState(WorldObjectState3D state, bool enabledInWorld)
    {
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer target = cachedRenderers[i];
            if (target == null)
            {
                continue;
            }

            target.enabled = enabledInWorld && state.rendererEnabled;
            ApplyRendererColor(target, enabledInWorld && state.useColorOverride, state.colorOverride);
        }
    }

    private void ApplyColliderState(WorldObjectState3D state, bool enabledInWorld)
    {
        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider target = cachedColliders[i];
            if (target != null)
            {
                target.enabled = enabledInWorld && state.collisionEnabled;
            }
        }
    }

    private void ApplyBehaviourState(WorldObjectState3D state, bool enabledInWorld)
    {
        for (int i = 0; i < cachedBehaviours.Length; i++)
        {
            Behaviour target = cachedBehaviours[i];
            if (target != null)
            {
                target.enabled = enabledInWorld && state.operationEnabled;
            }
        }
    }

    private void ApplyExtraObjectState(bool enabledInWorld)
    {
        GameObject[] objects = extraObjects ?? new GameObject[0];
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
            {
                objects[i].SetActive(enabledInWorld);
            }
        }
    }

    private static void ApplyRendererColor(Renderer target, bool useColor, Color color)
    {
        if (target == null)
        {
            return;
        }

        if (!useColor)
        {
            target.SetPropertyBlock(null);
            return;
        }

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        target.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        target.SetPropertyBlock(block);
    }

    private static T[] MergeTargets<T>(T[] automaticTargets, T[] extraTargets) where T : Component
    {
        int automaticCount = automaticTargets != null ? automaticTargets.Length : 0;
        int extraCount = extraTargets != null ? extraTargets.Length : 0;
        T[] merged = new T[automaticCount + extraCount];
        int count = 0;

        for (int i = 0; i < automaticCount; i++)
        {
            AddUnique(merged, ref count, automaticTargets[i]);
        }

        for (int i = 0; i < extraCount; i++)
        {
            AddUnique(merged, ref count, extraTargets[i]);
        }

        Array.Resize(ref merged, count);
        return merged;
    }

    private Behaviour[] MergeBehaviours(Behaviour[] automaticTargets, Behaviour[] extraTargets)
    {
        int automaticCount = automaticTargets != null ? automaticTargets.Length : 0;
        int extraCount = extraTargets != null ? extraTargets.Length : 0;
        Behaviour[] merged = new Behaviour[automaticCount + extraCount];
        int count = 0;

        for (int i = 0; i < automaticCount; i++)
        {
            Behaviour target = automaticTargets[i];
            if (ShouldControlBehaviour(target))
            {
                AddUnique(merged, ref count, target);
            }
        }

        for (int i = 0; i < extraCount; i++)
        {
            Behaviour target = extraTargets[i];
            if (ShouldControlBehaviour(target))
            {
                AddUnique(merged, ref count, target);
            }
        }

        Array.Resize(ref merged, count);
        return merged;
    }

    private bool ShouldControlBehaviour(Behaviour target)
    {
        return target != null
            && target != this
            && !(target is WorldSwitchable)
            && !(target is WorldSystem3D)
            && !(target is WorldVariant3D)
            && !(target is WorldStateObject3D);
    }

    private static void AddUnique<T>(T[] targets, ref int count, T target) where T : Component
    {
        if (target == null)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (targets[i] == target)
            {
                return;
            }
        }

        targets[count] = target;
        count++;
    }
}

[Serializable]
public class WorldObjectState3D
{
    public bool enabledInWorld = true; // 이 월드에서 오브젝트가 존재하는지 정합니다. 끄면 표시, 충돌, 작동이 모두 꺼집니다.
    public bool rendererEnabled = true; // 이 월드에서 오브젝트가 화면에 보이는지 정합니다.
    public bool collisionEnabled = true; // 이 월드에서 오브젝트의 충돌 판정이 켜지는지 정합니다.
    public bool operationEnabled = true; // 이 월드에서 버튼, 터미널, 발판 같은 작동 스크립트가 켜지는지 정합니다.
    public bool useColorOverride; // 이 월드에서 별도 색상을 적용할지 정합니다.
    public Color colorOverride = Color.white; // useColorOverride가 켜졌을 때 적용할 월드별 색상입니다.
    public bool doorOpen; // 문 오브젝트라면 이 월드에서 열린 상태인지 표시합니다.
    public Vector3 doorOpenLocalOffset; // doorOpen이 켜졌을 때 공통 위치에서 문을 얼마나 이동시킬지 정합니다.
    public bool useLocalPositionOffset; // 문 외에도 이 월드에서 위치 차이를 둘지 정합니다.
    public Vector3 localPositionOffset; // useLocalPositionOffset이 켜졌을 때 공통 위치에서 더할 위치 차이입니다.
    public bool useLocalScaleOverride; // 이 월드에서 별도 크기를 적용할지 정합니다.
    public Vector3 localScaleOverride = Vector3.one; // useLocalScaleOverride가 켜졌을 때 적용할 월드별 크기입니다.

    public static WorldObjectState3D CreateDefault(Color color)
    {
        return new WorldObjectState3D
        {
            enabledInWorld = true,
            rendererEnabled = true,
            collisionEnabled = true,
            operationEnabled = true,
            useColorOverride = false,
            colorOverride = color,
            doorOpen = false,
            doorOpenLocalOffset = Vector3.zero,
            useLocalPositionOffset = false,
            localPositionOffset = Vector3.zero,
            useLocalScaleOverride = false,
            localScaleOverride = Vector3.one
        };
    }
}
