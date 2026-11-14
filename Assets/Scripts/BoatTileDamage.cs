using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BoatTileDamage : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap brokenTilemap;
    [Tooltip("The shared tracker used by the ship's damage UI and game-over logic.")]
    [SerializeField] private ShipDamageTracker damageTracker;
    [Tooltip("Player 1's position, or a child Transform at their feet.")]
    [SerializeField] private Transform player;
    [Tooltip("Player 2's position, or a child Transform at their feet.")]
    [SerializeField] private Transform player2;

    [Header("Repair keys")]
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private Key player1RepairKey = Key.E;
    [SerializeField] private Key player2RepairKey = Key.RightShift;
#else
    [SerializeField] private KeyCode player1RepairKey = KeyCode.E;
    [SerializeField] private KeyCode player2RepairKey = KeyCode.RightShift;
#endif

    [Header("Damage")]
    [SerializeField] private TileBase[] brokenTiles = new TileBase[5];
    [Min(0.1f)]
    [SerializeField] private float breakInterval = 5f;
    [Tooltip("Percentage of breakable tiles randomly damaged when the scene starts. Applied once in Start.")]
    [Range(0f, 100f)]
    [SerializeField] private float startingDamagePercent = 0f;

    // Only intact floor cells can be selected for new damage.
    private readonly List<Vector3Int> intactCells = new List<Vector3Int>();

    // A cell in this dictionary is broken. Its value is its original tile.
    private readonly Dictionary<Vector3Int, TileBase> originals =
        new Dictionary<Vector3Int, TileBase>();

    private static readonly Vector3Int[] repairOffsets =
    {
        Vector3Int.zero, // Also allow repair while standing on the tile.
        Vector3Int.up,
        Vector3Int.down,
        Vector3Int.left,
        Vector3Int.right
    };

    private float timer;
    private int totalRepairableTiles;
    private bool gameOver;

    private void OnEnable()
    {
        if (damageTracker != null)
        {
            damageTracker.GameOver += HandleGameOver;
        }
    }

    private void OnDisable()
    {
        if (damageTracker != null)
        {
            damageTracker.GameOver -= HandleGameOver;
        }
    }

    private void Start()
    {
        if (damageTracker == null)
        {
            DisableWithError("Assign the scene's ShipDamageTracker to Damage Tracker.");
            return;
        }

        if (floorTilemap == null || brokenTilemap == null || player == null ||
            floorTilemap == brokenTilemap || brokenTiles == null ||
            brokenTiles.Length != 5)
        {
            DisableWithError("Assign two different tilemaps, the player, and five broken Tile assets.");
            return;
        }

        foreach (TileBase tile in brokenTiles)
        {
            if (tile == null)
            {
                DisableWithError("Fill all five Broken Tiles slots with Tile assets.");
                return;
            }
        }

        // Start with an empty broken tilemap so every damaged cell is tracked.
        foreach (Vector3Int cell in brokenTilemap.cellBounds.allPositionsWithin)
        {
            if (brokenTilemap.HasTile(cell))
            {
                DisableWithError("Erase painted tiles from the broken tilemap before playing.");
                return;
            }
        }

        foreach (Vector3Int cell in floorTilemap.cellBounds.allPositionsWithin)
        {
            if (floorTilemap.HasTile(cell))
                intactCells.Add(cell);
        }

        if (intactCells.Count == 0)
        {
            DisableWithError("The assigned floor tilemap has no tiles.");
            return;
        }

        // Count the painted breakable cells once, before any are removed.
        totalRepairableTiles = intactCells.Count;

        int startingBrokenCount = Mathf.RoundToInt(
            totalRepairableTiles * Mathf.Clamp(startingDamagePercent, 0f, 100f) / 100f);

        // Initialize the visuals before publishing the final starting counts.
        for (int i = 0; i < startingBrokenCount; i++)
            BreakRandomTile(notifyTracker: false);

        SyncDamageTracker();
    }

    private void HandleGameOver()
    {
        gameOver = true;
    }

    private void Update()
    {
        if (gameOver)
        {
            return;
        }

        timer += Time.deltaTime;

        if (timer >= Mathf.Max(0.1f, breakInterval))
        {
            timer = 0f;
            BreakRandomTile();
        }

        if (RepairPressed(false))
            TryRepairNearby(player);

        if (player2 != null && RepairPressed(true))
            TryRepairNearby(player2);
    }

    public bool IsBroken(Vector3Int cell)
    {
        return originals.ContainsKey(cell);
    }

    private void BreakRandomTile(bool notifyTracker = true)
    {
        if (intactCells.Count == 0)
            return;

        if (notifyTracker && damageTracker != null
            && originals.Count + 1 >= damageTracker.SinkingTileThreshold)
        {
            while (intactCells.Count > 0)
            {
                BreakRandomTile(notifyTracker: false);
            }

            SyncDamageTracker();
            return;
        }

        int index = Random.Range(0, intactCells.Count);
        Vector3Int cell = intactCells[index];
        TileBase original = floorTilemap.GetTile(cell);
        intactCells.RemoveAt(index);

        if (original == null)
            return;

        originals.Add(cell, original);
        floorTilemap.SetTile(cell, null);
        brokenTilemap.SetTile(cell, brokenTiles[Random.Range(0, brokenTiles.Length)]);
        if (notifyTracker)
            SyncDamageTracker();
    }

    // Returns true only when this press actually fixes a tile.
    public bool TryRepairNearby()
    {
        return TryRepairNearby(player);
    }

    // Each player repairs using their own position and the same damage state.
    public bool TryRepairNearby(Transform repairPlayer)
    {
        if (gameOver || (damageTracker != null && damageTracker.IsSinking)
            || !isActiveAndEnabled || repairPlayer == null || floorTilemap == null ||
            !repairPlayer.gameObject.activeInHierarchy)
            return false;

        Vector3Int playerCell = floorTilemap.WorldToCell(repairPlayer.position);
        Vector3Int target = default(Vector3Int);
        float nearestDistance = float.PositiveInfinity;
        bool found = false;

        foreach (Vector3Int offset in repairOffsets)
        {
            Vector3Int cell = playerCell + offset;
            if (!IsBroken(cell))
                continue;

            Vector2 difference = floorTilemap.GetCellCenterWorld(cell) - repairPlayer.position;
            float distance = difference.sqrMagnitude;
            if (distance < nearestDistance)
            {
                target = cell;
                nearestDistance = distance;
                found = true;
            }
        }

        if (!found)
            return false;

        floorTilemap.SetTile(target, originals[target]);
        brokenTilemap.SetTile(target, null);
        originals.Remove(target);
        intactCells.Add(target); // A repaired tile can break again later.
        SyncDamageTracker();
        return true;
    }

    private void SyncDamageTracker()
    {
        // "Missing" in the tracker means currently broken in this system.
        damageTracker.SetTileCounts(totalRepairableTiles, originals.Count);
    }

    private bool RepairPressed(bool secondPlayer)
    {
#if ENABLE_INPUT_SYSTEM
        Key key = secondPlayer ? player2RepairKey : player1RepairKey;
        return key != Key.None && Keyboard.current != null &&
            Keyboard.current[key].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        KeyCode key = secondPlayer ? player2RepairKey : player1RepairKey;
        return Input.GetKeyDown(key);
#else
        return false;
#endif
    }

    private void DisableWithError(string message)
    {
        Debug.LogError("BoatTileDamage: " + message, this);
        enabled = false;
    }
}
