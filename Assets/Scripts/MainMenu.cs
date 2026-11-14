using UnityEngine;
using UnityEngine.SceneManagement;

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
    }

    public void ShowPlayerSelection()
    {
        startPanel.SetActive(false);
        playerSelectPanel.SetActive(true);
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