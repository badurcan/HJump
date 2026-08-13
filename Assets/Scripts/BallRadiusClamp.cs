using UnityEngine;

// Locks the ball to a straight vertical drop down the tower's central axis.
// Instead of a physical tube collider (extra body, and prone to corner-catching jitter
// against the rotating ring mesh colliders), this freezes X/Z directly on the Rigidbody
// via engine constraints - zero extra cost, zero drift, guaranteed straight line for the
// camera. The tower (obstacles) rotates around the ball via TowerController, which is what
// actually creates/moves the gap under a laterally-fixed ball.
[RequireComponent(typeof(Rigidbody))]
public class BallRadiusClamp : MonoBehaviour
{
    [Tooltip("World X/Z the ball drops straight down through. Leave at (0,0) to auto-lock onto the ball's own spawn X/Z instead (recommended - the tower has a solid decorative core cylinder running through the exact center axis, so (0,0) itself is usually NOT a safe drop line).")]
    [SerializeField] private Vector2 towerCenterXZ = Vector2.zero;

    [Tooltip("Also freeze rotation on X/Z so the ball doesn't tumble sideways from contact torque. Y rotation (spin) is left free.")]
    [SerializeField] private bool freezeTumble = true;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Snap onto the axis once at start, then let the constraint solver hold it there.
        // Using transform.position (not rb.position) here matters: at Awake() the physics
        // scene hasn't run its first step yet, and setting rb.position too early can get
        // silently overwritten by Unity's initial Transform->physics sync, leaving the ball
        // resting slightly off-axis exactly where it was originally placed.
        Vector3 pos = transform.position;
        Vector2 lockXZ = towerCenterXZ;
        if (lockXZ == Vector2.zero)
        {
            // Default: lock onto wherever the ball was actually placed, not the literal
            // world origin - the origin sits inside the tower's solid core cylinder.
            lockXZ = new Vector2(pos.x, pos.z);
        }
        transform.position = new Vector3(lockXZ.x, pos.y, lockXZ.y);
        Physics.SyncTransforms();

        RigidbodyConstraints constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
        if (freezeTumble)
        {
            constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }
        rb.constraints = constraints;

        // This ball must never auto-sleep: it's meant to be constantly falling/accelerating,
        // and PhysX excludes sleeping bodies from gravity entirely. Without this, any moment
        // of near-zero velocity (e.g. resting a beat against a ring) can put it to sleep and
        // gravity silently stops applying until something explicitly wakes it - looks exactly
        // like the ball "getting stuck" for no reason.
        rb.sleepThreshold = 0f;
    }
}
