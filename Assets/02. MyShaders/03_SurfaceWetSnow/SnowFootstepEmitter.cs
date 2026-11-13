using UnityEngine;

/// <summary>
/// Stamps shoe-shaped marks into SnowFootprintManager.
/// Use LeftFootstep/RightFootstep as Animation Events, or enable distance fallback.
/// </summary>
[DisallowMultipleComponent]
public class SnowFootstepEmitter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] SnowFootprintManager footprintManager;
    [SerializeField] Transform leftFoot;
    [SerializeField] Transform rightFoot;
    [SerializeField] CharacterController characterController;

    [Header("Footprint Shape")]
    [Min(0.04f)] [SerializeField] float footprintWidth = 0.19f;
    [Min(0.06f)] [SerializeField] float footprintLength = 0.34f;
    [Range(0f, 1f)] [SerializeField] float strength = 0.95f;

    [Header("Automatic Fallback")]
    [Tooltip("Alternating prints based on movement distance. Turn this off when using Animation Events.")]
    [SerializeField] bool useDistanceFallback = true;
    [Min(0.1f)] [SerializeField] float stepDistance = 0.62f;
    [Min(0f)] [SerializeField] float sideOffset = 0.09f;

    Vector3 lastPosition;
    float distanceSinceLastStep;
    bool nextFallbackLeft = true;

    void Awake()
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();
        lastPosition = transform.position;
    }

    void OnEnable()
    {
        lastPosition = transform.position;
        distanceSinceLastStep = 0f;
    }

    void Update()
    {
        if (!useDistanceFallback || footprintManager == null)
        {
            lastPosition = transform.position;
            return;
        }

        Vector3 current = transform.position;
        if (characterController != null && !characterController.isGrounded)
        {
            lastPosition = current;
            distanceSinceLastStep = 0f;
            return;
        }

        Vector3 segment = current - lastPosition;
        segment.y = 0f;
        float segmentLength = segment.magnitude;
        if (segmentLength < 0.0001f)
        {
            lastPosition = current;
            return;
        }

        Vector3 segmentStart = lastPosition;
        Vector3 travelForward = segment / segmentLength;
        float consumed = 0f;

        // Interpolate stamp positions along this frame's movement so a long frame
        // does not stack several footprints at the exact same final position.
        while (distanceSinceLastStep + (segmentLength - consumed) >= stepDistance)
        {
            float needed = stepDistance - distanceSinceLastStep;
            consumed += needed;
            float t = Mathf.Clamp01(consumed / segmentLength);
            Vector3 stepPosition = segmentStart + segment * t;

            bool isLeft = nextFallbackLeft;
            nextFallbackLeft = !nextFallbackLeft;
            stepPosition += transform.right * (isLeft ? -sideOffset : sideOffset);

            Vector3 facing = transform.forward.sqrMagnitude > 0.001f
                ? transform.forward
                : travelForward;
            Stamp(stepPosition, facing);
            distanceSinceLastStep = 0f;
        }

        distanceSinceLastStep += segmentLength - consumed;
        lastPosition = current;
    }

    /// <summary>Call from a left-foot contact Animation Event.</summary>
    public void LeftFootstep() => StampFromFoot(leftFoot, -1f);

    /// <summary>Call from a right-foot contact Animation Event.</summary>
    public void RightFootstep() => StampFromFoot(rightFoot, 1f);

    /// <summary>Optional public entry point for custom movement/animation code.</summary>
    public void Footstep() => StampFromFoot(null, 0f);

    void StampFromFoot(Transform foot, float fallbackSide)
    {
        if (foot != null)
        {
            Stamp(foot.position, foot.forward);
            return;
        }

        Vector3 position = transform.position;
        if (fallbackSide != 0f)
            position += transform.right * (fallbackSide * sideOffset);
        Stamp(position, transform.forward);
    }

    void Stamp(Vector3 position, Vector3 forward)
    {
        if (footprintManager == null) return;
        if (characterController != null && !characterController.isGrounded) return;
        footprintManager.Stamp(position, forward, footprintWidth, footprintLength, strength);
    }
}
