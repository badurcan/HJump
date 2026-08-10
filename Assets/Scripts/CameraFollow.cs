using UnityEngine;

// Follows the ball straight down in world space. X/Z and rotation stay fixed
// so the tower and environment never move - only the camera (and the ball) descend.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothTime = 0.25f;
    [SerializeField] private float maxDropSpeed = 60f;

    private float fixedX;
    private float fixedZ;
    private float yOffset;
    private float velocityY;

    private void Start()
    {
        fixedX = transform.position.x;
        fixedZ = transform.position.z;
        if (target != null)
        {
            yOffset = transform.position.y - target.position.y;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float desiredY = target.position.y + yOffset;
        float smoothedY = Mathf.SmoothDamp(transform.position.y, desiredY, ref velocityY, smoothTime, maxDropSpeed);
        transform.position = new Vector3(fixedX, smoothedY, fixedZ);
    }
}
