using System.Collections;
using UnityEngine;

// Cinematic impact beat for the standalone VFX showcase scene (MeteorVFXTest.unity). Lives on
// the impact ground. On meteor contact: freezes the ball's physics in place (camera stops too,
// since CameraFollow tracks the ball's position), shrinks the meteor visual into the impact VFX,
// shakes the camera, then holds on the impact VFX. After the VFX finishes plus a deliberate
// pause, the ball reverts to its normal look and grows back in from scale 0, physics re-enables,
// and it settles into its normal idle bounce (BallPhysicalBounce) in place on this platform.
//
// Deliberately a separate script from StageEndTrigger: StageEndTrigger's job (used on HJump's
// Ground_03) is an instant revert-and-move-on stage transition, not a held reveal beat - forcing
// both behaviors through one component would mean toggling half its fields per use case. This
// script never deactivates its ground collider, since the whole point here is the ball staying
// and bouncing on it afterward.
[RequireComponent(typeof(Collider))]
public class MeteorImpactReveal : MonoBehaviour
{
    [Tooltip("Optional VFX spawned at the impact point. Leave empty to skip gracefully.")]
    [SerializeField] private GameObject impactVfxPrefab;
    [Tooltip("Euler rotation the impact VFX is spawned with (its particle systems are authored facing a particular local axis, so this orients the burst correctly against the impact surface).")]
    [SerializeField] private Vector3 impactVfxRotation = new Vector3(-90f, 0f, 0f);
    [Tooltip("World-space Y offset applied on top of the contact point, so the VFX plays a bit above the impact plane's surface instead of exactly on it.")]
    [SerializeField] private float impactVfxHeightOffset = 1f;
    [Tooltip("How long impactVfxPrefab's particle systems take to fully finish (longest system's duration + max start lifetime). Used to time the reveal - set this to match the actual prefab.")]
    [SerializeField] private float impactVfxDuration = 2f;
    [Tooltip("Extra hold time after the VFX finishes, before the ball reveals itself.")]
    [SerializeField] private float postEffectRevealDelay = 5f;

    [SerializeField] private CameraFollow cameraShakeTarget;
    [SerializeField] private float impactShakeDuration = 0.7f;
    [SerializeField] private float impactShakeMagnitude = 0.65f;
    [SerializeField] private float shakeFrequency = 25f;

    [Header("Pre-impact camera transition")]
    [Tooltip("Once the falling meteor gets within this many world units (on Y) of this object, the camera breaks off its normal chase-follow and moves in a straight line into cinematicCameraPosition, so the impact reads as a framed close-up instead of being viewed from the far chase distance.")]
    [SerializeField] private float cameraEaseTriggerDistance = 25f;
    [SerializeField] private Vector3 cinematicCameraPosition = new Vector3(0f, -117.1f, -5.5f);
    [Tooltip("Constant speed (world units/second) of the straight-line move to cinematicCameraPosition - not a fixed duration, so it always covers whatever distance is left at a steady, predictable rate instead of racing (and potentially losing) the falling ball.")]
    [SerializeField] private float cameraTransitionSpeed = 60f;

    [Tooltip("How long the meteor visual takes to shrink to nothing right at impact (masked by the VFX burst).")]
    [SerializeField] private float shrinkDuration = 0.15f;
    [Tooltip("How long the ball takes to grow back from scale 0 to its normal size on reveal.")]
    [SerializeField] private float growDuration = 0.4f;
    [Tooltip("Small upward nudge applied when physics re-enables, so the ball has a clean fresh landing collision to kick off its idle bounce instead of starting already embedded in the surface.")]
    [SerializeField] private float reboundNudge = 0.3f;

    [SerializeField] private BallMeteorTransform meteorTransform;
    [Tooltip("Leave empty to auto-find on the same object as meteorTransform.")]
    [SerializeField] private Rigidbody ballRigidbody;

    private bool triggered;
    private bool cameraEaseStarted;

    private void Awake()
    {
        if (ballRigidbody == null && meteorTransform != null) ballRigidbody = meteorTransform.GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (cameraEaseStarted || cameraShakeTarget == null || meteorTransform == null) return;
        if (!meteorTransform.IsMeteor) return;

        float distanceAboveGround = meteorTransform.transform.position.y - transform.position.y;
        if (distanceAboveGround <= cameraEaseTriggerDistance)
        {
            cameraEaseStarted = true;
            StartCoroutine(EaseCameraToImpactView());
        }
    }

    private IEnumerator EaseCameraToImpactView()
    {
        // Hand camera control over from CameraFollow's per-frame chase logic to our own straight
        // line move - otherwise CameraFollow's LateUpdate would fight this every frame. Left
        // disabled from here on - the camera holds this framing through the impact, freeze/
        // reveal, and the idle bounce afterward (all of which stay close to this spot), and our
        // own shake (below) drives its own offset directly since CameraFollow.Shake() needs its
        // LateUpdate running to apply, which is off for good once this starts.
        cameraShakeTarget.enabled = false;
        Transform cam = cameraShakeTarget.transform;

        while (cam.position != cinematicCameraPosition)
        {
            cam.position = Vector3.MoveTowards(cam.position, cinematicCameraPosition, cameraTransitionSpeed * Time.deltaTime);
            yield return null;
        }
    }

    // Self-contained shake (mirrors CameraFollow's Perlin-noise approach) applied as an offset
    // from cinematicCameraPosition, since CameraFollow itself is disabled by this point.
    private IEnumerator ShakeCinematicCamera()
    {
        Transform cam = cameraShakeTarget.transform;
        float seedX = Random.value * 100f;
        float seedZ = Random.value * 100f;
        float elapsed = 0f;

        while (elapsed < impactShakeDuration)
        {
            float falloff = 1f - (elapsed / impactShakeDuration);
            float noiseX = (Mathf.PerlinNoise(seedX, Time.time * shakeFrequency) - 0.5f) * 2f;
            float noiseZ = (Mathf.PerlinNoise(seedZ, Time.time * shakeFrequency) - 0.5f) * 2f;
            Vector3 offset = new Vector3(noiseX, 0f, noiseZ) * impactShakeMagnitude * falloff;
            cam.position = cinematicCameraPosition + offset;
            elapsed += Time.deltaTime;
            yield return null;
        }
        cam.position = cinematicCameraPosition;
    }

    private void OnCollisionEnter(Collision collision) => TryTrigger(collision);
    private void OnCollisionStay(Collision collision) => TryTrigger(collision);

    private void TryTrigger(Collision collision)
    {
        if (triggered) return;
        if (meteorTransform == null || !meteorTransform.IsMeteor) return;

        triggered = true;
        Vector3 contactPoint = collision.GetContact(0).point;
        StartCoroutine(ImpactSequence(contactPoint));
    }

    private IEnumerator ImpactSequence(Vector3 contactPoint)
    {
        Transform ball = meteorTransform.transform;

        // Freeze in place - camera stops too, since it only ever tracks the ball's position.
        if (ballRigidbody != null)
        {
            ballRigidbody.velocity = Vector3.zero;
            ballRigidbody.angularVelocity = Vector3.zero;
            ballRigidbody.isKinematic = true;
        }

        if (impactVfxPrefab != null)
        {
            Vector3 vfxPosition = contactPoint + Vector3.up * impactVfxHeightOffset;
            GameObject vfx = Instantiate(impactVfxPrefab, vfxPosition, Quaternion.Euler(impactVfxRotation));
            Destroy(vfx, impactVfxDuration + 1f); // safety net past the expected lifetime
        }

        if (cameraShakeTarget != null)
        {
            // Guarantee the camera is exactly at the cinematic framing by impact time, even if
            // the fall was fast enough that EaseCameraToImpactView() hadn't finished yet.
            cameraShakeTarget.enabled = false;
            cameraShakeTarget.transform.position = cinematicCameraPosition;
            StartCoroutine(ShakeCinematicCamera());
        }

        // Shrink the meteor visual away, masked by the impact VFX burst.
        Vector3 startScale = ball.localScale;
        float t = 0f;
        while (t < shrinkDuration)
        {
            t += Time.deltaTime;
            ball.localScale = Vector3.Lerp(startScale, Vector3.zero, t / shrinkDuration);
            yield return null;
        }
        ball.localScale = Vector3.zero;

        yield return new WaitForSeconds(impactVfxDuration + postEffectRevealDelay);

        // Revert visuals/material/mesh back to normal, then override the instant scale snap
        // ResetToNormal() does internally so we can grow it back in ourselves.
        meteorTransform.ResetToNormal();
        Vector3 targetScale = ball.localScale; // ResetToNormal() just set this to baseScale
        ball.localScale = Vector3.zero;

        t = 0f;
        while (t < growDuration)
        {
            t += Time.deltaTime;
            ball.localScale = Vector3.Lerp(Vector3.zero, targetScale, t / growDuration);
            yield return null;
        }
        ball.localScale = targetScale;

        // Hand back to physics with a small upward nudge so BallPhysicalBounce gets a clean
        // fresh landing collision to kick off the idle bounce, instead of starting embedded.
        if (ballRigidbody != null)
        {
            ballRigidbody.isKinematic = false;
            ball.position += Vector3.up * reboundNudge;
        }
    }
}
