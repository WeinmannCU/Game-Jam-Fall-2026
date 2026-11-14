using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class PlayerController : MonoBehaviour
{
    private const float SafePositionSearchStep = 0.025f;

    private readonly List<Collider2D> overlappingColliders = new List<Collider2D>();

    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Tilemap boatTilemap;
    [SerializeField] private BoatWaterBoundary boatBoundary;
    [SerializeField] private Transform visual;
    [SerializeField, Min(0f)] private float boatMoveSpeed = 4f;
    [SerializeField, Min(0f)] private float waterMoveSpeed = 2f;
    [SerializeField, Min(0.1f)] private float jumpDuration = 0.45f;
    [SerializeField, Min(1f)] private float jumpPeakScale = 1.3f;

    private Rigidbody2D body;
    private CircleCollider2D playerCollider;
    private InputActionAsset runtimeActions;
    private InputActionMap playerActionMap;
    private InputAction moveAction;
    private InputAction jumpAction;
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

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<CircleCollider2D>();

        if (inputActions == null || boatTilemap == null || boatBoundary == null || visual == null)
        {
            Debug.LogError(
                "PlayerController needs the Input Actions asset, Boat Tilemap, BoatWaterBoundary, and Visual child.",
                this);
            enabled = false;
            return;
        }

        runtimeActions = Instantiate(inputActions);
        playerActionMap = runtimeActions.FindActionMap("Player", throwIfNotFound: false);
        moveAction = playerActionMap?.FindAction("Move", throwIfNotFound: false);
        jumpAction = playerActionMap?.FindAction("Jump", throwIfNotFound: false);

        if (moveAction == null || jumpAction == null)
        {
            Debug.LogError("PlayerController needs the Player/Move and Player/Jump input actions.", this);
            enabled = false;
            return;
        }

        visualBaseScale = visual.localScale;
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

        UpdateJumpVisual();
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
                body.linearVelocity = Vector2.zero;
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
            body.linearVelocity = jumpDirection * jumpSpeed;
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

        body.linearVelocity = moveInput * GetMovementSpeed(body.position);
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
        Vector3Int boatCell = boatTilemap.WorldToCell(position);
        return boatTilemap.HasTile(boatCell) ? boatMoveSpeed : waterMoveSpeed;
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
