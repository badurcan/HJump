using UnityEngine;

// Follows the ball straight down in world space. X/Z and rotation stay fixed
// so the tower and environment never move - only the camera (and the ball) descend.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Tooltip("If true, camera Y tracks the ball almost instantly (tiny smoothing window below) - reads as a hard lock during normal fast descent, but still absorbs single-frame physics pops (e.g. the harder-than-usual first landing) instead of visibly snapping. If false, falls back to the slower damped follow below.")]
    [SerializeField] private bool hardLock = true;
    [Tooltip("Smoothing window used when hardLock is on. Small enough to feel instant while falling, large enough to swallow a one-frame landing pop.")]
    [SerializeField] private float hardLockSmoothTime = 0.05f;

    [Header("Damped follow (only used when hardLock is off)")]
    [SerializeField] private float smoothTime = 0.25f;
    [SerializeField] private float maxDropSpeed = 60f;

    [Header("Shake (e.g. on obstacle break)")]
    [Tooltip("Perlin-noise based shake instead of pure random jitter - reads as a subtle rumble rather than a jarring snap.")]
    [SerializeField] private float defaultShakeDuration = 0.12f;
    [SerializeField] private float defaultShakeMagnitude = 0.05f;
    [SerializeField] private float shakeFrequency = 25f;

    private float fixedX;
    private float fixedZ;
    private float yOffset;
    
    private float recordLowY;
private float velocityY;

    private float shakeElapsed;
    private float shakeDuration;
    private float shakeMagnitude;
    private float shakeSeedX;
    private float shakeSeedZ;

private void Start()
    {
        fixedX = transform.position.x;
        fixedZ = transform.position.z;
        if (target != null)
        {
            yOffset = transform.position.y - target.position.y;
            recordLowY = target.position.y;
        }
    }

    // Call with no args for a subtle default rumble (e.g. from an obstacle break), or pass
    // explicit values for a bigger hit later (e.g. the final impact beat).
    public void Shake(float duration = -1f, float magnitude = -1f)
    {
        shakeDuration = duration > 0f ? duration : defaultShakeDuration;
        shakeMagnitude = magnitude > 0f ? magnitude : defaultShakeMagnitude;
        shakeElapsed = 0f;
        shakeSeedX = Random.value * 100f;
        shakeSeedZ = Random.value * 100f;
    }

private void LateUpdate()
    {
        if (target == null) return;

        // Only ever follow the ball's deepest point reached so far (its record-low Y).
        // The idle physical bounce (BallPhysicalBounce) makes the ball's raw Y oscillate
        // upward briefly on every hop - if the camera tracked that directly it would jitter
        // up and down with each bounce. Tracking the running minimum instead means the
        // camera only ever moves forward (down) as real progress down the tower happens,
        // and ignores the bounce entirely.
        if (target.position.y < recordLowY)
        {
            recordLowY = target.position.y;
        }

        float desiredY = recordLowY + yOffset;
        float baseY = Mathf.SmoothDamp(transform.position.y, desiredY, ref velocityY, hardLock ? hardLockSmoothTime : smoothTime, maxDropSpeed);

        Vector3 shakeOffset = Vector3.zero;
        if (shakeElapsed < shakeDuration)
        {
            float falloff = 1f - (shakeElapsed / shakeDuration);
            float noiseX = (Mathf.PerlinNoise(shakeSeedX, Time.time * shakeFrequency) - 0.5f) * 2f;
            float noiseZ = (Mathf.PerlinNoise(shakeSeedZ, Time.time * shakeFrequency) - 0.5f) * 2f;
            shakeOffset = new Vector3(noiseX, 0f, noiseZ) * shakeMagnitude * falloff;
            shakeElapsed += Time.deltaTime;
        }

        transform.position = new Vector3(fixedX, baseY, fixedZ) + shakeOffset;
    }
}
