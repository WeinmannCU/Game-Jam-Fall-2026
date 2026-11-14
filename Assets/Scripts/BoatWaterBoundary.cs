using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D), typeof(TilemapCollider2D), typeof(CompositeCollider2D))]
public sealed class BoatWaterBoundary : MonoBehaviour
{
    private readonly HashSet<Collider2D> ignoredPlayers = new HashSet<Collider2D>();

    private CompositeCollider2D boundaryCollider;

    private void Awake()
    {
        boundaryCollider = GetComponent<CompositeCollider2D>();
    }

    public void SetPlayerCollisionIgnored(Collider2D playerCollider, bool ignored)
    {
        if (playerCollider == null || boundaryCollider == null)
        {
            Debug.LogError("BoatWaterBoundary needs a player collider and CompositeCollider2D.", this);
            return;
        }

        if (!ignored && Physics2D.GetIgnoreCollision(playerCollider, boundaryCollider)
            && !IsPlayerClear(playerCollider))
        {
            Debug.LogError("BoatWaterBoundary cannot restore collision while the player overlaps its boundary.", this);
            return;
        }

        Physics2D.IgnoreCollision(playerCollider, boundaryCollider, ignored);
        if (ignored)
        {
            ignoredPlayers.Add(playerCollider);
        }
        else
        {
            ignoredPlayers.Remove(playerCollider);
        }
    }

    public bool IsPlayerClear(Collider2D playerCollider)
    {
        if (playerCollider == null || boundaryCollider == null || !boundaryCollider.enabled)
        {
            Debug.LogError("BoatWaterBoundary cannot check clearance without enabled colliders.", this);
            return false;
        }

        ColliderDistance2D distance = Physics2D.Distance(playerCollider, boundaryCollider);
        if (!distance.isValid)
        {
            Debug.LogError("Unity could not calculate the player-to-boat-boundary distance.", this);
            return false;
        }

        return distance.distance >= 0f;
    }

    private void OnDisable()
    {
        foreach (Collider2D playerCollider in ignoredPlayers)
        {
            if (playerCollider != null && boundaryCollider != null)
            {
                Physics2D.IgnoreCollision(playerCollider, boundaryCollider, false);
            }
        }

        ignoredPlayers.Clear();
    }
}
