using UnityEngine;

[ExecuteAlways]
public class MonsterAni1Object3D : MonoBehaviour
{
    [SerializeField] private Vector3 tubeSize = new Vector3(2.05f, 3.41f, 0.8f); // 유리관 전체 표시 크기와 트리거 영역 크기입니다. 882:1467 비율에 맞춘 값입니다.
    [SerializeField] private Vector2 innerMonsterSize = new Vector2(0.9f, 2.37f); // 유리관 안에서 떠다니는 몬스터 그림 크기입니다. 368:969 비율에 맞춘 값입니다.
    [SerializeField] private Vector3 innerMonsterOffset = new Vector3(0.13f, -0.08f, -0.03f); // 유리관 중심 기준 내부 몬스터의 기본 위치입니다.
    [SerializeField] private float floatSpeed = 0.45f; // 내부 몬스터가 둥둥 떠다니는 속도입니다. 값을 올리면 더 빠르게 움직입니다.
    [SerializeField] private float floatHeight = 0.055f; // 내부 몬스터가 위아래로 움직이는 높이입니다.
    [SerializeField] private Vector2 bubbleSize = new Vector2(0.55f, 1.45f); // 유리관 안 물방울 묶음 표시 크기입니다.
    [SerializeField] private Vector3 bubbleOffset = new Vector3(0.13f, 0f, -0.03f); // 유리관 중심 기준 물방울 묶음의 기본 위치입니다.
    [SerializeField] private float bubbleFloatSpeed = 0.38f; // 물방울이 위아래로 움직이는 속도입니다. 괴물보다 살짝 느리게 둡니다.
    [SerializeField] private float bubbleFloatHeight = 0.06f; // 물방울이 위아래로 움직이는 높이입니다.
    [SerializeField] private bool useTriggerCollider = true; // 켜두면 플레이어를 막지 않고 감지만 가능한 트리거 오브젝트가 됩니다.
    [SerializeField] private string textureResourcePath = "MonsterAni1/skeleton"; // Resources 폴더 기준으로 불러올 atlas 텍스처 경로입니다.
    [SerializeField] private Vector2 atlasTextureSize = new Vector2(1865f, 1257f); // skeleton.png 원본 픽셀 크기입니다.
    [SerializeField] private Rect tubeAtlasPixelRect = new Rect(2f, 372f, 1467f, 883f); // 유리관 이미지가 들어있는 atlas 영역입니다.
    [SerializeField] private bool tubeRectUsesTopLeftOrigin = true; // tubeAtlasPixelRect의 Y 좌표를 텍스처 위쪽 기준으로 해석할지 정합니다.
    [SerializeField] private bool tubeRegionRotatedClockwise = true; // atlas에서 회전 압축된 유리관 영역을 원래 방향으로 표시할지 정합니다.
    [SerializeField] private Rect innerMonsterAtlasPixelRect = new Rect(2f, 2f, 969f, 368f); // 유리관 안 몬스터 이미지가 들어있는 atlas 영역입니다.
    [SerializeField] private bool innerMonsterRectUsesTopLeftOrigin = true; // innerMonsterAtlasPixelRect의 Y 좌표를 텍스처 위쪽 기준으로 해석할지 정합니다.
    [SerializeField] private bool innerMonsterRegionRotatedClockwise = true; // atlas에서 회전 압축된 내부 몬스터 영역을 원래 방향으로 표시할지 정합니다.
    [SerializeField] private Rect bubbleAtlasPixelRect = new Rect(1471f, 335f, 392f, 920f); // 물방울 이미지가 들어있는 atlas 영역입니다.
    [SerializeField] private bool bubbleRectUsesTopLeftOrigin = true; // bubbleAtlasPixelRect의 Y 좌표를 텍스처 위쪽 기준으로 해석할지 정합니다.
    [SerializeField] private bool bubbleRegionRotatedClockwise; // atlas에서 회전 압축된 물방울 영역을 원래 방향으로 표시할지 정합니다.
    [SerializeField] private Color tubeTintColor = Color.white; // 유리관 이미지에 곱해지는 색상입니다.
    [SerializeField] private Color innerMonsterTintColor = Color.white; // 내부 몬스터 이미지에 곱해지는 색상입니다.
    [SerializeField] private Color bubbleTintColor = Color.white; // 물방울 이미지에 곱해지는 색상입니다.

    private BoxCollider triggerCollider;
    private Rigidbody body;
    private MeshFilter tubeMeshFilter;
    private MeshRenderer tubeRenderer;
    private MeshFilter innerMonsterMeshFilter;
    private MeshRenderer innerMonsterRenderer;
    private MeshFilter bubbleMeshFilter;
    private MeshRenderer bubbleRenderer;
    private Mesh tubeMesh;
    private Mesh innerMonsterMesh;
    private Mesh bubbleMesh;
    private Material tubeMaterial;
    private Material innerMonsterMaterial;
    private Material bubbleMaterial;
    private float monsterFloatTimer;
    private float bubbleFloatTimer;

    private void Awake()
    {
        ConfigureObject();
    }

    private void OnEnable()
    {
        ConfigureObject();
    }

    private void Update()
    {
        ConfigureObject();
        UpdateFloatingMotion();
    }

    private void ConfigureObject()
    {
        ApplyDatabaseTuning();

        tubeSize = MonsterRuntime3D.ClampSize(tubeSize, 0.1f);
        innerMonsterSize = new Vector2(Mathf.Max(0.05f, innerMonsterSize.x), Mathf.Max(0.05f, innerMonsterSize.y));
        bubbleSize = new Vector2(Mathf.Max(0.05f, bubbleSize.x), Mathf.Max(0.05f, bubbleSize.y));
        floatSpeed = Mathf.Max(0f, floatSpeed);
        floatHeight = Mathf.Max(0f, floatHeight);
        bubbleFloatSpeed = Mathf.Max(0f, bubbleFloatSpeed);
        bubbleFloatHeight = Mathf.Max(0f, bubbleFloatHeight);

        ConfigurePhysics();
        ConfigureVisuals();
    }

    private void ApplyDatabaseTuning()
    {
        UnitBalanceDatabase3D database = UnitBalanceDatabase3D.Load();
        if (database == null || database.MonsterAni1 == null)
        {
            return;
        }

        MonsterAni1Balance3D tuning = database.MonsterAni1;
        tubeSize = tuning.tubeSize;
        innerMonsterSize = tuning.innerMonsterSize;
        innerMonsterOffset = tuning.innerMonsterOffset;
        floatSpeed = tuning.floatSpeed;
        floatHeight = tuning.floatHeight;
        bubbleSize = tuning.bubbleSize;
        bubbleOffset = tuning.bubbleOffset;
        bubbleFloatSpeed = tuning.bubbleFloatSpeed;
        bubbleFloatHeight = tuning.bubbleFloatHeight;
        useTriggerCollider = tuning.useTriggerCollider;
        tubeTintColor = tuning.tubeTintColor;
        innerMonsterTintColor = tuning.innerMonsterTintColor;
        bubbleTintColor = tuning.bubbleTintColor;
    }

    private void ConfigurePhysics()
    {
        triggerCollider = GetComponent<BoxCollider>();
        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<BoxCollider>();
        }

        body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;

        triggerCollider.size = tubeSize;
        triggerCollider.center = Vector3.zero;
        triggerCollider.isTrigger = useTriggerCollider;
    }

    private void ConfigureVisuals()
    {
        Texture2D texture = Resources.Load<Texture2D>(textureResourcePath);

        Transform innerMonsterVisual = FindOrCreateVisual("Floating Monster Visual", ref innerMonsterMeshFilter, ref innerMonsterRenderer);
        Transform bubbleVisual = FindOrCreateVisual("Floating Bubbles Visual", ref bubbleMeshFilter, ref bubbleRenderer);
        Transform tubeVisual = FindOrCreateVisual("Glass Tube Visual", ref tubeMeshFilter, ref tubeRenderer);

        if (innerMonsterVisual == null || bubbleVisual == null || tubeVisual == null)
        {
            return;
        }

        if (innerMonsterMesh == null)
        {
            innerMonsterMesh = new Mesh { name = "Generated Monster Ani1 Inner Mesh" };
            innerMonsterMeshFilter.sharedMesh = innerMonsterMesh;
        }

        if (tubeMesh == null)
        {
            tubeMesh = new Mesh { name = "Generated Monster Ani1 Tube Mesh" };
            tubeMeshFilter.sharedMesh = tubeMesh;
        }

        if (bubbleMesh == null)
        {
            bubbleMesh = new Mesh { name = "Generated Monster Ani1 Bubble Mesh" };
            bubbleMeshFilter.sharedMesh = bubbleMesh;
        }

        if (innerMonsterMaterial == null)
        {
            innerMonsterMaterial = MonsterRuntime3D.CreateMaterial("Generated Monster Ani1 Inner Material", innerMonsterTintColor, true);
            innerMonsterMaterial.renderQueue = 3010;
        }

        if (tubeMaterial == null)
        {
            tubeMaterial = MonsterRuntime3D.CreateMaterial("Generated Monster Ani1 Tube Material", tubeTintColor, true);
            tubeMaterial.renderQueue = 3000;
        }

        if (bubbleMaterial == null)
        {
            bubbleMaterial = MonsterRuntime3D.CreateMaterial("Generated Monster Ani1 Bubble Material", bubbleTintColor, true);
            bubbleMaterial.renderQueue = 3011;
        }

        ApplyMaterialTexture(innerMonsterMaterial, texture, innerMonsterTintColor);
        ApplyMaterialTexture(bubbleMaterial, texture, bubbleTintColor);
        ApplyMaterialTexture(tubeMaterial, texture, tubeTintColor);
        innerMonsterRenderer.sharedMaterial = innerMonsterMaterial;
        bubbleRenderer.sharedMaterial = bubbleMaterial;
        tubeRenderer.sharedMaterial = tubeMaterial;
        tubeRenderer.sortingOrder = 0;
        innerMonsterRenderer.sortingOrder = 2;
        bubbleRenderer.sortingOrder = 3;

        RebuildVisualMesh(
            innerMonsterMesh,
            new Vector2(innerMonsterSize.x, innerMonsterSize.y),
            innerMonsterAtlasPixelRect,
            innerMonsterRectUsesTopLeftOrigin,
            innerMonsterRegionRotatedClockwise,
            -0.47f,
            texture
        );
        RebuildVisualMesh(
            bubbleMesh,
            new Vector2(bubbleSize.x, bubbleSize.y),
            bubbleAtlasPixelRect,
            bubbleRectUsesTopLeftOrigin,
            bubbleRegionRotatedClockwise,
            -0.48f,
            texture
        );
        RebuildVisualMesh(
            tubeMesh,
            new Vector2(tubeSize.x, tubeSize.y),
            tubeAtlasPixelRect,
            tubeRectUsesTopLeftOrigin,
            tubeRegionRotatedClockwise,
            -0.44f,
            texture
        );
    }

    private Transform FindOrCreateVisual(string visualName, ref MeshFilter meshFilter, ref MeshRenderer meshRenderer)
    {
        Transform visual = transform.Find(visualName);
        if (visual == null)
        {
            GameObject visualObject = new GameObject(visualName, typeof(MeshFilter), typeof(MeshRenderer));
            visualObject.transform.SetParent(transform, false);
            visual = visualObject.transform;
        }

        meshFilter = visual.GetComponent<MeshFilter>();
        meshRenderer = visual.GetComponent<MeshRenderer>();
        return meshFilter != null && meshRenderer != null ? visual : null;
    }

    private void ApplyMaterialTexture(Material material, Texture2D texture, Color tintColor)
    {
        if (material == null)
        {
            return;
        }

        material.color = tintColor;
        if (texture == null)
        {
            return;
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }
    }

    private void RebuildVisualMesh(Mesh mesh, Vector2 size, Rect atlasPixelRect, bool usesTopLeftOrigin, bool rotatedClockwise, float z, Texture2D texture)
    {
        if (mesh == null)
        {
            return;
        }

        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;
        Vector3[] vertices =
        {
            new Vector3(-halfWidth, -halfHeight, z),
            new Vector3(halfWidth, -halfHeight, z),
            new Vector3(halfWidth, halfHeight, z),
            new Vector3(-halfWidth, halfHeight, z)
        };

        Vector2[] uv = BuildAtlasUv(texture, atlasPixelRect, usesTopLeftOrigin, rotatedClockwise);
        int[] triangles = { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private Vector2[] BuildAtlasUv(Texture2D texture, Rect atlasPixelRect, bool usesTopLeftOrigin, bool rotatedClockwise)
    {
        Vector2 textureSize = texture != null
            ? new Vector2(texture.width, texture.height)
            : new Vector2(Mathf.Max(1f, atlasTextureSize.x), Mathf.Max(1f, atlasTextureSize.y));

        float uMin = atlasPixelRect.x / textureSize.x;
        float uMax = (atlasPixelRect.x + atlasPixelRect.width) / textureSize.x;
        float vMin;
        float vMax;
        if (usesTopLeftOrigin)
        {
            vMin = 1f - (atlasPixelRect.y + atlasPixelRect.height) / textureSize.y;
            vMax = 1f - atlasPixelRect.y / textureSize.y;
        }
        else
        {
            vMin = atlasPixelRect.y / textureSize.y;
            vMax = (atlasPixelRect.y + atlasPixelRect.height) / textureSize.y;
        }

        Vector2 bottomLeft = new Vector2(uMin, vMin);
        Vector2 bottomRight = new Vector2(uMax, vMin);
        Vector2 topRight = new Vector2(uMax, vMax);
        Vector2 topLeft = new Vector2(uMin, vMax);

        return rotatedClockwise
            ? new[] { bottomRight, topRight, topLeft, bottomLeft }
            : new[] { bottomLeft, bottomRight, topRight, topLeft };
    }

    private void UpdateFloatingMotion()
    {
        Transform innerMonsterVisual = innerMonsterMeshFilter != null
            ? innerMonsterMeshFilter.transform
            : transform.Find("Floating Monster Visual");

        Transform tubeVisual = tubeMeshFilter != null
            ? tubeMeshFilter.transform
            : transform.Find("Glass Tube Visual");

        Transform bubbleVisual = bubbleMeshFilter != null
            ? bubbleMeshFilter.transform
            : transform.Find("Floating Bubbles Visual");

        if (tubeVisual != null)
        {
            tubeVisual.localPosition = Vector3.zero;
            tubeVisual.localRotation = Quaternion.identity;
            tubeVisual.localScale = Vector3.one;
        }

        if (innerMonsterVisual == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            monsterFloatTimer += Time.deltaTime * floatSpeed;
            bubbleFloatTimer += Time.deltaTime * bubbleFloatSpeed;
        }

        float monsterBob = Mathf.Sin(monsterFloatTimer * Mathf.PI * 2f) * floatHeight;
        innerMonsterVisual.localPosition = innerMonsterOffset + Vector3.up * monsterBob;
        innerMonsterVisual.localRotation = Quaternion.identity;
        innerMonsterVisual.localScale = Vector3.one;

        if (bubbleVisual == null)
        {
            return;
        }

        float bubbleBob = Mathf.Sin(bubbleFloatTimer * Mathf.PI * 2f + Mathf.PI * 0.12f) * bubbleFloatHeight;
        bubbleVisual.localPosition = bubbleOffset + Vector3.up * bubbleBob;
        bubbleVisual.localRotation = Quaternion.identity;
        bubbleVisual.localScale = Vector3.one;
    }
}
