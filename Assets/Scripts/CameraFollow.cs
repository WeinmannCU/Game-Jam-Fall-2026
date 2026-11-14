using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float smoothingTime = 0.2f;
    [SerializeField, Min(0f)] private float horizontalDeadZoneWidth = 1.5f;
    [SerializeField, Min(0f)] private float verticalDeadZoneHeight = 1.5f;
    [SerializeField] private float minCameraX = -8.22f;
    [SerializeField] private float maxCameraX = 10.22f;
    [SerializeField] private float minCameraY = -4.5f;
    [SerializeField] private float maxCameraY = 4.5f;

    private float horizontalVelocity;
    private float verticalVelocity;

    private void Awake()
    {
        if (target == null)
        {
            Debug.LogError("CameraFollow needs a target Transform.", this);
            enabled = false;
            return;
        }

        if (minCameraX > maxCameraX || minCameraY > maxCameraY)
        {
            Debug.LogError("CameraFollow minimum camera bounds must not exceed their maximum bounds.", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        Vector3 cameraPosition = transform.position;
        float horizontalOffset = target.position.x - cameraPosition.x;
        float halfHorizontalDeadZone = horizontalDeadZoneWidth * 0.5f;
        float desiredX = cameraPosition.x;

        if (Mathf.Abs(horizontalOffset) > halfHorizontalDeadZone)
        {
            desiredX = target.position.x - Mathf.Sign(horizontalOffset) * halfHorizontalDeadZone;
        }

        float offsetFromCenter = target.position.y - cameraPosition.y;
        float halfDeadZone = verticalDeadZoneHeight * 0.5f;
        float desiredY = cameraPosition.y;

        if (Mathf.Abs(offsetFromCenter) > halfDeadZone)
        {
            desiredY = target.position.y - Mathf.Sign(offsetFromCenter) * halfDeadZone;
        }

        desiredX = Mathf.Clamp(desiredX, minCameraX, maxCameraX);
        desiredY = Mathf.Clamp(desiredY, minCameraY, maxCameraY);
        cameraPosition.x = Mathf.Clamp(
            Mathf.SmoothDamp(cameraPosition.x, desiredX, ref horizontalVelocity, smoothingTime),
            minCameraX,
            maxCameraX);
        cameraPosition.y = Mathf.Clamp(
            Mathf.SmoothDamp(cameraPosition.y, desiredY, ref verticalVelocity, smoothingTime),
            minCameraY,
            maxCameraY);
        transform.position = cameraPosition;
    }
}
