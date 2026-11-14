using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class LocalGameMode : MonoBehaviour
{
    [SerializeField] private bool twoPlayerMode;
    [SerializeField] private PlayerController playerOne;
    [SerializeField] private PlayerController playerTwo;
    [SerializeField] private CameraFollow sharedCamera;

    public bool TwoPlayerMode => twoPlayerMode;

    private void Awake()
    {
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
}
