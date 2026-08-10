using UnityEngine;

// Rotates the tower (obstacle rings) around its own Y axis based on player input.
// The tower spins in place; it never translates, and the camera/ball are unaffected by it.
// Rotation goes through the kinematic Rigidbody so PhysX registers it as a moving body -
// without a Rigidbody, a rotating MeshCollider does not wake up sleeping Rigidbodies
// (e.g. a ball resting on it), so the ball would stay stuck even after a gap opens under it.
[RequireComponent(typeof(Rigidbody))]
public class TowerController : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 140f; // degrees per second

    private Rigidbody rb;
    private float pendingInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void Update()
    {
        pendingInput = 0f;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) pendingInput -= 1f;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) pendingInput += 1f;
    }

    private void FixedUpdate()
    {
        if (pendingInput == 0f) return;

        float deltaAngle = pendingInput * rotationSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.AngleAxis(deltaAngle, Vector3.up));
    }
}
