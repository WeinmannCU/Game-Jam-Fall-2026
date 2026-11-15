using UnityEngine;
using UnityEngine.UI;

public sealed class MenuControlsGuide : MonoBehaviour
{
    private GameObject guidePanel;
    private MainMenu mainMenu;

    private void Awake()
    {
        Canvas canvas = GetComponent<Canvas>();
        mainMenu = FindFirstObjectByType<MainMenu>();
        if (canvas == null)
        {
            Debug.LogError("MenuControlsGuide must be attached to a Canvas.", this);
            enabled = false;
            return;
        }

        if (mainMenu == null)
        {
            Debug.LogError("MenuControlsGuide needs the scene's MainMenu reference.", this);
            enabled = false;
            return;
        }

        CreateControlsButton(canvas.transform);
        CreateGuidePanel(canvas.transform);
    }

    private void CreateControlsButton(Transform parent)
    {
        Button button = CreateButton(parent, "Controls", new Vector2(1f, 0f), new Vector2(-18f, 20f), new Vector2(174f, 50f));
        button.onClick.AddListener(() => guidePanel.SetActive(true));
    }

    private void CreateGuidePanel(Transform parent)
    {
        guidePanel = new GameObject("Controls Guide Overlay", typeof(RectTransform), typeof(Image));
        guidePanel.transform.SetParent(parent, false);
        RectTransform overlayRect = guidePanel.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        guidePanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

        GameObject panel = new GameObject("Controls Guide Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(guidePanel.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(680f, 440f);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.18f, 0.22f, 0.98f);

        CreateText(panel.transform, "CONTROLS", 30, new Vector2(0f, 176f), new Vector2(620f, 46f), FontStyle.Bold, TextAnchor.MiddleCenter);
        CreateText(
            panel.transform,
            "PLAYER 1\nMove: WASD\nJump: Space\nInteract / Repair: F\n\nPLAYER 2\nMove: Arrow Keys\nJump: Slash (/)\nInteract / Repair: Right Shift\n\nSingle-player: Player 1 can use WASD or Arrow Keys.\nMultiplayer: Player 1 uses WASD; Player 2 uses Arrow Keys.\nJump to cross the boat's outer boundary.\nStand near a broken plank and press Interact to collect driftwood or repair.",
            18,
            new Vector2(0f, -5f),
            new Vector2(610f, 330f),
            FontStyle.Normal,
            TextAnchor.MiddleCenter);

        Button returnButton = CreateButton(
            panel.transform,
            "Return to Title",
            new Vector2(0.5f, 0f),
            new Vector2(-190f, 18f),
            new Vector2(220f, 54f));
        returnButton.onClick.AddListener(() =>
        {
            guidePanel.SetActive(false);
            mainMenu.ReturnToTitle();
        });

        Button closeButton = CreateButton(
            panel.transform,
            "Close",
            new Vector2(0.5f, 0f),
            new Vector2(150f, 18f),
            new Vector2(174f, 50f));
        closeButton.onClick.AddListener(() => guidePanel.SetActive(false));
        guidePanel.SetActive(false);
    }

    private static Button CreateButton(Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.3f, 0.34f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        CreateText(buttonObject.transform, label, 20, Vector2.zero, size, FontStyle.Bold, TextAnchor.MiddleCenter);
        return button;
    }

    private static Text CreateText(
        Transform parent,
        string value,
        int fontSize,
        Vector2 position,
        Vector2 size,
        FontStyle style,
        TextAnchor alignment)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }
}
