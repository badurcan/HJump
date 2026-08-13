using UnityEngine;

// Follows the ball straight down in world space. X/Z and rotation stay fixed
// so the tower and environment never move - only the camera (and the ball) descend.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Tooltip("If true, camera Y snaps exactly to the ball every frame - true lock-on, no lag even as the ball speeds up. If false, falls back to the damped follow below.")]
    [SerializeField] private bool hardLock = true;

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

        float desiredY = target.position.y + yOffset;
        float baseY = hardLock ? desiredY : Mathf.SmoothDamp(transform.position.y, desiredY, ref velocityY, smoothTime, maxDropSpeed);

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
