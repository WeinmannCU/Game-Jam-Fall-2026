using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class PlayerController : MonoBehaviour
{
    private const float SafePositionSearchStep = 0.025f;

    private readonly List<Collider2D> overlappingColliders = new List<Collider2D>();
    private readonly Vector3[] hudPanelCorners = new Vector3[4];

    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Tilemap boatTilemap;
    [SerializeField] private BoatWaterBoundary boatBoundary;
    [SerializeField] private Transform visual;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private RectTransform hudPanel;
    [SerializeField] private BoatTileDamage boatTileDamage;
    [SerializeField] private DriftwoodInventory driftwoodInventory;
    [SerializeField, Min(0.1f)] private float driftwoodInteractionRadius = 0.85f;
    [SerializeField, Min(0f)] private float boatMoveSpeed = 4f;
    [SerializeField, Min(0f)] private float waterMoveSpeed = 2f;
    [SerializeField, Min(0.1f)] private float jumpDuration = 0.45f;
    [SerializeField, Min(1f)] private float jumpPeakScale = 1.3f;
    [SerializeField, Min(0f)] private float screenEdgeMargin = 0.1f;

    private Rigidbody2D body;
    private CircleCollider2D playerCollider;
    private InputActionAsset runtimeActions;
    private InputActionMap playerActionMap;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction interactAction;
    private Vector2 moveInput;
    private Vector2 lastFacingDirection = Vector2.up;
    private Vector2 jumpDirection;
    private float jumpStartSpeed;
    private Vector2 jumpStartPosition;
    private Vector2 lastSafePosition;
    private Vector3 visualBaseScale;
    private float jumpElapsed;
    private bool isJumping;
    private bool isLandingRecovery;
    private bool hasSafePosition;
    private bool isPlayerTwo;
    private bool twoPlayerMode;

    public int RepairsCompleted { get; private set; }
    public DriftwoodInventory Inventory => driftwoodInventory;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<CircleCollider2D>();
        driftwoodInventory = driftwoodInventory != null
            ? driftwoodInventory
            : GetComponent<DriftwoodInventory>();

        if (inputActions == null || boatTilemap == null || boatBoundary == null || visual == null)
        {
            Debug.LogError(
                "PlayerController needs the Input Actions asset, Boat Tilemap, BoatWaterBoundary, and Visual child.",
                this);
            enabled = false;
            return;
        }

        visualBaseScale = visual.localScale;
    }

    private void Start()
    {
        InitializeInput();
    }

    public void ConfigureInput(bool playerTwo, bool useTwoPlayerMode, Camera camera)
    {
        isPlayerTwo = playerTwo;
        twoPlayerMode = useTwoPlayerMode;
        gameplayCamera = camera;
        RepairsCompleted = 0;
        driftwoodInventory?.ResetInventory();
    }

    public bool InteractPressedThisFrame => interactAction != null && interactAction.WasPressedThisFrame();

    private void InitializeInput()
    {
        runtimeActions = Instantiate(inputActions);
        string mapName = twoPlayerMode
            ? (isPlayerTwo ? "Player 2" : "Player 1 Co-op")
            : "Player";
        playerActionMap = runtimeActions.FindActionMap(mapName, throwIfNotFound: false);
        moveAction = playerActionMap?.FindAction("Move", throwIfNotFound: false);
        jumpAction = playerActionMap?.FindAction("Jump", throwIfNotFound: false);
        interactAction = playerActionMap?.FindAction("Interact", throwIfNotFound: false);

        if (moveAction == null || jumpAction == null || interactAction == null)
        {
            Debug.LogError(
                $"PlayerController needs the {mapName}/Move, {mapName}/Jump, and {mapName}/Interact input actions.",
                this);
            enabled = false;
            return;
        }

        playerActionMap.Enable();
    }

    private void OnEnable()
    {
        playerActionMap?.Enable();
    }

    private void OnDisable()
    {
        EndJumpSafely();
        moveInput = Vector2.zero;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        playerActionMap?.Disable();
        RestoreBoundaryCollision();
    }

    private void OnDestroy()
    {
        RestoreBoundaryCollision();

        if (runtimeActions != null)
        {
            runtimeActions.Disable();
            Destroy(runtimeActions);
        }
    }

    private void Update()
    {
        if (moveAction == null)
        {
            return;
        }

        moveInput = moveAction.ReadValue<Vector2>();
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        if (moveInput.sqrMagnitude > 0.001f)
        {
            lastFacingDirection = moveInput.normalized;
        }

        if (jumpAction.WasPressedThisFrame())
        {
            TryStartJump();
        }

        if (interactAction != null && interactAction.WasPressedThisFrame())
        {
            if (TryCollectNearbyDriftwood())
            {
                UpdateJumpVisual();
                return;
            }

            if (boatTileDamage != null && driftwoodInventory != null &&
                driftwoodInventory.WoodCount > 0 && boatTileDamage.TryRepairNearby(transform))
            {
                if (driftwoodInventory.TryConsumeWood())
                {
                    RepairsCompleted++;
                }
                else
                {
                    Debug.LogError("A successful repair could not consume its required driftwood.", this);
                }
            }
        }

        UpdateJumpVisual();
    }

    private bool TryCollectNearbyDriftwood()
    {
        if (driftwoodInventory == null || driftwoodInventory.IsFull)
        {
            return false;
        }

        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(transform.position, driftwoodInteractionRadius);
        foreach (Collider2D nearbyCollider in nearbyColliders)
        {
            DriftwoodCollectible collectible = nearbyCollider.GetComponent<DriftwoodCollectible>();
            if (collectible != null)
            {
                collectible.TryCollect(driftwoodInventory);
                return true;
            }
        }

        return false;
    }

    private void FixedUpdate()
    {
        if (body == null || boatTilemap == null)
        {
            return;
        }

        if (isJumping)
        {
            if (isLandingRecovery)
            {
                SetMovementVelocity(Vector2.zero);
                if (TryRestoreToSafePosition())
                {
                    CompleteJump();
                }

                return;
            }

            if (boatBoundary.IsPlayerClear(playerCollider))
            {
                lastSafePosition = body.position;
                hasSafePosition = true;
            }

            float jumpSpeed = Mathf.Min(GetMovementSpeed(body.position), jumpStartSpeed);
            SetMovementVelocity(jumpDirection * jumpSpeed);
            jumpElapsed += Time.fixedDeltaTime;

            if (jumpElapsed >= jumpDuration)
            {
                EndJumpSafely();
            }

            if (isJumping)
            {
                return;
            }
        }

        Vector2 movement = moveInput * GetMovementSpeed(body.position);
        SetMovementVelocity(movement);
    }

    private void SetMovementVelocity(Vector2 velocity)
    {
        if (gameplayCamera != null && playerCollider != null)
        {
            float cameraDepth = Vector3.Dot(
                transform.position - gameplayCamera.transform.position,
                gameplayCamera.transform.forward);
            Vector3 lowerLeft = gameplayCamera.ViewportToWorldPoint(new Vector3(0f, 0f, cameraDepth));
            Vector3 upperRight = gameplayCamera.ViewportToWorldPoint(new Vector3(1f, 1f, cameraDepth));
            float horizontalPadding = playerCollider.bounds.extents.x + screenEdgeMargin;
            float verticalPadding = playerCollider.bounds.extents.y + screenEdgeMargin;
            float minX = lowerLeft.x + horizontalPadding;
            float maxX = upperRight.x - horizontalPadding;
            float minY = lowerLeft.y + verticalPadding;
            float maxY = upperRight.y - verticalPadding;

            if (hudPanel != null)
            {
                hudPanel.GetWorldCorners(hudPanelCorners);
                Canvas canvas = hudPanel.GetComponentInParent<Canvas>();
                Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
                float hudBottomScreenY = RectTransformUtility.WorldToScreenPoint(uiCamera, hudPanelCorners[0]).y;
                float hudBottomWorldY = gameplayCamera.ScreenToWorldPoint(
                    new Vector3(0f, hudBottomScreenY, cameraDepth)).y;
                maxY = Mathf.Min(maxY, hudBottomWorldY - verticalPadding);
            }

            Vector2 nextPosition = body.position + velocity * Time.fixedDeltaTime;

            if (minX > maxX
                || (nextPosition.x < minX && velocity.x < 0f)
                || (nextPosition.x > maxX && velocity.x > 0f))
            {
                velocity.x = 0f;
            }

            if (minY > maxY
                || (nextPosition.y < minY && velocity.y < 0f)
                || (nextPosition.y > maxY && velocity.y > 0f))
            {
                velocity.y = 0f;
            }
        }

        body.linearVelocity = velocity;
    }

    private void TryStartJump()
    {
        if (isJumping || body == null || playerCollider == null || boatBoundary == null)
        {
            return;
        }

        jumpDirection = moveInput.sqrMagnitude > 0.001f
            ? moveInput.normalized
            : lastFacingDirection;

        if (jumpDirection.sqrMagnitude < 0.001f)
        {
            jumpDirection = Vector2.up;
        }

        jumpStartPosition = body.position;
        jumpStartSpeed = GetMovementSpeed(body.position);
        lastSafePosition = jumpStartPosition;
        hasSafePosition = boatBoundary.IsPlayerClear(playerCollider);
        jumpElapsed = 0f;
        isJumping = true;
        boatBoundary.SetPlayerCollisionIgnored(playerCollider, true);
    }

    private void EndJumpSafely()
    {
        if (isJumping && body != null && playerCollider != null && boatBoundary != null
            && !boatBoundary.IsPlayerClear(playerCollider))
        {
            if (!TryRestoreToSafePosition())
            {
                body.linearVelocity = Vector2.zero;
                isLandingRecovery = true;
                Debug.LogError("PlayerController could not find a clear landing position; it will keep retrying.", this);
                return;
            }

            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        CompleteJump();
    }

    private void CompleteJump()
    {
        isJumping = false;
        isLandingRecovery = false;
        jumpElapsed = 0f;
        jumpDirection = Vector2.zero;

        if (visual != null)
        {
            visual.localScale = visualBaseScale;
        }

        RestoreBoundaryCollision();
    }

    private bool TryRestoreToSafePosition()
    {
        Vector2 originalPosition = body.position;
        Vector2 anchorPosition = hasSafePosition ? lastSafePosition : jumpStartPosition;
        if (hasSafePosition && TryMoveToClearPosition(anchorPosition))
        {
            return true;
        }

        Vector2 backward = jumpDirection.sqrMagnitude > 0.001f
            ? -jumpDirection.normalized
            : Vector2.up;
        Vector2 perpendicular = new Vector2(-backward.y, backward.x);
        float searchDistance = Mathf.Max(playerCollider.radius * 2f, SafePositionSearchStep);

        for (float distance = SafePositionSearchStep; distance <= searchDistance; distance += SafePositionSearchStep)
        {
            if (TryMoveToClearPosition(anchorPosition + backward * distance)
                || TryMoveToClearPosition(anchorPosition + perpendicular * distance)
                || TryMoveToClearPosition(anchorPosition - perpendicular * distance)
                || TryMoveToClearPosition(anchorPosition + (backward + perpendicular).normalized * distance)
                || TryMoveToClearPosition(anchorPosition + (backward - perpendicular).normalized * distance)
                || TryMoveToClearPosition(anchorPosition - backward * distance))
            {
                return true;
            }
        }

        body.position = originalPosition;
        Physics2D.SyncTransforms();
        return false;
    }

    private bool TryMoveToClearPosition(Vector2 position)
    {
        body.position = position;
        Physics2D.SyncTransforms();

        if (!boatBoundary.IsPlayerClear(playerCollider))
        {
            return false;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(Physics2D.AllLayers);
        overlappingColliders.Clear();
        playerCollider.Overlap(filter, overlappingColliders);

        foreach (Collider2D otherCollider in overlappingColliders)
        {
            if (otherCollider == playerCollider
                || Physics2D.GetIgnoreLayerCollision(playerCollider.gameObject.layer, otherCollider.gameObject.layer)
                || Physics2D.GetIgnoreCollision(playerCollider, otherCollider))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private float GetMovementSpeed(Vector2 position)
    {
        return IsDeckPosition(position) ? boatMoveSpeed : waterMoveSpeed;
    }

    private bool IsDeckPosition(Vector2 position)
    {
        Vector3Int cell = boatTilemap.WorldToCell(position);
        return boatTilemap.HasTile(cell) || (boatTileDamage != null && boatTileDamage.IsBroken(cell));
    }

    private void RestoreBoundaryCollision()
    {
        if (playerCollider != null && boatBoundary != null)
        {
            boatBoundary.SetPlayerCollisionIgnored(playerCollider, false);
        }
    }

    private void UpdateJumpVisual()
    {
        if (visual == null)
        {
            return;
        }

        if (!isJumping)
        {
            visual.localScale = visualBaseScale;
            return;
        }

        float progress = Mathf.Clamp01(jumpElapsed / jumpDuration);
        float scale = 1f + (jumpPeakScale - 1f) * Mathf.Sin(Mathf.PI * progress);
        visual.localScale = visualBaseScale * scale;
    }
}
