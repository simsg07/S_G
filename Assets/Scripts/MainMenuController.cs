using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene"; // 게임 시작 버튼이 불러올 플레이 씬 이름입니다.
    [SerializeField] private Color backgroundColor = new Color(0.05f, 0.07f, 0.1f, 1f); // 메인 메뉴 배경 색상입니다.
    [SerializeField] private Color normalColor = new Color(0.16f, 0.18f, 0.22f, 1f); // 선택되지 않은 메뉴 버튼 색상입니다.
    [SerializeField] private Color selectedColor = new Color(0.1f, 0.38f, 0.75f, 1f); // 현재 선택된 메뉴 버튼 색상입니다.
    [SerializeField] private Color textColor = new Color(0.94f, 0.96f, 1f, 1f); // 메뉴 제목과 버튼 글자 색상입니다.
    [SerializeField] private Font menuFont; // 메뉴에서 사용할 폰트입니다. 비워두면 Unity 기본 폰트를 사용합니다.

    private readonly List<MenuEntry> entries = new List<MenuEntry>();

    private Canvas canvas;
    private RectTransform menuRoot;
    private int selectedIndex;
    private MenuMode mode = MenuMode.Main;
    private int volumePercent = 100;
    private bool fullscreen;

    private enum MenuMode
    {
        Main,
        Options
    }

    private void Awake()
    {
        fullscreen = Screen.fullScreen;
        EnsureEventSystem();
        BuildMenuUI();
        ShowMainMenu();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (entries.Count == 0)
        {
            return;
        }

        if (keyboard != null && (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame))
        {
            SelectIndex(selectedIndex - 1);
        }

        if (keyboard != null && (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame))
        {
            SelectIndex(selectedIndex + 1);
        }

        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            ExecuteSelected();
        }

        HandleMouseInput();
    }

    private void EnsureEventSystem()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            InputSystemUIInputModule inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            inputModule.enabled = true;
        }
    }

    private void BuildMenuUI()
    {
        GameObject canvasObject = new GameObject("Main Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        RectTransform canvasTransform = canvasObject.GetComponent<RectTransform>();

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvasTransform, false);
        RectTransform backgroundTransform = background.GetComponent<RectTransform>();
        backgroundTransform.anchorMin = Vector2.zero;
        backgroundTransform.anchorMax = Vector2.one;
        backgroundTransform.offsetMin = Vector2.zero;
        backgroundTransform.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = backgroundColor;

        GameObject panel = new GameObject("Menu Root", typeof(RectTransform));
        panel.transform.SetParent(canvasTransform, false);
        menuRoot = panel.GetComponent<RectTransform>();
        menuRoot.anchorMin = new Vector2(0.5f, 0.5f);
        menuRoot.anchorMax = new Vector2(0.5f, 0.5f);
        menuRoot.pivot = new Vector2(0.5f, 0.5f);
        menuRoot.anchoredPosition = Vector2.zero;
        menuRoot.sizeDelta = new Vector2(640f, 520f);
    }

    private void ShowMainMenu()
    {
        mode = MenuMode.Main;
        ClearEntries();
        AddTitle("S_G");
        AddButton("게임 시작", StartGame);
        AddButton("옵션", ShowOptionsMenu);
        SelectIndex(0);
    }

    private void ShowOptionsMenu()
    {
        mode = MenuMode.Options;
        ClearEntries();
        AddTitle("옵션");
        AddButton(GetVolumeLabel(), CycleVolume);
        AddButton(GetFullscreenLabel(), ToggleFullscreen);
        AddButton("뒤로", ShowMainMenu);
        SelectIndex(0);
    }

    private void AddTitle(string label)
    {
        GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleObject.transform.SetParent(menuRoot, false);

        RectTransform titleTransform = titleObject.GetComponent<RectTransform>();
        titleTransform.anchorMin = new Vector2(0.5f, 1f);
        titleTransform.anchorMax = new Vector2(0.5f, 1f);
        titleTransform.pivot = new Vector2(0.5f, 1f);
        titleTransform.anchoredPosition = new Vector2(0f, -20f);
        titleTransform.sizeDelta = new Vector2(640f, 100f);

        Text titleText = titleObject.GetComponent<Text>();
        titleText.text = label;
        titleText.font = GetFont();
        titleText.fontSize = 64;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = textColor;
    }

    private void AddButton(string label, Action action)
    {
        int index = entries.Count;
        float top = -150f - index * 92f;

        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(EventTrigger));
        buttonObject.transform.SetParent(menuRoot, false);

        RectTransform buttonTransform = buttonObject.GetComponent<RectTransform>();
        buttonTransform.anchorMin = new Vector2(0.5f, 1f);
        buttonTransform.anchorMax = new Vector2(0.5f, 1f);
        buttonTransform.pivot = new Vector2(0.5f, 1f);
        buttonTransform.anchoredPosition = new Vector2(0f, top);
        buttonTransform.sizeDelta = new Vector2(420f, 64f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = normalColor;

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;

        EventTrigger trigger = buttonObject.GetComponent<EventTrigger>();
        EventTrigger.Entry pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        pointerEnter.callback.AddListener(_ => SelectIndex(index));
        trigger.triggers.Add(pointerEnter);

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.anchorMin = Vector2.zero;
        textTransform.anchorMax = Vector2.one;
        textTransform.offsetMin = Vector2.zero;
        textTransform.offsetMax = Vector2.zero;

        Text labelText = textObject.GetComponent<Text>();
        labelText.text = label;
        labelText.font = GetFont();
        labelText.fontSize = 32;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = textColor;

        entries.Add(new MenuEntry(buttonTransform, buttonImage, labelText, action));
    }

    private void SelectIndex(int index)
    {
        if (entries.Count == 0)
        {
            selectedIndex = 0;
            return;
        }

        selectedIndex = (index % entries.Count + entries.Count) % entries.Count;
        RefreshSelection();
    }

    private void ExecuteSelected()
    {
        if (selectedIndex < 0 || selectedIndex >= entries.Count)
        {
            return;
        }

        entries[selectedIndex].Action.Invoke();
    }

    private void HandleMouseInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        Vector2 mousePosition = mouse.position.ReadValue();
        for (int i = 0; i < entries.Count; i++)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(entries[i].RectTransform, mousePosition, canvas.worldCamera))
            {
                continue;
            }

            SelectIndex(i);
            if (mouse.leftButton.wasPressedThisFrame)
            {
                ExecuteSelected();
            }

            return;
        }
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].Background.color = i == selectedIndex ? selectedColor : normalColor;
        }
    }

    private void ClearEntries()
    {
        for (int i = menuRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(menuRoot.GetChild(i).gameObject);
        }

        entries.Clear();
        selectedIndex = 0;
    }

    private void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    private void CycleVolume()
    {
        volumePercent -= 25;
        if (volumePercent < 0)
        {
            volumePercent = 100;
        }

        AudioListener.volume = volumePercent / 100f;
        ShowOptionsMenu();
    }

    private void ToggleFullscreen()
    {
        fullscreen = !fullscreen;
        Screen.fullScreen = fullscreen;
        ShowOptionsMenu();
    }

    private string GetVolumeLabel()
    {
        return $"음량 {volumePercent}%";
    }

    private string GetFullscreenLabel()
    {
        return fullscreen ? "전체화면 켜짐" : "전체화면 꺼짐";
    }

    private Font GetFont()
    {
        if (menuFont != null)
        {
            return menuFont;
        }

        menuFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (menuFont == null)
        {
            Debug.LogWarning("Main menu font was not found. Menu button backgrounds will still be visible.");
        }

        return menuFont;
    }

    private struct MenuEntry
    {
        public MenuEntry(RectTransform rectTransform, Image background, Text label, Action action)
        {
            RectTransform = rectTransform;
            Background = background;
            Label = label;
            Action = action;
        }

        public RectTransform RectTransform { get; }
        public Image Background { get; }
        public Text Label { get; }
        public Action Action { get; }
    }
}
