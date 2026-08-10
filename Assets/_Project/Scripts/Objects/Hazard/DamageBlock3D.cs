using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("_Project/Objects/Hazards/Spike Trap")]
[DisallowMultipleComponent]
[ExecuteAlways]
public class DamageBlock3D : MonoBehaviour
{
    public enum SpikeTrapState
    {
        ACTIVE
    }

    [Header("Spike Trap")]
    [SerializeField] private Vector3 blockSize = new Vector3(0.85f, 0.85f, 1f); // Hazard block visual and collider size.
    [SerializeField] private Color blockColor = new Color(0.95f, 0.15f, 0.1f, 1f); // Hazard block display color.
    [Min(0)] [SerializeField] private int damage = 1;

    [Header("Collision")]
    [Tooltip("Optional solid collider used as the physical hazard surface.")]
    [SerializeField] private BoxCollider solidCollider;
    [Tooltip("Trigger collider that applies contact damage.")]
    [SerializeField] private BoxCollider damageTrigger;

    [Header("Runtime (Read Only)")]
    [SerializeField] private SpikeTrapState currentState = SpikeTrapState.ACTIVE;
    [SerializeField] private int activeTargetCount;

    [Header("Debug")]
    [SerializeField] private bool debugMode;

    private static Mesh cubeMesh;
    private Material visualMaterial;
    private readonly Dictionary<int, int> contactRootByCollider = new Dictionary<int, int>(8);
    private readonly Dictionary<int, int> contactCountByRoot = new Dictionary<int, int>(4);
    private readonly HashSet<int> damagedRoots = new HashSet<int>();

    private void Awake()
    {
        ConfigureBlock();
    }

    private void OnEnable()
    {
        ClearContacts();
        ConfigureBlock();
    }

    private void OnDisable()
    {
        ClearContacts();
    }

    private void OnValidate()
    {
        damage = Mathf.Max(0, damage);
        blockSize.x = Mathf.Max(0.1f, blockSize.x);
        blockSize.y = Mathf.Max(0.1f, blockSize.y);
        blockSize.z = Mathf.Max(0.1f, blockSize.z);
        ResolveColliders();
        ConfigureCollider(solidCollider, false);
        ConfigureCollider(damageTrigger, true);
        transform.localScale = blockSize;
        TwoPointFiveDUtility3D.ClampTransformToPlane(transform);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!Application.isPlaying || other == null)
        {
            return;
        }

        RegisterContact(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!Application.isPlaying || other == null)
        {
            return;
        }

        UnregisterContact(other);
    }

    private void ConfigureBlock()
    {
        blockSize.x = Mathf.Max(0.1f, blockSize.x);
        blockSize.y = Mathf.Max(0.1f, blockSize.y);
        blockSize.z = Mathf.Max(0.1f, blockSize.z);

        ResolveColliders();
        if (solidCollider == null)
        {
            solidCollider = gameObject.AddComponent<BoxCollider>();
            solidCollider.isTrigger = false;
        }

        if (damageTrigger == null)
        {
            damageTrigger = gameObject.AddComponent<BoxCollider>();
            damageTrigger.isTrigger = true;
        }

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        ConfigureCollider(solidCollider, false);
        ConfigureCollider(damageTrigger, true);

        meshFilter.sharedMesh = GetCubeMesh();

        if (visualMaterial == null)
        {
            visualMaterial = CreateMaterial("Generated Hazard Block Material", blockColor);
        }

        visualMaterial.color = blockColor;
        meshRenderer.sharedMaterial = visualMaterial;

        transform.localScale = blockSize;
        TwoPointFiveDUtility3D.ClampTransformToPlane(transform);
    }

    public void ApplyDamageTo(PlayerHealth3D health)
    {
        // Combat health damage has been removed. This method remains for old scene references.
    }

    private void RegisterContact(Collider other)
    {
        if (!TryResolveDamageTarget(other, out IDamageable damageable, out int targetRootId, out string targetName))
        {
            return;
        }

        int colliderId = other.GetInstanceID();
        if (contactRootByCollider.ContainsKey(colliderId))
        {
            return;
        }

        contactRootByCollider.Add(colliderId, targetRootId);
        contactCountByRoot.TryGetValue(targetRootId, out int contactCount);
        contactCountByRoot[targetRootId] = contactCount + 1;
        activeTargetCount = contactCountByRoot.Count;

        if (damage <= 0 || !damagedRoots.Add(targetRootId) || !damageable.CanTakeDamage)
        {
            return;
        }

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        Vector3 hitDirection = (other.bounds.center - transform.position).normalized;
        DamageInfo damageInfo = new DamageInfo(
            damage,
            gameObject,
            gameObject,
            hitPoint,
            hitDirection,
            DamageType.Trap,
            HitSourceType.SpikeTrap);
        damageable.TakeDamage(damageInfo);
        Log($"Damaged {targetName}. Damage={damage}, Source={HitSourceType.SpikeTrap}");
    }

    private void UnregisterContact(Collider other)
    {
        int colliderId = other.GetInstanceID();
        if (!contactRootByCollider.TryGetValue(colliderId, out int targetRootId))
        {
            return;
        }

        contactRootByCollider.Remove(colliderId);
        if (!contactCountByRoot.TryGetValue(targetRootId, out int contactCount) || contactCount <= 1)
        {
            contactCountByRoot.Remove(targetRootId);
            damagedRoots.Remove(targetRootId);
        }
        else
        {
            contactCountByRoot[targetRootId] = contactCount - 1;
        }

        activeTargetCount = contactCountByRoot.Count;
    }

    private static bool TryResolveDamageTarget(
        Collider other,
        out IDamageable damageable,
        out int targetRootId,
        out string targetName)
    {
        PlayerDamageReceiver player = other.GetComponentInParent<PlayerDamageReceiver>();
        if (player != null)
        {
            damageable = player;
            targetRootId = player.gameObject.GetInstanceID();
            targetName = player.name;
            return true;
        }

        MonsterHealth monster = other.GetComponentInParent<MonsterHealth>();
        if (monster != null)
        {
            damageable = monster;
            targetRootId = monster.gameObject.GetInstanceID();
            targetName = monster.name;
            return true;
        }

        damageable = null;
        targetRootId = 0;
        targetName = string.Empty;
        return false;
    }

    private void ResolveColliders()
    {
        BoxCollider[] colliders = GetComponents<BoxCollider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            BoxCollider candidate = colliders[i];
            if (candidate == null)
            {
                continue;
            }

            if (candidate.isTrigger)
            {
                if (damageTrigger == null) damageTrigger = candidate;
            }
            else if (solidCollider == null)
            {
                solidCollider = candidate;
            }
        }
    }

    private static void ConfigureCollider(BoxCollider target, bool isTrigger)
    {
        if (target == null)
        {
            return;
        }

        target.size = Vector3.one;
        target.center = Vector3.zero;
        target.isTrigger = isTrigger;
        target.enabled = true;
    }

    private void ClearContacts()
    {
        contactRootByCollider.Clear();
        contactCountByRoot.Clear();
        damagedRoots.Clear();
        activeTargetCount = 0;
        currentState = SpikeTrapState.ACTIVE;
    }

    private void Log(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[Spike_Trap] {message}", this);
        }
    }

    private static Mesh GetCubeMesh()
    {
        if (cubeMesh != null)
        {
            return cubeMesh;
        }

        cubeMesh = new Mesh { name = "Generated Box Mesh" };
        cubeMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
        };
        cubeMesh.triangles = new[]
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4,
            2, 3, 7, 2, 7, 6,
            1, 2, 6, 1, 6, 5,
            3, 0, 4, 3, 4, 7
        };
        cubeMesh.RecalculateNormals();
        cubeMesh.RecalculateBounds();
        return cubeMesh;
    }

    private static Material CreateMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        return new Material(shader)
        {
            name = materialName,
            color = color,
            hideFlags = HideFlags.HideAndDontSave
        };
    }
}
