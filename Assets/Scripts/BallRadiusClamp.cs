using UnityEngine;

// Keeps the ball within a fixed horizontal distance of the tower's central axis so it
// can never fly off the structure through a gap edge. Only the X/Z position is clamped
// (Y/falling is untouched) and only the outward radial component of velocity is removed,
// so bouncing and left/right sliding along the ring still feel physical.
[RequireComponent(typeof(Rigidbody))]
public class BallRadiusClamp : MonoBehaviour
{
    [SerializeField] private Vector2 towerCenterXZ = Vector2.zero;
    [SerializeField] private float maxRadius = 0.68f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        Vector3 pos = rb.position;
        Vector2 offset = new Vector2(pos.x - towerCenterXZ.x, pos.z - towerCenterXZ.y);
        float dist = offset.magnitude;

        if (dist <= maxRadius || dist < 0.0001f) return;

        Vector2 dir = offset / dist;

        // Pull the position back onto the radius, keep Y (falling) untouched.
        Vector2 clampedXZ = towerCenterXZ + dir * maxRadius;
        rb.position = new Vector3(clampedXZ.x, pos.y, clampedXZ.y);

        // Strip only the outward-pointing part of the horizontal velocity so bounce/left-right motion continues.
        Vector3 vel = rb.velocity;
        Vector2 velXZ = new Vector2(vel.x, vel.z);
        float outward = Vector2.Dot(velXZ, dir);
        if (outward > 0f)
        {
            Vector2 velXZClamped = velXZ - dir * outward;
            rb.velocity = new Vector3(velXZClamped.x, vel.y, velXZClamped.y);
        }
    }
}
