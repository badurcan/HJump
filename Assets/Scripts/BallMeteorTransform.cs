using UnityEngine;
using UnityEngine.Events;

// Watches how far the ball has fallen from its starting height and, once it crosses
// a configurable depth threshold, flips it from "normal ball" to "meteor" visuals:
// trail on, particles on, material and mesh swapped to a tumbling rock look, optional
// scale pulse. Fires OnMeteorTransform so other scripts (obstacle break VFX, camera shake,
// audio) can react without needing a direct reference to this component.
// Can also be triggered externally via TriggerMeteor() (e.g. MeteorGateController's
// untouched-obstacle-streak rule or a forced-trigger ring), not just by fall distance.
[RequireComponent(typeof(SphereCollider))]
public class BallMeteorTransform : MonoBehaviour
{
    [Header("Trigger")]
    [Tooltip("How far (world units) the ball must fall from its starting Y before it transforms into a meteor.")]
    [SerializeField] private float fallDistanceToTransform = 20f;

    [Header("Visuals")]
    [Tooltip("The renderer/mesh that gets swapped on transform. Kept on a separate child object (not the physics body) so it can spin freely without fighting the Rigidbody's frozen rotation constraints.")]
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;
    [Tooltip("Swapped in on transform. Leave empty to skip the material swap.")]
    [SerializeField] private Material meteorMaterial;
    [Tooltip("Mesh swapped in on transform (e.g. a rock mesh for the meteor look). meteorMaterial above is kept - only the shape changes, the color/look stays the same. Leave empty to keep the original mesh.")]
    [SerializeField] private Mesh meteorMesh;
    [Tooltip("meshFilter's local scale is multiplied by this when meteorMesh goes in, to compensate for source meshes authored at a different scale than the ball (e.g. a rock prop mesh vs. the unit sphere). 1 = no change.")]
    [SerializeField] private float meteorMeshScaleMultiplier = 1f;
    [Tooltip("Enabled on transform (e.g. a fire/embers trail). Leave empty to skip.")]
    [SerializeField] private TrailRenderer trail;
    [Tooltip("Played on transform (e.g. ember particles). Leave empty to skip.")]
    [SerializeField] private ParticleSystem emberParticles;
    [Tooltip("Played on transform (e.g. the Hovl Meteor_body VFX, parented to this object so it travels with it). Leave empty to skip.")]
    [SerializeField] private ParticleSystem meteorBodyEffect;
    [Tooltip("Ball scales up by this factor over transformDuration when it becomes a meteor.")]
    [SerializeField] private float scalePulseMultiplier = 1.15f;
    [SerializeField] private float transformDuration = 0.35f;

    [Header("Meteor Tumble")]
    [Tooltip("While a meteor, the mesh spins around a random axis picked at transform time (not through the Rigidbody, so it can't fight BallRadiusClamp's frozen X/Z rotation).")]
    [SerializeField] private float minSpinSpeed = 90f;
    [SerializeField] private float maxSpinSpeed = 260f;

    [Header("Physics")]
    [Tooltip("Physic material used while the ball is normal - gives it a little bounce off obstacles. Leave empty to leave whatever's already on the collider.")]
    [SerializeField] private PhysicMaterial normalPhysicMaterial;
    [Tooltip("Physic material swapped in the instant the ball becomes a meteor - zero bounce, so it punches straight through instead of bouncing off what it's about to smash.")]
    [SerializeField] private PhysicMaterial meteorPhysicMaterial;

    [Header("Events")]
    public UnityEvent OnMeteorTransform;

    public bool IsMeteor { get; private set; }

    private float startY;
    private SphereCollider sphereCollider;
    private Vector3 baseScale;
    private float transformElapsed;
    private bool transforming;

    private Mesh normalMesh;
    private Vector3 normalMeshVisualScale;
    private Material originalMaterial;
    private Vector3 spinAxis;
    private float spinSpeed;

    private void Awake()
    {
        sphereCollider = GetComponent<SphereCollider>();
        baseScale = transform.localScale;

        if (meshFilter != null)
        {
            normalMesh = meshFilter.sharedMesh;
            normalMeshVisualScale = meshFilter.transform.localScale;
        }

        if (meshRenderer != null)
        {
            originalMaterial = meshRenderer.material;
        }

        if (normalPhysicMaterial != null)
        {
            sphereCollider.sharedMaterial = normalPhysicMaterial;
        }
    }

    private void Start()
    {
        startY = transform.position.y;

        if (trail != null) trail.emitting = false;
        if (emberParticles != null) emberParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (meteorBodyEffect != null) meteorBodyEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Update()
    {
        if (!IsMeteor)
        {
            float fallen = startY - transform.position.y;
            if (fallen >= fallDistanceToTransform)
            {
                BeginTransform();
            }
            return;
        }

        if (transforming)
        {
            transformElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(transformElapsed / transformDuration);
            transform.localScale = Vector3.Lerp(baseScale, baseScale * scalePulseMultiplier, t);
            if (t >= 1f) transforming = false;
        }

        if (meshFilter != null)
        {
            meshFilter.transform.Rotate(spinAxis * spinSpeed * Time.deltaTime, Space.World);
        }
    }

    // External entry point for non-fall-distance triggers (e.g. MeteorGateController's
    // untouched-obstacle-streak rule or a forced-trigger ring). No-ops if already a meteor.
    public void TriggerMeteor()
    {
        if (!IsMeteor) BeginTransform();
    }

    private void BeginTransform()
    {
        IsMeteor = true;
        transforming = true;
        transformElapsed = 0f;

        spinAxis = Random.onUnitSphere;
        spinSpeed = Random.Range(minSpinSpeed, maxSpinSpeed);

        if (meteorMaterial != null && meshRenderer != null)
        {
            meshRenderer.material = meteorMaterial;
        }

        if (meteorMesh != null && meshFilter != null)
        {
            meshFilter.sharedMesh = meteorMesh;
            meshFilter.transform.localScale = normalMeshVisualScale * meteorMeshScaleMultiplier;
        }

        if (meteorPhysicMaterial != null)
        {
            sphereCollider.sharedMaterial = meteorPhysicMaterial;
        }

        if (trail != null) trail.emitting = true;
        if (emberParticles != null) emberParticles.Play(true);
        if (meteorBodyEffect != null) meteorBodyEffect.Play(true);

        OnMeteorTransform?.Invoke();
    }

    // Reverts the ball back to its normal (pre-meteor) look and physics, using the
    // material/mesh cached at Awake - callers don't need to track/supply their own
    // reference to the original material.
    public void ResetToNormal()
    {
        IsMeteor = false;
        // Re-arm the fall-distance trigger from here, not the original spawn height - otherwise
        // a ball reverted far below its spawn point would immediately re-trigger next frame
        // (fallen already exceeds fallDistanceToTransform relative to the old startY).
        startY = transform.position.y;
        transform.localScale = baseScale;
        if (originalMaterial != null && meshRenderer != null) meshRenderer.material = originalMaterial;
        if (meshFilter != null && normalMesh != null) meshFilter.sharedMesh = normalMesh;
        if (meshFilter != null)
        {
            meshFilter.transform.localRotation = Quaternion.identity;
            meshFilter.transform.localScale = normalMeshVisualScale;
        }
        if (normalPhysicMaterial != null) sphereCollider.sharedMaterial = normalPhysicMaterial;
        if (trail != null) trail.emitting = false;
        if (emberParticles != null) emberParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (meteorBodyEffect != null) meteorBodyEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
