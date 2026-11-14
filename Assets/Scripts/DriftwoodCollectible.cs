using UnityEngine;

public sealed class DriftwoodCollectible : MonoBehaviour
{
    private bool collected;

    public bool IsAvailable => !collected && isActiveAndEnabled && gameObject.activeInHierarchy;

    public bool TryCollect(DriftwoodInventory inventory)
    {
        if (collected || inventory == null || !inventory.TryAddWood())
        {
            return false;
        }

        collected = true;
        Destroy(gameObject);
        return true;
    }
}
