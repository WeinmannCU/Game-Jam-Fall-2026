using UnityEngine;
using UnityEngine.UI;

public sealed class ShipCommentaryUI : MonoBehaviour
{
    [SerializeField] private ShipDamageTracker damageTracker;
    [SerializeField] private LocalGameMode gameMode;
    [SerializeField] private GameOverUI gameOverUI;
    [SerializeField] private RectTransform topHudPanel;
    [SerializeField] private RectTransform damageBar;
    [SerializeField] private RectTransform stopwatch;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float messageDuration = 4f;
    [SerializeField, Min(0.1f)] private float messageCooldown = 10f;
    [SerializeField, Min(0.1f)] private float stateCheckInterval = 1f;
    [SerializeField, Min(0.1f)] private float repairInactivitySeconds = 20f;

    private static readonly string[] playerOneLeadMessages =
    {
        "This man is useless.",
        "She's carrying this entire ship.",
        "Does he know what the repair button does?",
        "At least one of them is working."
    };

    private static readonly string[] playerTwoLeadMessages =
    {
        "Is she even trying?",
        "He's doing all the work.",
        "Someone tell her this isn't a cruise.",
        "He's practically rebuilding this boat alone."
    };

    private static readonly string[] equalMessages =
    {
        "Look at that. Actual teamwork.",
        "Two functioning brain cells. Incredible.",
        "Maybe there's hope for these two."
    };

    private static readonly string[] inactiveMessages =
    {
        "Two passengers. Zero mechanics.",
        "The boat is not going to fix itself.",
        "Are we just watching it sink?"
    };

    private static readonly string[] dangerMessages =
    {
        "This is fine. Everything is fine.",
        "The ocean is looking increasingly inviting.",
        "That seems like a concerning amount of water.",
        "I wouldn't get too attached to this boat."
    };

    private static readonly string[] finalWarningMessages =
    {
        "Well, it's been nice knowing you.",
        "Hope everyone can swim.",
        "The boat has officially given up."
    };

    private static readonly string[] singlePlayerMessages =
    {
        "She's doing the work of an entire crew.",
        "One woman versus an entire ocean.",
        "The boat is falling apart. No pressure.",
        "She's surprisingly good at delaying the inevitable.",
        "Management has declined to send reinforcements."
    };

    private static readonly int[] repairMilestones = { 3, 6, 10 };
    private static readonly string[] playerOneRepairMessages =
    {
        "She's keeping this disaster afloat.",
        "Finally, someone competent."
    };

    private static readonly string[] playerTwoRepairMessages =
    {
        "He does know how to use a hammer!",
        "A rare moment of usefulness."
    };

    private Text commentaryText;
    private PlayerController playerOne;
    private PlayerController playerTwo;
    private int observedPlayerOneRepairs;
    private int observedPlayerTwoRepairs;
    private int lastPerformanceState = int.MinValue;
    private float lastRepairTime;
    private float nextStateCheck;
    private float messageExpiresAt;
    private float nextOrdinaryMessageAt;
    private string pendingMessage;
    private string lastMessage;
    private bool inactivityMessageShown;
    private bool dangerMessageShown;
    private bool finalWarningShown;
    private bool sinking;
    private int lastScreenWidth = -1;
    private float lastCanvasScale = -1f;

    private void Awake()
    {
        damageTracker = damageTracker != null
            ? damageTracker
            : FindFirstObjectByType<ShipDamageTracker>();
        gameMode = gameMode != null
            ? gameMode
            : FindFirstObjectByType<LocalGameMode>();
        gameOverUI = gameOverUI != null
            ? gameOverUI
            : GetComponent<GameOverUI>();
        topHudPanel = topHudPanel != null
            ? topHudPanel
            : transform.Find("Top HUD Panel") as RectTransform;

        if (topHudPanel != null)
        {
            damageBar = damageBar != null
                ? damageBar
                : topHudPanel.Find("Damage Bar Frame") as RectTransform;
            stopwatch = stopwatch != null
                ? stopwatch
                : topHudPanel.Find("Survival Stopwatch") as RectTransform;
        }

        if (damageTracker == null || gameMode == null || gameOverUI == null ||
            topHudPanel == null || damageBar == null || stopwatch == null)
        {
            Debug.LogError("ShipCommentaryUI needs the damage tracker, game mode, GameOverUI, and HUD layout references.", this);
            enabled = false;
            return;
        }

        playerOne = gameMode.PlayerOne;
        playerTwo = gameMode.PlayerTwo;
        if (playerOne == null || playerTwo == null)
        {
            Debug.LogError("ShipCommentaryUI needs both PlayerController references through LocalGameMode.", this);
            enabled = false;
            return;
        }

        CreateCommentaryText();
        observedPlayerOneRepairs = gameMode.PlayerOneRepairs;
        observedPlayerTwoRepairs = gameMode.PlayerTwoRepairs;
        lastRepairTime = Time.unscaledTime;
        UpdateLayout();
    }

    private void OnEnable()
    {
        if (damageTracker != null)
        {
            damageTracker.DamageChanged += EvaluateDamage;
            damageTracker.GameOver += StopForSinking;
        }

        if (gameOverUI != null)
        {
            gameOverUI.CommentaryStopped += StopForSinking;
        }
    }

    private void OnDisable()
    {
        if (damageTracker != null)
        {
            damageTracker.DamageChanged -= EvaluateDamage;
            damageTracker.GameOver -= StopForSinking;
        }

        if (gameOverUI != null)
        {
            gameOverUI.CommentaryStopped -= StopForSinking;
        }
    }

    private void Update()
    {
        if (sinking)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (Screen.width != lastScreenWidth ||
            !Mathf.Approximately(topHudPanel.GetComponentInParent<Canvas>().scaleFactor, lastCanvasScale))
        {
            UpdateLayout();
        }

        if (now >= nextStateCheck)
        {
            nextStateCheck = now + stateCheckInterval;
            CheckRepairPerformance(now);
        }

        if (commentaryText.text.Length > 0 && now >= messageExpiresAt)
        {
            commentaryText.text = string.Empty;
        }

        if (commentaryText.text.Length == 0 && !string.IsNullOrEmpty(pendingMessage) &&
            now >= nextOrdinaryMessageAt)
        {
            ShowMessage(pendingMessage, now, critical: false);
            pendingMessage = null;
        }
    }

    private void CheckRepairPerformance(float now)
    {
        int playerOneRepairs = gameMode.PlayerOneRepairs;
        int playerTwoRepairs = gameMode.PlayerTwoRepairs;
        bool repairsChanged = playerOneRepairs != observedPlayerOneRepairs ||
            playerTwoRepairs != observedPlayerTwoRepairs;

        if (repairsChanged)
        {
            lastRepairTime = now;
            inactivityMessageShown = false;
            CheckRepairMilestones(playerOneRepairs, playerTwoRepairs);
            observedPlayerOneRepairs = playerOneRepairs;
            observedPlayerTwoRepairs = playerTwoRepairs;
            EvaluateRepairBalance(playerOneRepairs, playerTwoRepairs);
        }
        else if (!inactivityMessageShown && now - lastRepairTime >= repairInactivitySeconds)
        {
            inactivityMessageShown = true;
            QueueMessage(
                Pick(gameMode.TwoPlayerMode
                    ? inactiveMessages
                    : new[] { "The boat is not going to fix itself.", "Are we just watching it sink?" }),
                priority: 1);
        }
    }

    private void CheckRepairMilestones(int playerOneRepairs, int playerTwoRepairs)
    {
        for (int i = 0; i < repairMilestones.Length; i++)
        {
            if (observedPlayerOneRepairs < repairMilestones[i] && playerOneRepairs >= repairMilestones[i])
            {
                QueueMessage(
                    Pick(gameMode.TwoPlayerMode ? playerOneRepairMessages : singlePlayerMessages),
                    priority: 1);
            }

            if (gameMode.TwoPlayerMode &&
                observedPlayerTwoRepairs < repairMilestones[i] &&
                playerTwoRepairs >= repairMilestones[i])
            {
                QueueMessage(Pick(playerTwoRepairMessages), priority: 1);
            }
        }
    }

    private void EvaluateRepairBalance(int playerOneRepairs, int playerTwoRepairs)
    {
        if (!gameMode.TwoPlayerMode || playerOneRepairs + playerTwoRepairs < 3)
        {
            return;
        }

        int difference = playerOneRepairs - playerTwoRepairs;
        int performanceState = difference >= 3 ? 1 : difference <= -3 ? -1 : 0;
        if (performanceState == lastPerformanceState)
        {
            return;
        }

        lastPerformanceState = performanceState;
        string[] messages = performanceState > 0
            ? playerOneLeadMessages
            : performanceState < 0
                ? playerTwoLeadMessages
                : equalMessages;
        QueueMessage(Pick(messages), priority: 1);
    }

    private void EvaluateDamage()
    {
        if (sinking)
        {
            return;
        }

        if (damageTracker.IsFinalTileWarning)
        {
            if (!finalWarningShown)
            {
                finalWarningShown = true;
                dangerMessageShown = true;
                QueueMessage(Pick(finalWarningMessages), priority: 3);
            }

            return;
        }

        finalWarningShown = false;
        if (damageTracker.DamageFraction >= 0.65f && !dangerMessageShown)
        {
            dangerMessageShown = true;
            QueueMessage(Pick(dangerMessages), priority: 2);
        }
        else if (damageTracker.DamageFraction < 0.6f)
        {
            dangerMessageShown = false;
        }
    }

    private void QueueMessage(string message, int priority)
    {
        if (sinking || string.IsNullOrEmpty(message) || message == lastMessage)
        {
            return;
        }

        if (priority >= 2)
        {
            ShowMessage(message, Time.unscaledTime, critical: true);
            pendingMessage = null;
            return;
        }

        if (Time.unscaledTime >= nextOrdinaryMessageAt && commentaryText.text.Length == 0)
        {
            ShowMessage(message, Time.unscaledTime, critical: false);
            return;
        }

        if (string.IsNullOrEmpty(pendingMessage))
        {
            pendingMessage = message;
        }
    }

    private void ShowMessage(string message, float now, bool critical)
    {
        commentaryText.text = message;
        lastMessage = message;
        messageExpiresAt = now + messageDuration;
        if (!critical)
        {
            nextOrdinaryMessageAt = now + messageCooldown;
        }
    }

    private void StopForSinking()
    {
        sinking = true;
        pendingMessage = null;
        commentaryText.text = string.Empty;
    }

    private void CreateCommentaryText()
    {
        GameObject textObject = new GameObject("Ship Commentary", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(topHudPanel, false);
        commentaryText = textObject.GetComponent<Text>();
        commentaryText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        commentaryText.fontSize = 20;
        commentaryText.fontStyle = FontStyle.Bold;
        commentaryText.color = Color.white;
        commentaryText.alignment = TextAnchor.MiddleCenter;
        commentaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
        commentaryText.verticalOverflow = VerticalWrapMode.Truncate;
        commentaryText.raycastTarget = false;
        commentaryText.text = string.Empty;
    }

    private void UpdateLayout()
    {
        Canvas canvas = topHudPanel.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("ShipCommentaryUI's HUD panel must be under a Canvas.", this);
            enabled = false;
            return;
        }

        RectTransform textRect = commentaryText.rectTransform;
        Vector3[] corners = new Vector3[4];
        damageBar.GetWorldCorners(corners);
        float left = topHudPanel.InverseTransformPoint(corners[3]).x + 20f;
        stopwatch.GetWorldCorners(corners);
        float right = topHudPanel.InverseTransformPoint(corners[0]).x - 20f;
        float availableWidth = right - left;
        float leftInset = left + topHudPanel.rect.width * 0.5f;
        float rightInset = topHudPanel.rect.width * 0.5f - right;

        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(leftInset, -79f);
        textRect.offsetMax = new Vector2(-rightInset, -27f);
        commentaryText.enabled = availableWidth >= 100f;
        commentaryText.fontSize = availableWidth < 220f ? 18 : availableWidth < 360f ? 22 : 24;
        lastScreenWidth = Screen.width;
        lastCanvasScale = canvas.scaleFactor;
    }

    private string Pick(string[] messages)
    {
        if (messages.Length == 1)
        {
            return messages[0];
        }

        string message = messages[Random.Range(0, messages.Length)];
        while (message == lastMessage)
        {
            message = messages[Random.Range(0, messages.Length)];
        }

        return message;
    }
}
