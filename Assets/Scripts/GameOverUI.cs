using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public sealed class GameOverUI : MonoBehaviour
{
    [SerializeField] private ShipDamageTracker damageTracker;
    [SerializeField] private SurvivalTimer survivalTimer;
    [SerializeField] private LocalGameMode gameMode;
    [SerializeField] private TilemapRenderer wallsRenderer;
    [SerializeField] private TilemapRenderer boatRenderer;
    [SerializeField] private Tilemap playableFloorTilemap;
    [SerializeField] private Tilemap collapsedDeckTilemap;
    [SerializeField] private TileBase driftwoodTile;

    private GameObject overlay;
    private Text survivedLabel;
    private Text repairsLabel;
    private bool isShowing;
    private readonly List<Vector3Int> playableDeckCells = new List<Vector3Int>();

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
        playableFloorTilemap = playableFloorTilemap != null
            ? playableFloorTilemap
            : GameObject.Find("Grid/Broken tiles")?.GetComponent<Tilemap>();

        if (damageTracker == null || survivalTimer == null || gameMode == null || GetComponent<Canvas>() == null)
        {
            Debug.LogError("GameOverUI needs a Canvas, ShipDamageTracker, SurvivalTimer, and LocalGameMode.", this);
            enabled = false;
            return;
        }

        CapturePlayableDeckCells();
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
        repairsLabel.text = gameMode.TwoPlayerMode
            ? $"Player 1 Repairs: {gameMode.PlayerOneRepairs}\nPlayer 2 Repairs: {gameMode.PlayerTwoRepairs}"
            : $"Repairs Completed: {gameMode.PlayerOneRepairs}";
        survivalTimer.Stop();
        gameMode.SetGameplayEnabled(false);
        Time.timeScale = 0f;
        CommentaryStopped?.Invoke();
        StartCoroutine(ShowSinkingSequence());
    }

    public event Action CommentaryStopped;

    private IEnumerator ShowSinkingSequence()
    {
        yield return new WaitForSecondsRealtime(1f);
        ApplySinkingVisuals();

        yield return new WaitForSecondsRealtime(1f);
        overlay.SetActive(true);
    }

    private void ApplySinkingVisuals()
    {
        if (wallsRenderer == null || boatRenderer == null ||
            playableFloorTilemap == null || collapsedDeckTilemap == null ||
            driftwoodTile == null || playableDeckCells.Count == 0)
        {
            Debug.LogError("GameOverUI needs the boat renderers, playable floor, collapsed deck tilemap, and driftwood tile.", this);
            return;
        }

        wallsRenderer.enabled = false;
        boatRenderer.enabled = false;

        foreach (Vector3Int cell in collapsedDeckTilemap.cellBounds.allPositionsWithin)
        {
            if (!playableDeckCells.Contains(cell) && collapsedDeckTilemap.HasTile(cell))
            {
                collapsedDeckTilemap.SetTile(cell, null);
            }
        }

        foreach (Vector3Int cell in playableDeckCells)
        {
            collapsedDeckTilemap.SetTile(cell, driftwoodTile);
            int quarterTurns = UnityEngine.Random.Range(0, 4);
            collapsedDeckTilemap.SetTransformMatrix(
                cell,
                Matrix4x4.TRS(
                    Vector3.zero,
                    Quaternion.Euler(0f, 0f, quarterTurns * 90f),
                    Vector3.one));
        }
    }

    private void CapturePlayableDeckCells()
    {
        if (playableFloorTilemap == null || wallsRenderer == null)
        {
            Debug.LogError("GameOverUI needs the playable floor tilemap and Walls renderer.", this);
            return;
        }

        Tilemap wallsTilemap = wallsRenderer.GetComponent<Tilemap>();
        if (wallsTilemap == null)
        {
            Debug.LogError("GameOverUI's Walls renderer must be on a Tilemap.", this);
            return;
        }

        foreach (Vector3Int cell in playableFloorTilemap.cellBounds.allPositionsWithin)
        {
            if (playableFloorTilemap.HasTile(cell) && !wallsTilemap.HasTile(cell))
            {
                playableDeckCells.Add(cell);
            }
        }
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        LocalGameMode.PreserveModeForReload(gameMode.TwoPlayerMode);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void ReturnHome()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main Menu");
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
        repairsLabel = CreateText("Repair Statistics", overlay.transform, string.Empty, 22, -20f);
        title.fontStyle = FontStyle.Bold;

        CreateButton(overlay.transform, "Play Again", -100f, RestartGame);
        CreateButton(overlay.transform, "Home", -170f, ReturnHome);
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
