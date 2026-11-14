using System;
using UnityEngine;

public sealed class DriftwoodInventory : MonoBehaviour
{
    [SerializeField, Min(1)] private int capacity = 5;
    [SerializeField] private ShipDamageTracker damageTracker;

    public event Action InventoryChanged;

    public int WoodCount { get; private set; }
    public int Capacity => capacity;
    public bool IsFull => WoodCount >= capacity;

    private bool frozen;

    private void Awake()
    {
        damageTracker = damageTracker != null
            ? damageTracker
            : FindFirstObjectByType<ShipDamageTracker>();
    }

    private void OnEnable()
    {
        if (damageTracker != null)
        {
            damageTracker.GameOver += FreezeInventory;
        }
    }

    private void OnDisable()
    {
        if (damageTracker != null)
        {
            damageTracker.GameOver -= FreezeInventory;
        }
    }

    public bool TryAddWood()
    {
        if (frozen || (damageTracker != null && damageTracker.IsSinking) || WoodCount >= capacity)
        {
            return false;
        }

        WoodCount++;
        InventoryChanged?.Invoke();
        return true;
    }

    public bool TryConsumeWood()
    {
        if (frozen || (damageTracker != null && damageTracker.IsSinking) || WoodCount <= 0)
        {
            return false;
        }

        WoodCount--;
        InventoryChanged?.Invoke();
        return true;
    }

    public void ResetInventory()
    {
        frozen = false;
        WoodCount = 0;
        InventoryChanged?.Invoke();
    }

    private void FreezeInventory()
    {
        frozen = true;
    }
}
