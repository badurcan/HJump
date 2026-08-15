using System.Collections;
using UnityEngine;

// Lives on the stage-end ground plane (e.g. "Ground_03"). When the meteor ball hits it,
// runs a one-shot impact beat: optional VFX, a bigger camera shake than the per-obstacle
// rumble, an optional brief slow-motion dip, then a fade to white and back - after which the
// ball reverts to normal and this object deactivates so the ball keeps falling under gravity
// where it used to be blocked.
//
// FUTURE: the SetActive(false) at the end of the sequence is a placeholder for "move on to
// the next stage" - this is the hook point where real stage-2 content (a new obstacle tower,
// a new StageEndTrigger further down, etc.) would be spawned/activated instead of just
// clearing the way.
//
// Deliberately left Untagged (not "Obstacle") so MeteorObstacleBreaker and
// MeteorGateController - both of which filter on that tag - never see this as a regular
// obstacle. This component itself is the marker.
public class StageEndTrigger : MonoBehaviour
{
    [Tooltip("Optional VFX spawned at the impact point. Leave empty to skip gracefully.")]
    [SerializeField] private GameObject impactVfxPrefab;
    [Tooltip("Euler rotation the impact VFX is spawned with (its particle systems are authored facing a particular local axis, so this orients the burst correctly against the impact surface).")]
    [SerializeField] private Vector3 impactVfxRotation = new Vector3(-90f, 0f, 0f);
    [Tooltip("World-space Y offset applied on top of the contact point, so the VFX plays a bit above the impact surface instead of exactly on it.")]
    [SerializeField] private float impactVfxHeightOffset = 0.1f;

    [SerializeField] private CameraFollow cameraShakeTarget;
    [Tooltip("Bigger than the per-obstacle default (0.12s / 0.05 magnitude) for a stage-ending impact beat.")]
    [SerializeField] private float impactShakeDuration = 0.4f;
    [SerializeField] private float impactShakeMagnitude = 0.3f;
    [SerializeField] private float shakeFrequency = 25f;

    [Header("Camera hold/resume")]
    [Tooltip("World-space camera position held (in place of the normal ball-chase) for the whole impact/shake/fade beat, so the impact reads as a framed shot instead of the camera drifting with the still-falling ball.")]
    [SerializeField] private Vector3 cameraHoldPosition = new Vector3(-0.0548f, 44.39f, -2.969f);
    [Tooltip("World-space camera position it snaps to while the screen is fully white (hidden from the player), right before resuming normal ball-chase tracking as the fade back in starts.")]
    [SerializeField] private Vector3 cameraResumePosition = new Vector3(0f, 37.95f, -2.92f);

    [Header("Slow motion (optional)")]
    [SerializeField] private bool useSlowMotion = true;
    [SerializeField] private float slowMotionScale = 0.25f;
    [Tooltip("Real-world seconds (unaffected by the timescale dip itself) to hold the slow-motion beat.")]
    [SerializeField] private float slowMotionRealtimeDuration = 0.5f;

    [Header("Fade")]
    [SerializeField] private ScreenFader screenFader;
    [Tooltip("Extra real-time pause after the shake/slow-motion beat, before the fade to white starts.")]
    [SerializeField] private float preFadeDelay = 0.3f;
    [SerializeField] private float fadeOutTime = 0.15f;
    [SerializeField] private float fadeHoldTime = 0.6f;
    [SerializeField] private float fadeInTime = 0.3f;

    [Header("Ball")]
    [SerializeField] private BallMeteorTransform meteorTransform;

    private bool triggered;

    private void OnCollisionEnter(Collision collision)
    {
        TryTriggerSequence(collision);
    }

    // Same Enter+Stay reasoning as MeteorObstacleBreaker: a same-frame meteor transform
    // right at first contact could otherwise be missed by Enter alone.
    private void OnCollisionStay(Collision collision)
    {
        TryTriggerSequence(collision);
    }

    private void TryTriggerSequence(Collision collision)
    {
        if (triggered) return;
        if (meteorTransform == null || !meteorTransform.IsMeteor) return;

        // Guard set synchronously (before the coroutine even starts) so a same-tick Stay
        // call can't re-enter mid-sequence.
        triggered = true;

        Vector3 contactPoint = collision.GetContact(0).point;
        StartCoroutine(ImpactSequence(contactPoint));
    }

    private IEnumerator ImpactSequence(Vector3 contactPoint)
    {
        if (impactVfxPrefab != null)
        {
            Vector3 vfxPosition = contactPoint + Vector3.up * impactVfxHeightOffset;
            GameObject vfx = Instantiate(impactVfxPrefab, vfxPosition, Quaternion.Euler(impactVfxRotation));
            Destroy(vfx, 3f); // safety net, matches MeteorObstacleBreaker's pattern
        }

        if (cameraShakeTarget != null)
        {
            // Hand camera control over from CameraFollow's per-frame ball-chase to a held framing
            // shot - the ball keeps falling under gravity through this whole sequence (unlike the
            // standalone VFX test scene, it's never frozen here), so leaving CameraFollow enabled
            // would have the camera drift down with it instead of holding the impact framing.
            // Our own shake (below) drives its own offset directly since CameraFollow.Shake()
            // needs its LateUpdate running to apply, which is off while this holds.
            cameraShakeTarget.enabled = false;
            cameraShakeTarget.transform.position = cameraHoldPosition;
            StartCoroutine(ShakeHeldCamera());
        }

        if (useSlowMotion)
        {
            Time.timeScale = slowMotionScale;
            yield return new WaitForSecondsRealtime(slowMotionRealtimeDuration);
            Time.timeScale = 1f;
        }

        if (preFadeDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(preFadeDelay);
        }

        if (screenFader != null)
        {
            yield return StartCoroutine(screenFader.FadeOut(fadeOutTime));

            // Screen is fully white here - hidden from the player, so this is where the camera
            // can snap to the resume position and hand back to CameraFollow without it reading
            // as a cut. By the time the fade back in finishes, it's already tracking the ball
            // normally from its new (now much lower) position.
            if (cameraShakeTarget != null)
            {
                cameraShakeTarget.transform.position = cameraResumePosition;
                cameraShakeTarget.enabled = true;
            }

            yield return new WaitForSecondsRealtime(fadeHoldTime);
            yield return StartCoroutine(screenFader.FadeIn(fadeInTime));
        }

        if (meteorTransform != null) meteorTransform.ResetToNormal();

        gameObject.SetActive(false);
    }

    // Self-contained shake (mirrors CameraFollow's Perlin-noise approach) applied as an offset
    // from cameraHoldPosition, since CameraFollow itself is disabled while this holds.
    private IEnumerator ShakeHeldCamera()
    {
        Transform cam = cameraShakeTarget.transform;
        float seedX = Random.value * 100f;
        float seedZ = Random.value * 100f;
        float elapsed = 0f;

        while (elapsed < impactShakeDuration && !cameraShakeTarget.enabled)
        {
            float falloff = 1f - (elapsed / impactShakeDuration);
            float noiseX = (Mathf.PerlinNoise(seedX, Time.time * shakeFrequency) - 0.5f) * 2f;
            float noiseZ = (Mathf.PerlinNoise(seedZ, Time.time * shakeFrequency) - 0.5f) * 2f;
            Vector3 offset = new Vector3(noiseX, 0f, noiseZ) * impactShakeMagnitude * falloff;
            cam.position = cameraHoldPosition + offset;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!cameraShakeTarget.enabled) cam.position = cameraHoldPosition;
    }
}
