using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenu : MonoBehaviour
{
    public const string TwoPlayerModeKey = "TwoPlayerMode";

    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject playerSelectPanel;
    [SerializeField] private string gameSceneName;

    private bool loadingGame;

    private void Awake()
    {
        startPanel.SetActive(true);
        playerSelectPanel.SetActive(false);
        CreateReturnToTitleButton(playerSelectPanel.transform);
    }

    public void ShowPlayerSelection()
    {
        startPanel.SetActive(false);
        playerSelectPanel.SetActive(true);
    }

    public void ReturnToTitle()
    {
        startPanel.SetActive(true);
        playerSelectPanel.SetActive(false);
    }

    private void CreateReturnToTitleButton(Transform parent)
    {
        GameObject buttonObject = new GameObject(
            "Return to Title Button",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(24f, 24f);
        rect.sizeDelta = new Vector2(210f, 54f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.3f, 0.34f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(ReturnToTitle);

        GameObject labelObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text label = labelObject.GetComponent<Text>();
        label.text = "Return to Title";
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 20;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
    }

    public void StartOnePlayer()
    {
        StartGame(false);
    }

    public void StartTwoPlayers()
    {
        StartGame(true);
    }

    private void StartGame(bool twoPlayers)
    {
        if (loadingGame)
        {
            return;
        }

        loadingGame = true;

        PlayerPrefs.SetInt(TwoPlayerModeKey, twoPlayers ? 1 : 0);
        SceneManager.LoadSceneAsync(gameSceneName);
    }
}