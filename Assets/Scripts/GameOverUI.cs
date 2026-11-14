using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class GameOverUI : MonoBehaviour
{
    [SerializeField] private ShipDamageTracker damageTracker;
    [SerializeField] private SurvivalTimer survivalTimer;
    [SerializeField] private LocalGameMode gameMode;

    private GameObject overlay;
    private Text survivedLabel;
    private bool isShowing;

    private void Awake()
    {
        damageTracker = damageTracker != null
            ? damageTracker
            : FindFirstObjectByType<ShipDamageTracker>();
        survivalTimer = survivalTimer != null
            ? survivalTimer
            : FindFirstObjectByType<SurvivalTimer>();
        gameMode = gameMode != null
            ? gameMode
            : FindFirstObjectByType<LocalGameMode>();

        if (damageTracker == null || survivalTimer == null || gameMode == null || GetComponent<Canvas>() == null)
        {
            Debug.LogError("GameOverUI needs a Canvas, ShipDamageTracker, SurvivalTimer, and LocalGameMode.", this);
            enabled = false;
            return;
        }

        EnsureEventSystem();
        CreateOverlay();
    }

    private void OnEnable()
    {
        if (damageTracker != null)
        {
            damageTracker.GameOver += ShowGameOver;
        }
    }

    private void OnDisable()
    {
        if (damageTracker != null)
        {
            damageTracker.GameOver -= ShowGameOver;
        }
    }

    private void ShowGameOver()
    {
        if (isShowing)
        {
            return;
        }

        isShowing = true;
        long milliseconds = (long)Math.Floor(survivalTimer.ElapsedSeconds * 1000d);
        long totalSeconds = milliseconds / 1000;
        survivedLabel.text = $"You survived: {totalSeconds / 60:00}:{totalSeconds % 60:00}.{milliseconds % 1000:000}";
        survivalTimer.Stop();
        gameMode.SetGameplayEnabled(false);
        overlay.SetActive(true);
        Time.timeScale = 0f;
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        LocalGameMode.PreserveModeForReload(gameMode.TwoPlayerMode);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void ReturnHome()
    {
        // TODO: Return to the main menu when the title screen is implemented.
    }

    private void CreateOverlay()
    {
        overlay = new GameObject("Game Over Overlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(transform, false);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image background = overlay.GetComponent<Image>();
        background.color = new Color(0.025f, 0.035f, 0.07f, 0.94f);
        overlay.SetActive(false);

        Text title = CreateText("Title", overlay.transform, "SHIP SUNK", 48, 110f);
        survivedLabel = CreateText("Survival Time", overlay.transform, "You survived: 00:00.000", 28, 35f);
        title.fontStyle = FontStyle.Bold;

        CreateButton(overlay.transform, "Play Again", -55f, RestartGame);
        CreateButton(overlay.transform, "Home", -125f, ReturnHome);
        overlay.transform.SetAsLastSibling();
    }

    private static Text CreateText(string objectName, Transform parent, string value, int fontSize, float y)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(700f, 60f);

        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return text;
    }

    private void CreateButton(Transform parent, string label, float y, Action onClick)
    {
        GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(240f, 54f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.15f, 0.28f, 0.45f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => onClick());
        CreateText("Label", buttonObject.transform, label, 22, 0f).rectTransform.sizeDelta = rect.sizeDelta;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
