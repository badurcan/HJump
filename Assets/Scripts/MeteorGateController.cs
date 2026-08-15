using System.Collections.Generic;
using UnityEngine;

// Tracks the ball's progress down the tower ring by ring and triggers the meteor transform
// two ways:
//  - passing N consecutive rings without touching any of them (a clean-fall reward streak)
//  - reaching a specific named ring unconditionally, regardless of the streak
//
// The ball is locked to a single vertical line (BallRadiusClamp), and the tower only rotates
// rings underneath it (TowerController) - it never translates - so each ring's world Y is
// stable for the whole session and can be snapshotted once at Start(). At any given moment
// the ball can only possibly be touching the one ring at its current Y band, so a simple
// "did any genuine touch happen since the last ring's threshold was crossed" flag is enough
// to know whether that ring counted as touched or passed clean - no per-ring identity
// tracking needed.
[RequireComponent(typeof(Rigidbody))]
public class MeteorGateController : MonoBehaviour
{
    [Tooltip("Root transform containing all Obstacle-tagged ring colliders as children (the tower, e.g. 'obs').")]
    [SerializeField] private Transform towerRoot;

    [Tooltip("Number of consecutive untouched obstacle rings that triggers a meteor transform.")]
    [SerializeField] private int untouchedStreakToTransform = 3;

    [Tooltip("Exact GameObject name of the ring that unconditionally forces a meteor transform when passed, regardless of streak.")]
    [SerializeField] private string forcedTriggerRingName = "Obs_330 (20)";

    [Tooltip("Minimum upward-ness (contact normal dot world up) for a collision to count as a genuine touch, not a graze. Mirrors BallPhysicalBounce's graze-rejection check.")]
    [SerializeField] private float minUpwardNormal = 0.5f;

    [Tooltip("Leave empty to auto-find on this object.")]
    [SerializeField] private BallMeteorTransform meteorTransform;

    private struct Gate
    {
        public float worldY;
        public string name;
    }

    private readonly List<Gate> gates = new List<Gate>();
    private int nextGateIndex;
    private int untouchedStreak;
    private bool touchedSinceLastGate;

    private void Awake()
    {
        if (meteorTransform == null) meteorTransform = GetComponent<BallMeteorTransform>();
    }

    [Tooltip("Collider Y positions within this distance of each other are treated as the same physical ring band (some ring types, e.g. Obs_100, are built from two separate collider pieces at an identical height) and merged into a single gate, so 'N obstacles' counts distinct ring passes rather than raw collider count.")]
    [SerializeField] private float gateMergeTolerance = 0.1f;

    private void Start()
    {
        List<Gate> raw = new List<Gate>();

        if (towerRoot != null)
        {
            foreach (Collider col in towerRoot.GetComponentsInChildren<Collider>(true))
            {
                if (!col.CompareTag("Obstacle")) continue;
                raw.Add(new Gate { worldY = col.transform.position.y, name = col.gameObject.name });
            }
        }

        // Ball falls from high Y to low Y, so it should evaluate the topmost ring first.
        raw.Sort((a, b) => b.worldY.CompareTo(a.worldY));

        gates.Clear();
        foreach (Gate g in raw)
        {
            if (gates.Count > 0 && Mathf.Abs(gates[gates.Count - 1].worldY - g.worldY) <= gateMergeTolerance)
            {
                continue; // same band as the previous entry - already represented by one gate
            }
            gates.Add(g);
        }

        // Skip any gate positioned above the ball's own starting height (e.g. a decorative
        // cap ring right at spawn). The ball never genuinely "falls past" those - it just
        // starts life already below them - so counting them would inflate the untouched
        // streak before real gameplay even begins.
        float startY = transform.position.y;
        nextGateIndex = 0;
        while (nextGateIndex < gates.Count && gates[nextGateIndex].worldY > startY)
        {
            nextGateIndex++;
        }

        untouchedStreak = 0;
        touchedSinceLastGate = false;
    }

    private void Update()
    {
        if (meteorTransform == null) return;

        float ballY = transform.position.y;

        // Loop (not "if") in case the ball crosses more than one gate in a single frame at
        // high fall speed - otherwise a fast frame could silently skip a gate's evaluation.
        while (nextGateIndex < gates.Count && ballY < gates[nextGateIndex].worldY)
        {
            EvaluateGate(gates[nextGateIndex]);
            nextGateIndex++;
        }
    }

    private void EvaluateGate(Gate gate)
    {
        if (gate.name == forcedTriggerRingName)
        {
            // Independent, unconditional rule - doesn't consume or reset the untouched streak.
            meteorTransform.TriggerMeteor();
        }
        else if (touchedSinceLastGate)
        {
            untouchedStreak = 0;
        }
        else
        {
            untouchedStreak++;
            if (untouchedStreak >= untouchedStreakToTransform)
            {
                meteorTransform.TriggerMeteor();
                untouchedStreak = 0;
            }
        }

        touchedSinceLastGate = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Obstacle")) return;

        Vector3 avgNormal = Vector3.zero;
        int count = collision.contactCount;
        for (int i = 0; i < count; i++)
        {
            avgNormal += collision.GetContact(i).normal;
        }
        if (count > 0) avgNormal /= count;

        if (avgNormal.y < minUpwardNormal) return; // graze (e.g. the tower's central shaft), not a genuine touch

        touchedSinceLastGate = true;
        // Zero the streak the instant a genuine touch/bounce happens, not just at the next
        // gate boundary - a bounce always means "not a clean pass," so the counter should
        // reflect that immediately rather than waiting for the ball to fall past this ring.
        untouchedStreak = 0;
    }
}
