using System;
using UnityEditor;
using UnityEngine;

public static class CraneAssetRepairUtility
{
    private const string RailPath = "Assets/_Project/Art/Objects/Dynamic/Crane/rail.psd";
    private const string CranePath = "Assets/_Project/Art/Objects/Dynamic/Crane/crane.psd";
    private const string CableMaterialPath = "Assets/_Project/Art/Objects/Dynamic/Crane/CraneCableLine.mat";
    private const string PrefabPath = "Assets/_Project/Prefabs/Objects/Crane/Crane_Set.prefab";
    private const string SessionKey = "CraneAssetRepairUtility.AutoRepairCompleted";
    private const string VisualSessionKey = "CraneAssetRepairUtility.VisualOnly.V4";

    // Intentionally manual-only: automatic editor reload repair overwrote prefab Visual transforms.

    private static void TryApplyVisualOnly()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= ApplyVisualAfterPlayMode;
            EditorApplication.playModeStateChanged += ApplyVisualAfterPlayMode;
            return;
        }

        if (ApplyVisualOnly(false)) SessionState.SetBool(VisualSessionKey, true);
    }

    private static void ApplyVisualAfterPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= ApplyVisualAfterPlayMode;
        EditorApplication.delayCall += TryApplyVisualOnly;
    }

    private static void ScheduleRepairAfterImport()
    {
        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        EditorApplication.delayCall += TryAutoRepair;
    }

    private static void TryAutoRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
            return;
        }

        if (RepairCraneAssets(false))
        {
            SessionState.SetBool(SessionKey, true);
        }
    }

    private static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.delayCall += TryAutoRepair;
    }

    [MenuItem("Tools/_Project/Crane/Repair PSD And Lever References")]
    public static void RepairFromMenu()
    {
        ApplyVisualOnly(true);
    }

    public static bool ApplyVisualOnly(bool logResult)
    {
        AssetDatabase.ImportAsset(RailPath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(CranePath, ImportAssetOptions.ForceSynchronousImport);

        Sprite railSprite = FindSprite(RailPath, "Layer 0");
        Sprite cabinSprite = FindSprite(CranePath, "cable car");
        Sprite trolleySprite = FindSprite(CranePath, "Hoist");
        Sprite ropeSprite = FindSprite(CranePath, "wire");
        Sprite hookSprite = FindSprite(CranePath, "hook");
        if (railSprite == null || cabinSprite == null)
        {
            Debug.LogWarning("[CraneAssetRepairUtility] Existing rail/crane PSD sprites are missing. Visual setup was skipped; Crane functionality is unchanged.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null) return false;
        try
        {
            Transform rail = FindDescendant(root.transform, "Crane_Rail");
            Transform cabin = FindDescendant(root.transform, "Crane");
            CraneRailPath3D railPath = rail != null ? rail.GetComponent<CraneRailPath3D>() : null;
            if (railPath == null || cabin == null || !railPath.IsValid)
            {
                Debug.LogWarning("[CraneAssetRepairUtility] RailPath or Cabin is missing. Visual setup was skipped.");
                return false;
            }

            Transform visualRoot = rail.Find("RailVisualRoot");
            CraneRailVisualBuilder3D builder = rail.GetComponent<CraneRailVisualBuilder3D>();
            if (builder != null) builder.enabled = false;
            if (visualRoot != null) visualRoot.gameObject.SetActive(false);

            Transform staticRail = EnsureChild(root.transform, "Crane_Rail_Static", Vector3.zero);
            Vector3 railCenter = (railPath.GetRailPointA() + railPath.GetRailPointB()) * 0.5f;
            staticRail.position = railCenter + new Vector3(0f, 0f, 0.1f);
            ConfigureVisualSprite(staticRail, railSprite, 5);

            Transform cabinVisual = EnsureChild(cabin, "CabinVisual", Vector3.zero);
            ConfigureVisualSprite(cabinVisual, cabinSprite, 10);
            cabinVisual.localPosition = Vector3.zero;

            Transform trolleyVisual = EnsureChild(cabin, "TrolleyVisual", new Vector3(0f, 3f, 0.05f));
            ConfigureVisualSprite(trolleyVisual, trolleySprite, 12);
            trolleyVisual.localPosition = new Vector3(0f, 3f, 0.05f);
            Transform ropeVisual = EnsureChild(cabin, "RopeOrCableVisual", new Vector3(0f, 1.8f, 0.04f));
            ConfigureVisualSprite(ropeVisual, ropeSprite, 11);
            ropeVisual.localPosition = new Vector3(0f, 1.8f, 0.04f);
            Transform hookVisual = EnsureChild(cabin, "HookVisual", new Vector3(0f, 0.8f, 0.03f));
            ConfigureVisualSprite(hookVisual, hookSprite, 11);
            hookVisual.localPosition = new Vector3(0f, 0.8f, 0.03f);

            CraneMovingVisualAligner3D aligner = cabin.GetComponent<CraneMovingVisualAligner3D>();
            if (aligner != null) aligner.enabled = false;

            LineRenderer[] lines = root.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lines.Length; i++) lines[i].enabled = false;

            Transform obsoleteCable = FindDescendant(root.transform, "Crane_CableVisual");
            if (obsoleteCable != null) obsoleteCable.gameObject.SetActive(false);
            Transform oldCableVisual = cabin.Find("CableVisual");
            if (oldCableVisual != null) oldCableVisual.gameObject.SetActive(false);
            Transform oldConnector = cabin.Find("UpperConnectorVisual");
            if (oldConnector != null) oldConnector.gameObject.SetActive(false);

            SetDebugVisualsActive(root.transform, false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            if (logResult) Debug.Log("[CraneAssetRepairUtility] Static rail and moving trolley/rope/hook/cabin visuals applied. Crane functionality was not modified.");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureVisualSprite(Transform target, Sprite sprite, int sortingOrder)
    {
        if (target == null) return;
        target.gameObject.SetActive(true);
        target.localScale = Vector3.one;
        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = target.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = sortingOrder;
        renderer.enabled = true;
        if (sprite == null) Debug.LogWarning($"[CraneAssetRepairUtility] Sprite for {target.name} is missing. Assign an existing project Sprite manually.");
    }

    private static void SetDebugVisualsActive(Transform root, bool active)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name.Contains("Debug_Block")) all[i].gameObject.SetActive(active);
        }
    }

    private static bool RepairCraneAssets(bool logResult)
    {
        AssetDatabase.ImportAsset(RailPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(CranePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        Sprite railSprite = FindSprite(RailPath, "rail_start");
        Sprite craneSprite = FindSprite(CranePath, "cable car");
        Sprite wireSprite = FindSprite(CranePath, "wire");
        if (railSprite == null || craneSprite == null)
        {
            Debug.LogWarning(
                "[CraneAssetRepairUtility] PSD Sprites were not found. Confirm that 2D PSD Importer is installed and both PSD assets are imported.");
            return false;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogWarning($"[CraneAssetRepairUtility] Crane prefab was not found: {PrefabPath}");
            return false;
        }

        try
        {
            Transform railVisual = FindDescendant(prefabRoot.transform, "Crane_Rail/Rail_Visual");
            Transform crane = FindDescendant(prefabRoot.transform, "Crane");
            Transform craneVisual = crane != null ? crane.Find("Visual") : null;
            Transform wireVisual = crane != null ? crane.Find("WireVisual") : null;
            Transform hangingWireVisual = crane != null ? EnsureChild(crane, "HangingWireVisual", new Vector3(0f, -1.2f, 0.02f)) : null;
            Transform carryPlatform = crane != null ? EnsureChild(crane, "CarryPlatform", new Vector3(0f, -2f, 0f)) : null;
            Transform craneCablePoint = crane != null ? EnsureChild(crane, "CablePoint", new Vector3(0f, 0.6f, 0f)) : null;
            Transform lever = FindDescendant(prefabRoot.transform, "Lever");
            Transform leverCablePoint = lever != null ? EnsureChild(lever, "CablePoint", new Vector3(0f, 0.65f, 0f)) : null;
            Transform cableVisual = EnsureChild(prefabRoot.transform, "CableVisual", Vector3.zero);

            bool setupValid = ConfigureRenderer(railVisual, railSprite, 5)
                & ConfigureRenderer(craneVisual, craneSprite, 10);

            if (wireSprite != null)
            {
                ConfigureRenderer(wireVisual, wireSprite, 9);
                ConfigureRenderer(hangingWireVisual, wireSprite, 9);
            }
            else
            {
                ConfigureLineRenderer(hangingWireVisual, EnsureCableMaterial(), 0.04f, 9, false);
            }

            ConfigureCarryPlatform(carryPlatform);
            ConfigureCableVisual(cableVisual, leverCablePoint, craneCablePoint);
            FitRailVisualToPoints(railVisual);

            CraneObject craneObject = crane != null ? crane.GetComponent<CraneObject>() : null;
            CraneLeverSwitch leverSwitch = lever != null ? lever.GetComponent<CraneLeverSwitch>() : null;
            if (leverSwitch == null || craneObject == null)
            {
                Debug.LogWarning("[CraneAssetRepairUtility] CraneObject or Lever/CraneLeverSwitch is missing from Crane_Set.");
                setupValid = false;
            }
            else
            {
                lever.gameObject.SetActive(true);
                Collider leverTrigger = lever.GetComponent<Collider>();
                if (leverTrigger != null)
                {
                    leverTrigger.enabled = true;
                    leverTrigger.isTrigger = true;
                }

                leverSwitch.SetTargetCrane(craneObject);
                EditorUtility.SetDirty(leverSwitch);
            }

            if (!setupValid)
            {
                return false;
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            AssetDatabase.SaveAssets();

            if (logResult)
            {
                Debug.Log("[CraneAssetRepairUtility] Crane PSD Sprites and Lever target reference repaired.");
            }

            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static bool ConfigureRenderer(Transform target, Sprite sprite, int sortingOrder)
    {
        if (target == null || sprite == null)
        {
            Debug.LogWarning($"[CraneAssetRepairUtility] Visual target or Sprite is missing. Target={(target != null ? target.name : "None")}");
            return false;
        }

        target.gameObject.SetActive(true);
        if (target.localScale == Vector3.zero)
        {
            target.localScale = Vector3.one;
        }

        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = target.gameObject.AddComponent<SpriteRenderer>();
        }

        renderer.enabled = true;
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        EditorUtility.SetDirty(renderer);
        return true;
    }

    private static Transform EnsureChild(Transform parent, string childName, Vector3 localPosition)
    {
        if (parent == null)
        {
            return null;
        }

        Transform child = parent.Find(childName);
        if (child != null)
        {
            child.gameObject.SetActive(true);
            return child;
        }

        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(parent, false);
        childObject.transform.localPosition = localPosition;
        childObject.transform.localRotation = Quaternion.identity;
        childObject.transform.localScale = Vector3.one;
        return childObject.transform;
    }

    private static void ConfigureCarryPlatform(Transform carryPlatform)
    {
        if (carryPlatform == null)
        {
            return;
        }

        carryPlatform.localPosition = new Vector3(0f, -2f, 0f);
        carryPlatform.localRotation = Quaternion.identity;
        carryPlatform.localScale = new Vector3(2f, 0.25f, 0.6f);

        BoxCollider collider = carryPlatform.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = carryPlatform.gameObject.AddComponent<BoxCollider>();
        }

        collider.isTrigger = false;
        collider.enabled = true;
        collider.size = Vector3.one;
        collider.center = Vector3.zero;
        EditorUtility.SetDirty(collider);
    }

    private static void ConfigureCableVisual(Transform cableVisual, Transform startPoint, Transform endPoint)
    {
        if (cableVisual == null)
        {
            return;
        }

        LineRenderer lineRenderer = ConfigureLineRenderer(cableVisual, EnsureCableMaterial(), 0.05f, 10, true);
        CraneCableVisual3D cable = cableVisual.GetComponent<CraneCableVisual3D>();
        if (cable == null)
        {
            cable = cableVisual.gameObject.AddComponent<CraneCableVisual3D>();
        }

        cable.SetPoints(startPoint, endPoint);
        if (lineRenderer != null && startPoint != null && endPoint != null)
        {
            lineRenderer.SetPosition(0, startPoint.position);
            lineRenderer.SetPosition(1, endPoint.position);
        }

        EditorUtility.SetDirty(cable);
    }

    private static LineRenderer ConfigureLineRenderer(Transform target, Material material, float width, int sortingOrder, bool worldSpace)
    {
        if (target == null)
        {
            return null;
        }

        target.gameObject.SetActive(true);
        LineRenderer lineRenderer = target.GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = target.gameObject.AddComponent<LineRenderer>();
        }

        lineRenderer.enabled = true;
        lineRenderer.useWorldSpace = worldSpace;
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.sortingOrder = sortingOrder;
        lineRenderer.startColor = Color.white;
        lineRenderer.endColor = Color.white;
        if (material != null)
        {
            lineRenderer.sharedMaterial = material;
        }

        if (!worldSpace)
        {
            lineRenderer.SetPosition(0, new Vector3(0f, 0.7f, 0f));
            lineRenderer.SetPosition(1, new Vector3(0f, -0.9f, 0f));
        }

        EditorUtility.SetDirty(lineRenderer);
        return lineRenderer;
    }

    private static Material EnsureCableMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(CableMaterialPath);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            return null;
        }

        material = new Material(shader)
        {
            name = "CraneCableLine"
        };
        material.color = Color.white;
        AssetDatabase.CreateAsset(material, CableMaterialPath);
        return material;
    }

    private static void FitRailVisualToPoints(Transform railVisual)
    {
        if (railVisual == null)
        {
            return;
        }

        CraneRailPath3D railPath = railVisual.GetComponentInParent<CraneRailPath3D>();
        SpriteRenderer renderer = railVisual.GetComponent<SpriteRenderer>();
        if (railPath == null || renderer == null || renderer.sprite == null || !railPath.IsValid)
        {
            return;
        }

        Vector3 localA = railVisual.parent.InverseTransformPoint(railPath.PointA);
        Vector3 localB = railVisual.parent.InverseTransformPoint(railPath.PointB);
        float railLength = Vector3.Distance(localA, localB);
        float spriteWidth = Mathf.Max(0.01f, renderer.sprite.bounds.size.x);

        railVisual.localPosition = (localA + localB) * 0.5f + new Vector3(0f, 1.5f, 0.1f);
        railVisual.localScale = new Vector3(railLength / spriteWidth, railVisual.localScale.y, 1f);
        EditorUtility.SetDirty(railVisual);
    }

    private static Sprite FindSprite(string assetPath, string preferredName)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        Sprite largest = null;
        float largestArea = -1f;
        for (int i = 0; i < assets.Length; i++)
        {
            if (!(assets[i] is Sprite sprite))
            {
                continue;
            }

            if (string.Equals(sprite.name, preferredName, StringComparison.OrdinalIgnoreCase))
            {
                return sprite;
            }

            float area = sprite.rect.width * sprite.rect.height;
            if (area > largestArea)
            {
                largest = sprite;
                largestArea = area;
            }
        }

        return largest;
    }

    private static Transform FindDescendant(Transform root, string relativePath)
    {
        return root != null ? root.Find(relativePath) : null;
    }
}
