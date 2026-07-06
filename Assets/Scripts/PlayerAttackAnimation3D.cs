using System.Collections.Generic;
using UnityEngine;

public class PlayerAttackAnimation3D : MonoBehaviour
{
    [SerializeField] private string resourceFolder = "PlayerAttack"; // 공격 이미지가 들어 있는 Resources 하위 폴더 이름입니다.
    [SerializeField] private string[] frameNames = { "attack_01", "attack_02", "attack_03", "attack_04" }; // 공격 모션을 재생할 이미지 순서입니다.
    [SerializeField] private float frameDuration = 0.045f; // 공격 모션 한 장면이 유지되는 시간입니다.
    [SerializeField] private float pixelsPerUnit = 340f; // 공격 스프라이트의 게임 안 크기를 조절하는 픽셀 비율입니다.
    [SerializeField] private Vector3 localOffset = new Vector3(0.2f, 0.04f, -0.45f); // 공격 모션 이미지가 플레이어 기준으로 표시될 위치입니다.
    [SerializeField] private int sortingOrder = 30; // 공격 모션이 다른 스프라이트보다 앞에 보이는 순서입니다.

    private readonly List<Sprite> frames = new List<Sprite>();
    private SpriteRenderer spriteRenderer;
    private PlatformerPlayer3D movement;
    private PlayerHealth3D health;
    private float timer;
    private int frameIndex;
    private bool isPlaying;
    private float facingSign = 1f;

    private void Awake()
    {
        movement = GetComponent<PlatformerPlayer3D>();
        health = GetComponent<PlayerHealth3D>();
        ApplyDatabaseTuning();
        EnsureRenderer();
        LoadFrames();
        ShowIdleFrame();
    }

    private void OnEnable()
    {
        movement = GetComponent<PlatformerPlayer3D>();
        health = GetComponent<PlayerHealth3D>();
        ApplyDatabaseTuning();
        EnsureRenderer();
        LoadFrames();
        ShowIdleFrame();
    }

    private void Update()
    {
        if (frames.Count == 0)
        {
            DisableRenderer();
            return;
        }

        if (!Application.isPlaying)
        {
            ShowIdleFrame();
            return;
        }

        if (!isPlaying)
        {
            UpdateIdleFacing();
            ShowIdleFrame();
            return;
        }

        timer += Time.deltaTime;
        if (timer < frameDuration)
        {
            return;
        }

        timer -= frameDuration;
        frameIndex++;

        if (frameIndex >= frames.Count)
        {
            ShowIdleFrame();
            return;
        }

        spriteRenderer.sprite = frames[frameIndex];
    }

    public void Play(Vector3 attackDirection, float currentFacingDirection)
    {
        ApplyDatabaseTuning();
        EnsureRenderer();
        LoadFrames();

        if (frames.Count == 0)
        {
            return;
        }

        facingSign = Mathf.Abs(attackDirection.x) > 0.01f
            ? Mathf.Sign(attackDirection.x)
            : Mathf.Sign(currentFacingDirection);

        if (Mathf.Abs(facingSign) < 0.01f)
        {
            facingSign = 1f;
        }

        frameIndex = 0;
        timer = 0f;
        isPlaying = true;

        spriteRenderer.flipX = facingSign < 0f;
        spriteRenderer.sprite = frames[frameIndex];
        if (CanControlVisibility())
        {
            spriteRenderer.enabled = true;
        }

        ApplySpriteTransform();
    }

    private void ShowIdleFrame()
    {
        isPlaying = false;

        if (spriteRenderer == null || frames.Count == 0)
        {
            DisableRenderer();
            return;
        }

        spriteRenderer.flipX = facingSign < 0f;
        spriteRenderer.sprite = frames[0];
        ApplySpriteTransform();

        if (CanControlVisibility())
        {
            spriteRenderer.enabled = true;
        }
    }

    private void ApplySpriteTransform()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Vector3 parentScale = transform.lossyScale;
        float scaleX = Mathf.Max(0.001f, Mathf.Abs(parentScale.x));
        float scaleY = Mathf.Max(0.001f, Mathf.Abs(parentScale.y));
        float scaleZ = Mathf.Max(0.001f, Mathf.Abs(parentScale.z));

        spriteRenderer.transform.localPosition = new Vector3(localOffset.x * facingSign / scaleX, localOffset.y / scaleY, localOffset.z / scaleZ);
        spriteRenderer.transform.localScale = new Vector3(1f / scaleX, 1f / scaleY, 1f / scaleZ);
    }

    private void DisableRenderer()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.enabled = false;
        spriteRenderer.sprite = null;
    }

    private void UpdateIdleFacing()
    {
        if (movement == null)
        {
            movement = GetComponent<PlatformerPlayer3D>();
        }

        if (movement == null || Mathf.Abs(movement.FacingDirection) < 0.01f)
        {
            return;
        }

        facingSign = Mathf.Sign(movement.FacingDirection);
    }

    private bool CanControlVisibility()
    {
        if (health == null)
        {
            health = GetComponent<PlayerHealth3D>();
        }

        return health == null || !health.IsInvincible;
    }

    private void ApplyDatabaseTuning()
    {
        UnitBalanceDatabase3D database = UnitBalanceDatabase3D.Load();
        if (database == null || database.PlayerAttackAnimation == null)
        {
            return;
        }

        PlayerAttackAnimationBalance3D tuning = database.PlayerAttackAnimation;
        string nextResourceFolder = string.IsNullOrWhiteSpace(tuning.resourceFolder) ? resourceFolder : tuning.resourceFolder;
        string[] nextFrameNames = tuning.frameNames == null || tuning.frameNames.Length == 0 ? frameNames : tuning.frameNames;
        bool shouldReloadFrames = resourceFolder != nextResourceFolder || !FrameNamesMatch(frameNames, nextFrameNames);
        resourceFolder = nextResourceFolder;
        frameNames = nextFrameNames;
        frameDuration = tuning.frameDuration;
        pixelsPerUnit = tuning.pixelsPerUnit;
        localOffset = tuning.localOffset;
        sortingOrder = tuning.sortingOrder;

        if (shouldReloadFrames)
        {
            frames.Clear();
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = sortingOrder;
        }
    }

    private static bool FrameNamesMatch(string[] left, string[] right)
    {
        if (left == right)
        {
            return true;
        }

        if (left == null || right == null || left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    private void EnsureRenderer()
    {
        if (spriteRenderer != null)
        {
            return;
        }

        Transform existing = transform.Find("Player Attack Animation");
        GameObject animationObject = existing != null
            ? existing.gameObject
            : new GameObject("Player Attack Animation");

        animationObject.transform.SetParent(transform, false);
        animationObject.transform.localPosition = localOffset;
        animationObject.transform.localRotation = Quaternion.identity;

        spriteRenderer = animationObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = animationObject.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sortingOrder = sortingOrder;
        spriteRenderer.enabled = false;
        ApplySpriteTransform();
    }

    private void LoadFrames()
    {
        if (frames.Count > 0)
        {
            return;
        }

        foreach (string frameName in frameNames)
        {
            if (string.IsNullOrWhiteSpace(frameName))
            {
                continue;
            }

            Texture2D texture = Resources.Load<Texture2D>($"{resourceFolder}/{frameName}");
            if (texture == null)
            {
                continue;
            }

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                Mathf.Max(1f, pixelsPerUnit)
            );
            sprite.name = frameName;
            frames.Add(sprite);
        }
    }
}
