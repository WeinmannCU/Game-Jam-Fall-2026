using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class LocalGameMode : MonoBehaviour
{
    private static bool preserveModeOnNextLoad;
    private static bool preservedTwoPlayerMode;

    [SerializeField] private bool twoPlayerMode;
    [SerializeField] private PlayerController playerOne;
    [SerializeField] private PlayerController playerTwo;
    [SerializeField] private CameraFollow sharedCamera;

    public bool TwoPlayerMode => twoPlayerMode;
    public int PlayerOneRepairs => playerOne != null ? playerOne.RepairsCompleted : 0;
    public int PlayerTwoRepairs => playerTwo != null ? playerTwo.RepairsCompleted : 0;

    public static void PreserveModeForReload(bool twoPlayer)
    {
        preservedTwoPlayerMode = twoPlayer;
        preserveModeOnNextLoad = true;
    }

    private void Awake()
    {
        if (preserveModeOnNextLoad)
        {
            twoPlayerMode = preservedTwoPlayerMode;
            preserveModeOnNextLoad = false;
        }
        else
        {
            twoPlayerMode = PlayerPrefs.GetInt(
                MainMenu.TwoPlayerModeKey,
                twoPlayerMode ? 1 : 0) == 1;
        }
        if (playerOne == null || playerTwo == null || sharedCamera == null)
        {
            Debug.LogError("LocalGameMode needs both players and the shared CameraFollow reference.", this);
            enabled = false;
            return;
        }

        Camera camera = sharedCamera.GetComponent<Camera>();
        if (camera == null)
        {
            Debug.LogError("LocalGameMode needs a Camera component on the shared camera.", sharedCamera);
            enabled = false;
            return;
        }

        playerOne.ConfigureInput(false, twoPlayerMode, camera);
        playerTwo.ConfigureInput(true, twoPlayerMode, camera);
        playerTwo.gameObject.SetActive(twoPlayerMode);
        sharedCamera.ConfigureTargets(
            playerOne.transform,
            twoPlayerMode ? playerTwo.transform : null,
            twoPlayerMode);
    }

    public void SetGameplayEnabled(bool gameplayEnabled)
    {
        if (playerOne != null)
        {
            playerOne.enabled = gameplayEnabled;
        }

        if (playerTwo != null)
        {
            playerTwo.enabled = gameplayEnabled && twoPlayerMode;
        }
    }
}
