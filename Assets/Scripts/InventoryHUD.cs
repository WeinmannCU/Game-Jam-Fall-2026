using UnityEngine;
using UnityEngine.UI;

public sealed class InventoryHUD : MonoBehaviour
{
    [SerializeField] private LocalGameMode gameMode;
    [SerializeField] private RectTransform topHudPanel;

    private Text inventoryText;
    private DriftwoodInventory playerOneInventory;
    private DriftwoodInventory playerTwoInventory;
    private int lastScreenWidth = -1;

    private void Awake()
    {
        gameMode = gameMode != null
            ? gameMode
            : FindFirstObjectByType<LocalGameMode>();
        topHudPanel = topHudPanel != null
            ? topHudPanel
            : transform.Find("Top HUD Panel") as RectTransform;

        if (gameMode == null || topHudPanel == null)
        {
            Debug.LogError("InventoryHUD needs LocalGameMode and the Top HUD Panel.", this);
            enabled = false;
            return;
        }

        CreateInventoryText();
    }

    private void Start()
    {
        PlayerController playerOne = gameMode.PlayerOne;
        PlayerController playerTwo = gameMode.PlayerTwo;
        playerOneInventory = playerOne != null ? playerOne.Inventory : null;
        playerTwoInventory = playerTwo != null ? playerTwo.Inventory : null;

        if (playerOneInventory == null || (gameMode.TwoPlayerMode && playerTwoInventory == null))
        {
            Debug.LogError("InventoryHUD needs a DriftwoodInventory on each active player.", this);
            enabled = false;
            return;
        }

        playerOneInventory.InventoryChanged += UpdateInventoryText;
        if (gameMode.TwoPlayerMode)
        {
            playerTwoInventory.InventoryChanged += UpdateInventoryText;
        }

        UpdateInventoryText();
        UpdateLayout();
    }

    private void OnDisable()
    {
        if (playerOneInventory != null)
        {
            playerOneInventory.InventoryChanged -= UpdateInventoryText;
        }

        if (playerTwoInventory != null)
        {
            playerTwoInventory.InventoryChanged -= UpdateInventoryText;
        }
    }

    private void Update()
    {
        if (Screen.width != lastScreenWidth)
        {
            UpdateLayout();
        }
    }

    private void CreateInventoryText()
    {
        GameObject textObject = new GameObject("Driftwood Inventory", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(topHudPanel, false);

        inventoryText = textObject.GetComponent<Text>();
        Text stopwatch = topHudPanel.Find("Survival Stopwatch")?.GetComponent<Text>();
        inventoryText.font = stopwatch != null
            ? stopwatch.font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        inventoryText.fontSize = 24;
        inventoryText.fontStyle = FontStyle.Bold;
        inventoryText.color = Color.white;
        inventoryText.alignment = TextAnchor.MiddleCenter;
        inventoryText.horizontalOverflow = HorizontalWrapMode.Overflow;
        inventoryText.verticalOverflow = VerticalWrapMode.Truncate;
        inventoryText.raycastTarget = false;
        inventoryText.text = string.Empty;
    }

    private void UpdateInventoryText()
    {
        if (inventoryText == null || playerOneInventory == null)
        {
            return;
        }

        inventoryText.text = gameMode.TwoPlayerMode
            ? $"P1 Wood: {playerOneInventory.WoodCount}    P2 Wood: {playerTwoInventory.WoodCount}"
            : $"P1 Wood: {playerOneInventory.WoodCount}";
    }

    private void UpdateLayout()
    {
        if (inventoryText == null)
        {
            return;
        }

        RectTransform textRect = inventoryText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(12f, -104f);
        textRect.offsetMax = new Vector2(-12f, -72f);
        inventoryText.fontSize = Screen.width < 700 ? 22 : 26;
        lastScreenWidth = Screen.width;
    }
}
