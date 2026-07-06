using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PlayerHealth3D : MonoBehaviour
{
    [SerializeField] private int maxHealth = 5; // 플레이어 최대 체력 칸 수입니다.
    [SerializeField] private int currentHealth = 5; // 게임 시작 시 플레이어 현재 체력입니다.
    [FormerlySerializedAs("damageCooldown")]
    [SerializeField] private float invincibilityDuration = 0.8f; // 피격 후 추가 피해를 받지 않는 무적 시간입니다.
    [SerializeField] private float invincibleBlinkInterval = 0.1f; // 무적 중 플레이어가 깜빡이는 간격입니다.
    [SerializeField] private Vector2 slotSize = new Vector2(28f, 18f); // 좌측 상단 체력칸 하나의 UI 크기입니다.
    [SerializeField] private float slotSpacing = 6f; // 체력칸 사이의 간격입니다.
    [SerializeField] private Color filledColor = new Color(0.95f, 0.2f, 0.15f, 1f); // 남아 있는 체력칸 색상입니다.
    [SerializeField] private Color emptyColor = new Color(0.12f, 0.12f, 0.12f, 0.75f); // 소모된 체력칸 색상입니다.
    [SerializeField] private Color borderColor = new Color(1f, 1f, 1f, 0.85f); // 체력칸 테두리 색상입니다.

    private readonly List<Image> healthSlots = new List<Image>();
    private Canvas healthCanvas;
    private PlatformerPlayer3D playerMovement;
    private Renderer[] playerRenderers;
    private float invincibilityEndTime;
    private float nextBlinkTime;
    private bool isBlinkVisible = true;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsInvincible => Time.time < invincibilityEndTime;

    private void Awake()
    {
        ApplyDatabaseTuning();
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        playerMovement = GetComponent<PlatformerPlayer3D>();
        CachePlayerRenderers();
        EnsureHealthUI();
        RefreshHealthUI();
    }

    private void Start()
    {
        ApplyDatabaseTuning();
        CachePlayerRenderers();
        EnsureHealthUI();
        RefreshHealthUI();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame))
        {
            Heal(1);
        }

        UpdateInvincibilityBlink();
    }

    private void ApplyDatabaseTuning()
    {
        UnitBalanceDatabase3D database = UnitBalanceDatabase3D.Load();
        if (database == null || database.PlayerHealth == null)
        {
            return;
        }

        PlayerHealthBalance3D tuning = database.PlayerHealth;
        maxHealth = Mathf.Max(1, tuning.maxHealth);
        currentHealth = Mathf.Clamp(tuning.currentHealth, 0, maxHealth);
        invincibilityDuration = tuning.invincibilityDuration;
        invincibleBlinkInterval = tuning.invincibleBlinkInterval;
        slotSize = tuning.slotSize;
        slotSpacing = tuning.slotSpacing;
        filledColor = tuning.filledColor;
        emptyColor = tuning.emptyColor;
        borderColor = tuning.borderColor;
    }

    private void OnDestroy()
    {
        if (healthCanvas != null)
        {
            Destroy(healthCanvas.gameObject);
        }

        SetPlayerVisible(true);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryTakeDamageFrom(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryTakeDamageFrom(collision.collider);
    }

    public bool TakeDamage(int amount)
    {
        return TakeDamage(amount, transform.position);
    }

    public bool TakeDamage(int amount, Vector3 damageSourcePosition)
    {
        if (amount <= 0 || IsInvincible || currentHealth <= 0)
        {
            return false;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        invincibilityEndTime = Time.time + invincibilityDuration;
        nextBlinkTime = Time.time;
        isBlinkVisible = false;
        SetPlayerVisible(false);
        RefreshHealthUI();

        if (playerMovement == null)
        {
            playerMovement = GetComponent<PlatformerPlayer3D>();
        }

        if (playerMovement != null)
        {
            playerMovement.ApplyKnockback(damageSourcePosition);
        }

        return true;
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || currentHealth >= maxHealth)
        {
            return;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        RefreshHealthUI();
    }

    private void EnsureHealthUI()
    {
        if (healthCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Health UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        healthCanvas = canvasObject.GetComponent<Canvas>();
        healthCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        healthCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        GameObject containerObject = new GameObject("Health Slots", typeof(RectTransform));
        containerObject.transform.SetParent(canvasObject.transform, false);

        RectTransform container = containerObject.GetComponent<RectTransform>();
        float totalWidth = maxHealth * slotSize.x + Mathf.Max(0, maxHealth - 1) * slotSpacing;
        container.anchorMin = new Vector2(0f, 1f);
        container.anchorMax = new Vector2(0f, 1f);
        container.pivot = new Vector2(0f, 1f);
        container.anchoredPosition = new Vector2(24f, -24f);
        container.sizeDelta = new Vector2(totalWidth, slotSize.y);

        healthSlots.Clear();
        for (int i = 0; i < maxHealth; i++)
        {
            GameObject slotObject = new GameObject($"Health Slot {i + 1}", typeof(RectTransform), typeof(Image), typeof(Outline));
            slotObject.transform.SetParent(containerObject.transform, false);

            RectTransform slotTransform = slotObject.GetComponent<RectTransform>();
            slotTransform.anchorMin = new Vector2(0f, 0.5f);
            slotTransform.anchorMax = new Vector2(0f, 0.5f);
            slotTransform.pivot = new Vector2(0f, 0.5f);
            slotTransform.anchoredPosition = new Vector2(i * (slotSize.x + slotSpacing), 0f);
            slotTransform.sizeDelta = slotSize;

            Image slotImage = slotObject.GetComponent<Image>();
            slotImage.color = filledColor;

            Outline outline = slotObject.GetComponent<Outline>();
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(2f, -2f);

            healthSlots.Add(slotImage);
        }
    }

    private void RefreshHealthUI()
    {
        if (healthCanvas == null || healthSlots.Count == 0)
        {
            return;
        }

        for (int i = 0; i < healthSlots.Count; i++)
        {
            healthSlots[i].color = i < currentHealth ? filledColor : emptyColor;
        }
    }

    private void CachePlayerRenderers()
    {
        playerRenderers = GetComponentsInChildren<Renderer>();
    }

    private void UpdateInvincibilityBlink()
    {
        if (invincibilityEndTime <= 0f)
        {
            return;
        }

        if (Time.time >= invincibilityEndTime)
        {
            invincibilityEndTime = 0f;
            SetPlayerVisible(true);
            return;
        }

        if (Time.time < nextBlinkTime)
        {
            return;
        }

        isBlinkVisible = !isBlinkVisible;
        SetPlayerVisible(isBlinkVisible);
        nextBlinkTime = Time.time + invincibleBlinkInterval;
    }

    private void SetPlayerVisible(bool visible)
    {
        if (playerRenderers == null || playerRenderers.Length == 0)
        {
            CachePlayerRenderers();
        }

        if (playerRenderers == null)
        {
            return;
        }

        foreach (Renderer playerRenderer in playerRenderers)
        {
            if (playerRenderer != null)
            {
                playerRenderer.enabled = visible;
            }
        }

        isBlinkVisible = visible;
    }

    private void TryTakeDamageFrom(Collider other)
    {
        DamageBlock3D damageBlock = other.GetComponentInParent<DamageBlock3D>();
        if (damageBlock != null)
        {
            damageBlock.ApplyDamageTo(this);
        }
    }
}
