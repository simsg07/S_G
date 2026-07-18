using UnityEngine;

public static class MonsterRuntime3D
{
    private static Mesh cubeMesh;
    private static readonly RaycastHit[] sightHits = new RaycastHit[24];

    public static void ConfigureKinematicBox(
        GameObject owner,
        Vector3 size,
        Color color,
        string materialName,
        ref BoxCollider boxCollider,
        ref MeshFilter meshFilter,
        ref MeshRenderer meshRenderer,
        ref Rigidbody body,
        ref Material material)
    {
        boxCollider = owner.GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = owner.AddComponent<BoxCollider>();
        }

        meshFilter = owner.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = owner.AddComponent<MeshFilter>();
        }

        meshRenderer = owner.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = owner.AddComponent<MeshRenderer>();
        }

        body = owner.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = owner.AddComponent<Rigidbody>();
        }

        body.isKinematic = true;
        body.useGravity = false;
        TwoPointFiveDUtility3D.ConfigureRigidbodyForSideView(body);
        TwoPointFiveDUtility3D.ClampTransformToPlane(owner.transform);

        boxCollider.size = Vector3.one;
        boxCollider.center = Vector3.zero;
        boxCollider.isTrigger = false;
        meshFilter.sharedMesh = GetCubeMesh();

        if (material == null)
        {
            material = CreateMaterial(materialName, color, false);
        }

        material.color = color;
        meshRenderer.sharedMaterial = material;
        owner.transform.localScale = ClampSize(size, 0.05f);
    }

    public static Transform FindOrCreateBoxVisual(Transform parent, string visualName, Color color, ref Material material)
    {
        Transform existing = parent.Find(visualName);
        if (existing != null)
        {
            ConfigureBoxVisual(existing.gameObject, visualName, color, ref material);
            return existing;
        }

        GameObject visualObject = new GameObject(visualName, typeof(MeshFilter), typeof(MeshRenderer));
        visualObject.transform.SetParent(parent, false);
        ConfigureBoxVisual(visualObject, visualName, color, ref material);
        return visualObject.transform;
    }

    public static void ApplyWorldBoxVisual(Transform parent, Transform visual, Vector3 center, Vector3 size, float depth)
    {
        if (visual == null)
        {
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        visual.localPosition = parent.InverseTransformPoint(center);
        visual.localRotation = Quaternion.identity;
        visual.localScale = new Vector3(
            size.x / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
            size.y / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)),
            depth / Mathf.Max(0.001f, Mathf.Abs(parentScale.z))
        );
    }

    public static void SetVisualVisible(Transform visual, bool visible)
    {
        if (visual == null)
        {
            return;
        }

        MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.enabled = visible;
        }
    }

    public static bool PlayerOverlapsBox(Collider[] hits, Vector3 center, Vector3 size, PlatformerPlayer3D player)
    {
        if (player == null)
        {
            return false;
        }

        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            ClampSize(size, 0.01f) * 0.5f,
            hits,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hits[i];
            if (hit != null && hit.GetComponentInParent<PlatformerPlayer3D>() == player)
            {
                return true;
            }
        }

        return false;
    }

    public static bool PlayerOverlapsVisibleBox(Collider[] hits, Transform observer, Vector3 center, Vector3 size, PlatformerPlayer3D player)
    {
        return PlayerOverlapsBox(hits, center, size, player) && HasClearSightToPlayer(observer, center, player);
    }

    public static bool HasClearSightToPlayer(Transform observer, Vector3 fallbackOrigin, PlatformerPlayer3D player)
    {
        if (player == null)
        {
            return false;
        }

        Vector3 origin = observer != null ? observer.position : fallbackOrigin;
        Vector3 target = GetPlayerSightPoint(player);
        Vector3 direction = target - origin;
        float distance = direction.magnitude;

        if (distance <= 0.01f)
        {
            return true;
        }

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction / distance,
            sightHits,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = sightHits[i].collider;
            if (hit == null)
            {
                continue;
            }

            if ((observer != null && hit.transform.IsChildOf(observer)) || hit.GetComponentInParent<PlatformerPlayer3D>() == player)
            {
                continue;
            }

            if (hit.GetComponentInParent<PlatformSurface3D>() != null)
            {
                return false;
            }
        }

        return true;
    }

    public static bool DamagePlayerInBox(Collider[] hits, Vector3 center, Vector3 size, PlatformerPlayer3D player, int damage, Vector3 sourcePosition)
    {
        // Player combat effects have been removed.
        // Keep this legacy entry point so existing monster prototypes compile without affecting the player.
        return false;
    }

    public static bool PlatformOverlapsBox(Collider[] hits, Transform owner, Vector3 center, Vector3 size)
    {
        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            ClampSize(size, 0.01f) * 0.5f,
            hits,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.transform.IsChildOf(owner))
            {
                continue;
            }

            if (hit.GetComponentInParent<PlatformSurface3D>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3 GetPlayerSightPoint(PlatformerPlayer3D player)
    {
        Collider playerCollider = player.GetComponentInChildren<Collider>();
        return playerCollider != null ? playerCollider.bounds.center : player.transform.position;
    }

    public static Vector3 ClampSize(Vector3 size, float minimum)
    {
        return new Vector3(
            Mathf.Max(minimum, size.x),
            Mathf.Max(minimum, size.y),
            Mathf.Max(minimum, size.z)
        );
    }

    private static void ConfigureBoxVisual(GameObject visualObject, string visualName, Color color, ref Material material)
    {
        visualObject.hideFlags = HideFlags.HideAndDontSave;

        MeshFilter meshFilter = visualObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = visualObject.AddComponent<MeshFilter>();
        }

        MeshRenderer meshRenderer = visualObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = visualObject.AddComponent<MeshRenderer>();
        }

        meshFilter.sharedMesh = GetCubeMesh();
        if (material == null)
        {
            material = CreateMaterial($"Generated {visualName} Material", color, true);
        }

        material.color = color;
        meshRenderer.sharedMaterial = material;
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

    public static Material CreateMaterial(string materialName, Color color, bool transparent)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            name = materialName,
            color = color,
            hideFlags = HideFlags.HideAndDontSave
        };

        if (transparent)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        return material;
    }
}
