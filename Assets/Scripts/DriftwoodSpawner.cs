using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class DriftwoodSpawner : MonoBehaviour
{
    private enum SpawnSide
    {
        Left,
        Right,
        Bottom,
        Top
    }

    [Header("Scene references")]
    [SerializeField] private Tilemap oceanTilemap;
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap brokenTilemap;
    [SerializeField] private Tilemap wallsTilemap;
    [SerializeField] private Tilemap hullTilemap;
    [SerializeField] private ShipDamageTracker damageTracker;
    [SerializeField] private LocalGameMode gameMode;
    [SerializeField] private Tile driftwoodTile;

    [Header("Spawning")]
    [SerializeField, Min(0f)] private float minimumSpawnDistance = 2f;
    [SerializeField, Min(0f)] private float maximumSpawnDistance = 5f;
    [SerializeField, Min(0.05f)] private float collectibleRadius = 0.24f;
    [SerializeField, Min(0f)] private float positionJitter = 0.2f;

    private readonly List<DriftwoodCollectible> activeCollectibles = new List<DriftwoodCollectible>();
    private readonly List<Vector3Int> candidateCells = new List<Vector3Int>();
    private readonly List<Vector3Int>[] candidateCellsBySide =
    {
        new List<Vector3Int>(),
        new List<Vector3Int>(),
        new List<Vector3Int>(),
        new List<Vector3Int>()
    };
    private Bounds boatBounds;
    private DriftwoodInventory playerOneInventory;
    private DriftwoodInventory playerTwoInventory;
    private bool supplyNeedsBalancing = true;
    private bool warnedNoSpawnPosition;
    private bool stopped;

    private void Awake()
    {
        ResolveReferences();
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        if (!CacheBoatBounds())
        {
            enabled = false;
            return;
        }

        CacheCandidateCells();
    }

    private void OnEnable()
    {
        if (damageTracker != null)
        {
            damageTracker.DamageChanged += MarkSupplyForBalancing;
            damageTracker.GameOver += StopSpawning;
        }

        if (playerOneInventory != null)
        {
            playerOneInventory.InventoryChanged += MarkSupplyForBalancing;
        }

        if (playerTwoInventory != null)
        {
            playerTwoInventory.InventoryChanged += MarkSupplyForBalancing;
        }
    }

    private void OnDisable()
    {
        if (damageTracker != null)
        {
            damageTracker.DamageChanged -= MarkSupplyForBalancing;
            damageTracker.GameOver -= StopSpawning;
        }

        if (playerOneInventory != null)
        {
            playerOneInventory.InventoryChanged -= MarkSupplyForBalancing;
        }

        if (playerTwoInventory != null)
        {
            playerTwoInventory.InventoryChanged -= MarkSupplyForBalancing;
        }
    }

    private void LateUpdate()
    {
        if (stopped || !supplyNeedsBalancing)
        {
            return;
        }

        supplyNeedsBalancing = !BalanceSupply();
    }

    private void ResolveReferences()
    {
        oceanTilemap = oceanTilemap != null
            ? oceanTilemap
            : FindTilemap("Grid/Ocean");
        floorTilemap = floorTilemap != null
            ? floorTilemap
            : FindTilemap("Grid/Broken tiles");
        brokenTilemap = brokenTilemap != null
            ? brokenTilemap
            : FindTilemap("Grid/Play area");
        wallsTilemap = wallsTilemap != null
            ? wallsTilemap
            : FindTilemap("Grid/Walls");
        hullTilemap = hullTilemap != null
            ? hullTilemap
            : FindTilemap("Grid/Boat");
        damageTracker = damageTracker != null
            ? damageTracker
            : FindFirstObjectByType<ShipDamageTracker>();
        gameMode = gameMode != null
            ? gameMode
            : FindFirstObjectByType<LocalGameMode>();

        playerOneInventory = gameMode != null && gameMode.PlayerOne != null
            ? gameMode.PlayerOne.Inventory
            : null;
        playerTwoInventory = gameMode != null && gameMode.PlayerTwo != null
            ? gameMode.PlayerTwo.Inventory
            : null;
    }

    private bool ValidateReferences()
    {
        if (oceanTilemap == null || floorTilemap == null || brokenTilemap == null ||
            wallsTilemap == null || hullTilemap == null || damageTracker == null ||
            gameMode == null || playerOneInventory == null ||
            (gameMode.TwoPlayerMode && playerTwoInventory == null) || driftwoodTile == null)
        {
            Debug.LogError(
                "DriftwoodSpawner needs the boat/ocean tilemaps, damage tracker, game mode, active player inventories, and driftwood tile.",
                this);
            return false;
        }

        if (maximumSpawnDistance < minimumSpawnDistance)
        {
            Debug.LogError("DriftwoodSpawner maximum spawn distance must be at least the minimum distance.", this);
            return false;
        }

        return true;
    }

    private bool CacheBoatBounds()
    {
        bool hasBounds = false;
        IncludeTilemapBounds(floorTilemap, ref hasBounds);
        IncludeTilemapBounds(wallsTilemap, ref hasBounds);
        IncludeTilemapBounds(hullTilemap, ref hasBounds);

        if (!hasBounds)
        {
            Debug.LogError("DriftwoodSpawner could not find boat tiles to define the ocean spawn area.", this);
            return false;
        }

        return true;
    }

    private void IncludeTilemapBounds(Tilemap tilemap, ref bool hasBounds)
    {
        foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(cell))
            {
                continue;
            }

            Vector3 center = tilemap.GetCellCenterWorld(cell);
            if (!hasBounds)
            {
                boatBounds = new Bounds(center, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                boatBounds.Encapsulate(center);
            }
        }
    }

    private void CacheCandidateCells()
    {
        foreach (Vector3Int cell in oceanTilemap.cellBounds.allPositionsWithin)
        {
            if (IsBoatCell(cell) || !oceanTilemap.HasTile(cell))
            {
                continue;
            }

            Vector3 position = oceanTilemap.GetCellCenterWorld(cell);
            float distance = DistanceToBoat(position);
            if (distance >= minimumSpawnDistance && distance <= maximumSpawnDistance)
            {
                candidateCells.Add(cell);
                candidateCellsBySide[(int)GetSpawnSide(position)].Add(cell);
            }
        }

        if (candidateCells.Count == 0)
        {
            Debug.LogError("DriftwoodSpawner found no ocean cells within its configured spawn range.", this);
        }
    }

    private bool IsBoatCell(Vector3Int cell)
    {
        return floorTilemap.HasTile(cell) ||
            brokenTilemap.HasTile(cell) ||
            wallsTilemap.HasTile(cell) ||
            hullTilemap.HasTile(cell);
    }

    private float DistanceToBoat(Vector3 position)
    {
        float dx = Mathf.Max(boatBounds.min.x - position.x, 0f, position.x - boatBounds.max.x);
        float dy = Mathf.Max(boatBounds.min.y - position.y, 0f, position.y - boatBounds.max.y);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private SpawnSide GetSpawnSide(Vector3 position)
    {
        float distanceFromHorizontalEdge = position.x < boatBounds.min.x
            ? boatBounds.min.x - position.x
            : position.x > boatBounds.max.x
                ? position.x - boatBounds.max.x
                : 0f;
        float distanceFromVerticalEdge = position.y < boatBounds.min.y
            ? boatBounds.min.y - position.y
            : position.y > boatBounds.max.y
                ? position.y - boatBounds.max.y
                : 0f;

        if (distanceFromHorizontalEdge >= distanceFromVerticalEdge)
        {
            return position.x < boatBounds.min.x ? SpawnSide.Left : SpawnSide.Right;
        }

        return position.y < boatBounds.min.y ? SpawnSide.Bottom : SpawnSide.Top;
    }

    private bool BalanceSupply()
    {
        if (damageTracker.IsSinking)
        {
            StopSpawning();
            return true;
        }

        activeCollectibles.RemoveAll(collectible => collectible == null);
        RemoveInaccessibleCollectibles();

        int carriedWood = playerOneInventory.WoodCount;
        if (gameMode.TwoPlayerMode)
        {
            carriedWood += playerTwoInventory.WoodCount;
        }

        int missingSupplies = damageTracker.MissingTiles - carriedWood - activeCollectibles.Count;
        while (missingSupplies > 0)
        {
            if (!SpawnCollectible())
            {
                if (!warnedNoSpawnPosition)
                {
                    Debug.LogError(
                        "DriftwoodSpawner cannot fulfill the broken-plank supply requirement: no reachable, unoccupied ocean spawn position is available.",
                        this);
                    warnedNoSpawnPosition = true;
                }

                return false;
            }

            warnedNoSpawnPosition = false;
            missingSupplies--;
        }

        return true;
    }

    private void RemoveInaccessibleCollectibles()
    {
        for (int i = activeCollectibles.Count - 1; i >= 0; i--)
        {
            DriftwoodCollectible collectible = activeCollectibles[i];
            if (collectible == null || !collectible.IsAvailable)
            {
                activeCollectibles.RemoveAt(i);
                continue;
            }

            Vector3 position = collectible.transform.position;
            Vector3Int cell = oceanTilemap.WorldToCell(position);
            if (!oceanTilemap.HasTile(cell) || IsBoatCell(cell) || IsInsideSolidCollider(position))
            {
                Destroy(collectible.gameObject);
                activeCollectibles.RemoveAt(i);
            }
        }
    }

    private bool SpawnCollectible()
    {
        if (candidateCells.Count == 0)
        {
            return false;
        }

        int[] activeBySide = new int[candidateCellsBySide.Length];
        foreach (DriftwoodCollectible collectible in activeCollectibles)
        {
            if (collectible != null)
            {
                activeBySide[(int)GetSpawnSide(collectible.transform.position)]++;
            }
        }

        List<int> sideOrder = new List<int>();
        for (int side = 0; side < candidateCellsBySide.Length; side++)
        {
            if (candidateCellsBySide[side].Count > 0)
            {
                sideOrder.Add(side);
            }
        }

        for (int i = sideOrder.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (sideOrder[i], sideOrder[swapIndex]) = (sideOrder[swapIndex], sideOrder[i]);
        }

        sideOrder.Sort((first, second) => activeBySide[first].CompareTo(activeBySide[second]));

        foreach (int side in sideOrder)
        {
            List<Vector3Int> sideCells = candidateCellsBySide[side];
            int firstCandidate = Random.Range(0, sideCells.Count);
            for (int attempt = 0; attempt < sideCells.Count; attempt++)
            {
                Vector3Int cell = sideCells[(firstCandidate + attempt) % sideCells.Count];
                Vector3 position = oceanTilemap.GetCellCenterWorld(cell);
                position += new Vector3(
                    Random.Range(-positionJitter, positionJitter),
                    Random.Range(-positionJitter, positionJitter),
                    0f);

                if (HasNearbyCollectible(position) || IsInsideSolidCollider(position))
                {
                    continue;
                }

                CreateCollectible(position);
                return true;
            }
        }

        return false;
    }

    private bool HasNearbyCollectible(Vector3 position)
    {
        float spacing = collectibleRadius * 2f;
        foreach (DriftwoodCollectible collectible in activeCollectibles)
        {
            if (collectible != null &&
                Vector2.Distance(position, collectible.transform.position) < spacing)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsInsideSolidCollider(Vector3 position)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(position, collectibleRadius);
        foreach (Collider2D collider in colliders)
        {
            if (collider.isTrigger || collider.GetComponentInParent<PlayerController>() != null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void CreateCollectible(Vector3 position)
    {
        GameObject collectibleObject = new GameObject("Floating Driftwood");
        collectibleObject.transform.position = position;
        collectibleObject.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0, 4) * 90f);

        SpriteRenderer spriteRenderer = collectibleObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = driftwoodTile.sprite;
        spriteRenderer.sortingOrder = 1;

        CircleCollider2D circleCollider = collectibleObject.AddComponent<CircleCollider2D>();
        circleCollider.isTrigger = true;
        circleCollider.radius = collectibleRadius;

        activeCollectibles.Add(collectibleObject.AddComponent<DriftwoodCollectible>());
    }

    private void MarkSupplyForBalancing()
    {
        supplyNeedsBalancing = true;
    }

    private void StopSpawning()
    {
        stopped = true;
        supplyNeedsBalancing = false;
        foreach (DriftwoodCollectible collectible in activeCollectibles)
        {
            if (collectible != null)
            {
                Destroy(collectible.gameObject);
            }
        }

        activeCollectibles.Clear();
    }

    private static Tilemap FindTilemap(string path)
    {
        return GameObject.Find(path)?.GetComponent<Tilemap>();
    }
}
