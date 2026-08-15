using UnityEngine;

// Real Helix-Jump-style idle bounce: every time the ball lands on a platform/obstacle
// it gets a fixed upward Rigidbody velocity kick, so it physically leaves the surface
// and lands again - a genuine repeating bounce-in-place, not a cosmetic mesh animation.
// The kick fires once per landing (OnCollisionEnter), not via a per-frame velocity
// threshold check, so the height is exactly the same every single bounce - it doesn't
// depend on how hard the ball happened to hit. That also requires the ball's physic
// material to carry zero bounciness (BouncyBall.physicMaterial): any natural PhysX
// restitution on top of our own kick would stack unpredictably with it and produce a
// low/high/low alternating bounce depending on impact speed, exactly the problem this
// replaces.
// Gated off during the meteor state (MeteorObstacleBreaker wants the meteor to punch
// straight through, not hop).
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class BallPhysicalBounce : MonoBehaviour
{
    [Tooltip("Hop height as a multiple of the ball's world-space radius. 0.5 = hops half its own radius high.")]
    [SerializeField] private float hopHeightRatio = 1.6f;
    [Tooltip("Minimum time between landings before another hop can trigger. Guards against a single landing registering as multiple simultaneous collision contacts (e.g. touching two colliders at once) and double-kicking.")]
    [SerializeField] private float minHopInterval = 0.15f;
    [Tooltip("Leave empty to auto-find on this object. While IsMeteor is true, bouncing is suppressed.")]
    [SerializeField] private BallMeteorTransform meteorTransform;
    [Tooltip("Minimum upward-ness (contact normal dot world up) for a collision to count as a landing on top of a platform. Lower values (e.g. grazing the tower's central shaft while falling through a gap) are ignored so the ball keeps falling straight through instead of bouncing off the wall.")]
    [SerializeField] private float minUpwardNormal = 0.5f;
    [Tooltip("Spawned at the contact point (under the ball) on every bounce. Leave empty to skip.")]
    [SerializeField] private ParticleSystem bounceVfxPrefab;

    private Rigidbody rb;
    private SphereCollider sphereCollider;
    private float lastHopTime = -999f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sphereCollider = GetComponent<SphereCollider>();
        if (meteorTransform == null) meteorTransform = GetComponent<BallMeteorTransform>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (meteorTransform != null && meteorTransform.IsMeteor) return;
        if (Time.time - lastHopTime < minHopInterval) return;

        Vector3 avgNormal = Vector3.zero;
        Vector3 avgPoint = Vector3.zero;
        int count = collision.contactCount;
        for (int i = 0; i < count; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            avgNormal += contact.normal;
            avgPoint += contact.point;
        }
        if (count > 0)
        {
            avgNormal /= count;
            avgPoint /= count;
        }

        // Only bounce off something roughly floor-like (a platform top). A near-vertical
        // normal (e.g. brushing the tower's central shaft while falling through a gap)
        // means this isn't a landing - let the ball keep falling straight through.
        if (avgNormal.y < minUpwardNormal) return;

        float worldRadius = sphereCollider.radius * transform.lossyScale.x;
        float hopHeight = hopHeightRatio * worldRadius;
        float g = Mathf.Abs(Physics.gravity.y);
        float hopVelocity = Mathf.Sqrt(2f * g * Mathf.Max(hopHeight, 0.0001f));

        Vector3 v = rb.velocity;
        v.y = hopVelocity;
        rb.velocity = v;
        lastHopTime = Time.time;

        if (bounceVfxPrefab != null)
        {
            ParticleSystem vfx = Instantiate(bounceVfxPrefab, avgPoint, Quaternion.LookRotation(Vector3.up));
            Destroy(vfx.gameObject, vfx.main.duration + vfx.main.startLifetime.constantMax);
        }
    }
}
