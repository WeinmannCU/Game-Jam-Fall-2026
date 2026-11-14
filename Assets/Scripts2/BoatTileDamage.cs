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
    [Tooltip("The player's position, or a child Transform at their feet.")]
    [SerializeField] private Transform player;

    [Header("Damage")]
    [SerializeField] private TileBase[] brokenTiles = new TileBase[5];
    [Min(0.1f)]
    [SerializeField] private float breakInterval = 5f;

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

    private void Start()
    {
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
            DisableWithError("The assigned floor tilemap has no tiles.");
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= Mathf.Max(0.1f, breakInterval))
        {
            timer = 0f;
            BreakRandomTile();
        }

        if (RepairPressed())
            TryRepairNearby();
    }

    public bool IsBroken(Vector3Int cell)
    {
        return originals.ContainsKey(cell);
    }

    private void BreakRandomTile()
    {
        if (intactCells.Count == 0)
            return;

        int index = Random.Range(0, intactCells.Count);
        Vector3Int cell = intactCells[index];
        TileBase original = floorTilemap.GetTile(cell);
        intactCells.RemoveAt(index);

        if (original == null)
            return;

        originals.Add(cell, original);
        floorTilemap.SetTile(cell, null);
        brokenTilemap.SetTile(cell, brokenTiles[Random.Range(0, brokenTiles.Length)]);
    }

    // Returns true only when this press actually fixes a tile.
    public bool TryRepairNearby()
    {
        if (!isActiveAndEnabled || player == null || floorTilemap == null)
            return false;

        Vector3Int playerCell = floorTilemap.WorldToCell(player.position);
        Vector3Int target = default(Vector3Int);
        float nearestDistance = float.PositiveInfinity;
        bool found = false;

        foreach (Vector3Int offset in repairOffsets)
        {
            Vector3Int cell = playerCell + offset;
            if (!IsBroken(cell))
                continue;

            Vector2 difference = floorTilemap.GetCellCenterWorld(cell) - player.position;
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
        return true;
    }

    private bool RepairPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.E);
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
