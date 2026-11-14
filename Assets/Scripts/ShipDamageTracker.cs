using System;
using UnityEngine;

public sealed class ShipDamageTracker : MonoBehaviour
{
    [SerializeField] private bool useDemoValues = true;
    [SerializeField, Min(1)] private int demoTotalRepairableTiles = 100;
    [SerializeField, Min(0)] private int demoMissingTiles = 25;

    private int totalRepairableTiles;
    private int missingTiles;

    public event Action DamageChanged;
    public event Action GameOver;

    public int TotalRepairableTiles => totalRepairableTiles;
    public int MissingTiles => missingTiles;
    public int SinkingTileThreshold => Mathf.CeilToInt(totalRepairableTiles * 0.75f);
    public float DamageFraction => totalRepairableTiles == 0 ? 0f : (float)missingTiles / totalRepairableTiles;
    public bool IsSinking => totalRepairableTiles > 0 && missingTiles >= SinkingTileThreshold;
    public bool IsFinalTileWarning => totalRepairableTiles > 0
        && !IsSinking
        && missingTiles + 1 >= SinkingTileThreshold;

    private void Awake()
    {
        if (useDemoValues)
        {
            SetTileCountsInternal(demoTotalRepairableTiles, demoMissingTiles, notify: false);
        }
    }

    /// <summary>
    /// Replaces the authoritative repairable and missing tile counts.
    /// Call after a tile breaks or is repaired; missingTiles must be between zero and total.
    /// Calling this method disables the temporary Inspector demo values.
    /// </summary>
    public void SetTileCounts(int total, int missing)
    {
        if (total < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(total), "Total repairable tiles must be at least one.");
        }

        if (missing < 0 || missing > total)
        {
            throw new ArgumentOutOfRangeException(nameof(missing), "Missing tiles must be between zero and the total.");
        }

        useDemoValues = false;
        bool wasSinking = IsSinking;
        bool changed = totalRepairableTiles != total || missingTiles != missing;
        totalRepairableTiles = total;
        missingTiles = missing;

        if (changed)
        {
            DamageChanged?.Invoke();
        }

        if (!wasSinking && IsSinking)
        {
            GameOver?.Invoke();
        }
    }

    private void SetTileCountsInternal(int total, int missing, bool notify)
    {
        if (total < 1)
        {
            Debug.LogError("ShipDamageTracker demo total must be at least one.", this);
            totalRepairableTiles = 1;
            missingTiles = 0;
            return;
        }

        totalRepairableTiles = total;
        missingTiles = Mathf.Clamp(missing, 0, total);

        if (notify)
        {
            DamageChanged?.Invoke();
        }
    }
}
