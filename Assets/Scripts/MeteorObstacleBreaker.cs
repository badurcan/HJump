using UnityEngine;

// Lives on the ball only. While the ball is a meteor (see BallMeteorTransform.IsMeteor),
// contact with an object tagged "Obstacle" spawns one shared break-VFX burst at the impact
// point and deactivates that obstacle so the meteor visibly smashes through it.
// This is the single listener for all ~100 ring segments - no per-obstacle scripts needed.
//
// Obstacle colliders stay solid (non-trigger) the whole time - PhysX does not support trigger
// mode on concave MeshColliders ("Triggers on concave MeshColliders are not supported"), and
// the rings have to stay concave (a convex hull of a ring would fill in the gap). So instead
// of removing the physical response, this preserves the ball's velocity across the hit: the
// velocity going into each physics step is cached in FixedUpdate, and if that step's contact
// broke an obstacle, the ball's velocity is restored to the pre-impact value right after -
// the one-step deceleration from the resolved contact never has a frame to be visible, so the
// meteor reads as smashing straight through without slowing down.
[RequireComponent(typeof(BallMeteorTransform))]
[RequireComponent(typeof(Rigidbody))]
public class MeteorObstacleBreaker : MonoBehaviour
{
    [Tooltip("Shared VFX prefab instantiated at the impact point on every obstacle break.")]
    [SerializeField] private GameObject breakVfxPrefab;

    [Tooltip("Camera to give a subtle shake to on every obstacle break.")]
    [SerializeField] private CameraFollow cameraShakeTarget;

    private BallMeteorTransform meteorState;
    private Rigidbody rb;
    private Vector3 preStepVelocity;

    private void Awake()
    {
        meteorState = GetComponent<BallMeteorTransform>();
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        // Snapshot the velocity BEFORE this step's physics resolves, so a same-step break
        // can restore exactly what the ball would have had without the contact.
        preStepVelocity = rb.velocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryBreak(collision.collider);
    }

    // OnCollisionEnter only fires on the first contact frame. If the ball becomes a meteor
    // a beat after contact already started (or briefly rests against a ring before the
    // transform flag flips), Enter alone can miss it. Stay keeps checking every frame the
    // contact persists, so a meteor can never get physically wedged against an unbroken ring.
    private void OnCollisionStay(Collision collision)
    {
        TryBreak(collision.collider);
    }

    private void TryBreak(Collider hitCollider)
    {
        if (!meteorState.IsMeteor) return;

        // collision.gameObject would report the tower root ("obs"), not the specific ring -
        // the rings are child colliders in a compound collider under the tower's Rigidbody.
        // The collider itself is the real leaf object that needs to be checked/broken.
        GameObject hitObject = hitCollider.gameObject;
        if (!hitObject.CompareTag("Obstacle")) return;
        if (!hitObject.activeInHierarchy) return;

        Vector3 contactPoint = hitCollider.ClosestPoint(transform.position);

        if (breakVfxPrefab != null)
        {
            GameObject vfx = Instantiate(breakVfxPrefab, contactPoint, Quaternion.identity);
            Destroy(vfx, 2f); // safety net in case the particle system isn't set to self-destroy
        }

        hitObject.SetActive(false);

        if (cameraShakeTarget != null)
        {
            cameraShakeTarget.Shake(); // subtle default rumble - see CameraFollow's defaultShakeDuration/Magnitude
        }

        // Undo this step's contact-induced deceleration so the meteor's fall speed never dips.
        rb.velocity = preStepVelocity;
        rb.WakeUp();
    }
}
