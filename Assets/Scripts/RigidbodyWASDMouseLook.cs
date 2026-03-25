using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// WASD movement + mouse look controller using Unity's new Input System and a Rigidbody.
/// Attach this to the player GameObject that has a Rigidbody.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RigidbodyWASDMouseLook : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Rigidbody used for movement. If null, uses the Rigidbody on this GameObject.")]
    public Rigidbody rb;

    [Tooltip("Transform to rotate for pitch (typically the camera pitch pivot). Pitch is applied to this transform's local rotation.")]
    public Transform pitchTransform;

    [Tooltip("Optional transform to rotate for yaw (typically the camera yaw pivot). If null, yaw is still used for camera orbit and movement direction calculations.")]
    public Transform cameraYawTransform;

    [Tooltip("Optional camera transform to drive for a simple third-person orbit camera.")]
    public Transform cameraTransform;

    [Tooltip("Target point for the third-person camera. If null, uses this GameObject's position.")]
    public Transform cameraTarget;

    [Header("Auto Setup")]
    [Tooltip("If true, and cameraTransform is not assigned, tries to find a child Camera and uses it.")]
    public bool autoFindCameraTransform = true;

    [Header("Movement")]
    [Tooltip("Horizontal move speed in units/second.")]
    public float moveSpeed = 6f;

    [Tooltip("If true, movement direction is based on the current camera orbit yaw (third-person style). If false, uses player forward (player yaw).")]
    public bool moveRelativeToCamera = true;

    [Header("Look")]
    [Tooltip("Mouse sensitivity multiplier.")]
    public float lookSensitivity = 0.1f;

    [Tooltip("Invert Y look (mouse up = pitch down).")]
    public bool invertY = true;

    [Tooltip("Pitch clamp in degrees.")]
    public float minPitch = -80f;
    public float maxPitch = 80f;

    [Header("Third-Person Player Rotation")]
    [Tooltip("If true, rotate the player to match camera yaw. If false, player yaw is controlled by movement direction (if enabled below).")]
    public bool rotatePlayerWithCameraYaw = false;

    [Tooltip("If true, rotate the player to face the movement direction (camera-relative). Typical third-person feel.")]
    public bool rotatePlayerToMoveDirection = true;

    [Tooltip("Player turning speed in degrees/second.")]
    public float playerTurnSpeed = 720f;

    [Header("Third-Person Camera (optional)")]
    [Tooltip("If true and cameraTransform is assigned, script will position/orient the camera as a simple orbit rig.")]
    public bool driveCameraTransform = true;

    [Tooltip("Distance of the camera behind the target.")]
    public float cameraDistance = 5f;

    [Tooltip("World-space height of the camera from the target position.")]
    public float cameraHeight = 1.5f;

    [Tooltip("World-space height the camera looks at (target head height).")]
    public float cameraLookAtHeight = 1.5f;

    [Tooltip("Higher values = snappier camera follow.")]
    public float cameraSmooth = 15f;

    [Header("Camera Mode")]
    [Tooltip("When enabled, uses a first-person camera style (yaw on player, pitch on camera/pitch pivot) and disables orbit positioning.")]
    public bool firstPersonMode = true;

    [Header("Jump (Rigidbody)")]
    [Tooltip("Vertical velocity applied when jumping.")]
    public float jumpVelocity = 7.5f;

    [Tooltip("Layers considered as ground for jumping.")]
    public LayerMask groundLayers = ~0;

    [Tooltip("If true, auto-finds a Collider on this GameObject for ground checking.")]
    public bool autoFindColliderForGroundCheck = true;

    [Tooltip("Optional collider used for grounded check. If null and autoFindColliderForGroundCheck is true, uses GetComponent<Collider>().")]
    public Collider groundCheckCollider;

    [Tooltip("Extra distance below the collider to consider grounded.")]
    public float groundCheckDistance = 0.2f;

    [Tooltip("How much to shrink the sphere radius relative to collider extents (helps avoid side hits).")]
    [Range(0f, 0.9f)]
    public float groundCheckRadiusShrink = 0.1f;

    [Tooltip("If true, jump resets upward velocity to ensure consistent jump height.")]
    public bool resetVerticalVelocityOnJump = true;

    [Header("Crouch")]
    [Tooltip("Transform to scale when crouching. If null, scales this GameObject's transform.")]
    [SerializeField] private Transform crouchScaleRoot;

    [Tooltip("Multiplier applied to movement speed while crouched.")]
    [SerializeField, Range(0.1f, 1f)] private float crouchMoveSpeedMultiplier = 0.5f;

    [Tooltip("Height multiplier applied to the capsule collider while crouched.")]
    [SerializeField, Range(0.1f, 1f)] private float crouchHeightMultiplier = 0.5f;

    [Tooltip("Extra distance for ceiling check when trying to stand up.")]
    [SerializeField] private float standUpCheckPadding = 0.02f;

    [Tooltip("Layers that can block standing up (typically Everything).")]
    [SerializeField] private LayerMask standUpObstaclesLayers = ~0;

    [Header("Input Actions (New Input System)")]
    [Tooltip("Move action (Vector2). Expected mapping: WASD -> (x,y).")]
    public InputActionReference moveAction;

    [Tooltip("Look action (Vector2). Expected mapping: mouse delta -> (x=deltaX,y=deltaY).")]
    public InputActionReference lookAction;

    [Tooltip("Jump action (Button). Expected mapping: Space -> triggered/performed.")]
    public InputActionReference jumpAction;

    [Tooltip("Crouch action (Button). Expected mapping: Left Shift -> press/hold.")]
    public InputActionReference crouchAction;

    [Tooltip("If true, enables and disables input actions with this component's lifecycle.")]
    public bool autoEnableActions = true;

    [Header("Cursor")]
    public bool lockCursorOnStart = true;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool jumpRequested;
    private bool isGrounded;
    private bool isCrouched;

    private float yaw;
    private float pitch;

    private CapsuleCollider capsuleCollider;
    private Vector3 standingScaleRootLocalScale;
    private bool hasStandingScaleRootLocalScale;
    private readonly Collider[] standUpOverlapBuffer = new Collider[16];

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (autoFindColliderForGroundCheck && groundCheckCollider == null)
            groundCheckCollider = GetComponent<Collider>();

        capsuleCollider = GetComponent<CapsuleCollider>();
        if (crouchScaleRoot == null)
            crouchScaleRoot = transform;

        standingScaleRootLocalScale = crouchScaleRoot.localScale;
        hasStandingScaleRootLocalScale = true;

        if (cameraTarget == null)
            cameraTarget = transform;

        if (autoFindCameraTransform && cameraTransform == null)
        {
            // Common third-person layout: player has a Camera as a child.
            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null)
                cameraTransform = cam.transform;
        }

        if (firstPersonMode)
        {
            // In first-person, pitch is typically applied to the camera (or a pitch pivot) which is a child of the player.
            if (pitchTransform == null && cameraTransform != null)
                pitchTransform = cameraTransform;
        }

        // Initialize yaw/pitch from current transforms (helps when scene-placing your rig).
        yaw = (cameraYawTransform != null) ? cameraYawTransform.eulerAngles.y : transform.eulerAngles.y;

        if (pitchTransform != null)
        {
            pitch = pitchTransform.localEulerAngles.x;
            // Convert to [-180, 180] for stable clamping.
            if (pitch > 180f) pitch -= 360f;
        }
        else
        {
            // If no pitchTransform is provided, pitch is still tracked and used for camera orbit.
            pitch = transform.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
        }
    }

    private void OnEnable()
    {
        if (autoEnableActions)
        {
            moveAction?.action.Enable();
            lookAction?.action.Enable();
            jumpAction?.action.Enable();
            crouchAction?.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (autoEnableActions)
        {
            moveAction?.action.Disable();
            lookAction?.action.Disable();
            jumpAction?.action.Disable();
            crouchAction?.action.Disable();
        }
    }

    private void Start()
    {
        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Sync rotation once on start.
        ApplyLookRotation();
    }

    private void Update()
    {
        // Allow ESC to unlock cursor.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (moveAction != null)
            moveInput = moveAction.action.ReadValue<Vector2>();

        if (lookAction != null)
            lookInput = lookAction.action.ReadValue<Vector2>();

        if (jumpAction != null && jumpAction.action.WasPressedThisFrame())
            jumpRequested = true;

        bool wantsCrouch = crouchAction != null && crouchAction.action.IsPressed();
        if (wantsCrouch)
            SetCrouched(true);
        else
            TryStandUp();

        // Mouse look: interpret input as delta (most common when binding mouse to a Vector2 delta).
        yaw += lookInput.x * lookSensitivity;
        pitch -= lookInput.y * lookSensitivity * (invertY ? 1f : -1f);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        ApplyLookRotation();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        // Ground check for jumping.
        isGrounded = ComputeGrounded();

        Vector3 forward;
        Vector3 right;

        // Choose basis for movement direction.
        if (moveRelativeToCamera)
        {
            // Use camera orbit yaw for horizontal movement; ignore pitch.
            forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
        }
        else
        {
            forward = transform.forward;
            right = transform.right;
        }

        // moveInput expected as (x=strafe, y=forward) from WASD.
        float currentMoveSpeed = isCrouched ? moveSpeed * crouchMoveSpeedMultiplier : moveSpeed;
        Vector3 desiredVelocity = (right * moveInput.x + forward * moveInput.y) * currentMoveSpeed;

        // Typical third-person feel: rotate the player to face where they're moving.
        if (!firstPersonMode && rotatePlayerToMoveDirection)
        {
            Vector3 flatMove = new Vector3(desiredVelocity.x, 0f, desiredVelocity.z);
            if (flatMove.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(flatMove.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, playerTurnSpeed * Time.fixedDeltaTime);
            }
        }

        // Jump (applied in FixedUpdate to sync with physics step).
        if (jumpRequested && isGrounded)
        {
            Vector3 v = rb.linearVelocity;
            float yVel = resetVerticalVelocityOnJump ? jumpVelocity : Mathf.Max(v.y, 0f) + jumpVelocity;
            rb.linearVelocity = new Vector3(v.x, yVel, v.z);
            jumpRequested = false;
        }

        // Preserve vertical velocity so gravity/jumping still works.
        Vector3 vel = rb.linearVelocity;
        rb.linearVelocity = new Vector3(desiredVelocity.x, vel.y, desiredVelocity.z);
    }

    private bool ComputeGrounded()
    {
        if (groundCheckCollider == null)
            return false;

        Bounds b = groundCheckCollider.bounds;

        // Sphere just above the bottom of the collider.
        float radius = Mathf.Max(0.01f, Mathf.Min(b.extents.x, b.extents.z) * (1f - groundCheckRadiusShrink));
        Vector3 origin = new Vector3(b.center.x, b.min.y + radius + 0.01f, b.center.z);

        // SphereCast down a small distance to detect ground.
        return Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out _,
            groundCheckDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );
    }

    private void ApplyLookRotation()
    {
        if (firstPersonMode)
        {
            // Yaw rotates the player.
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Pitch rotates the camera/pitch pivot (usually a child of the player).
            if (pitchTransform != null)
                pitchTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            return;
        }

        // Third-person orbit: rotate camera rig pivots (if provided).
        if (cameraYawTransform != null)
            cameraYawTransform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (pitchTransform != null)
            pitchTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // Optionally rotate player to match camera yaw.
        if (rotatePlayerWithCameraYaw && !rotatePlayerToMoveDirection)
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void LateUpdate()
    {
        // Simple orbit camera.
        if (firstPersonMode) return;
        if (cameraTransform == null || !driveCameraTransform) return;

        Vector3 targetPos = cameraTarget != null ? cameraTarget.position : transform.position;
        Vector3 lookTarget = targetPos + Vector3.up * cameraLookAtHeight;
        Vector3 orbitForward = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;

        Vector3 desiredCamPos = (targetPos + Vector3.up * cameraHeight) - orbitForward * cameraDistance;

        // Exponential smoothing (frame-rate independent).
        float t = 1f - Mathf.Exp(-cameraSmooth * Time.deltaTime);
        cameraTransform.position = Vector3.Lerp(cameraTransform.position, desiredCamPos, t);
        cameraTransform.LookAt(lookTarget);
    }

    private void SetCrouched(bool crouched)
    {
        if (isCrouched == crouched)
            return;

        isCrouched = crouched;

        if (crouchScaleRoot == null || !hasStandingScaleRootLocalScale)
            return;

        Vector3 targetScale = standingScaleRootLocalScale;
        targetScale.y *= crouched ? crouchHeightMultiplier : 1f;
        crouchScaleRoot.localScale = targetScale;
    }

    private void TryStandUp()
    {
        if (!isCrouched)
            return;

        if (!CanStandUp())
            return;

        SetCrouched(false);
    }

    private bool CanStandUp()
    {
        if (capsuleCollider == null)
            return true;

        Transform scaleRoot = crouchScaleRoot != null ? crouchScaleRoot : transform;
        Vector3 standingScale = hasStandingScaleRootLocalScale ? standingScaleRootLocalScale : scaleRoot.localScale;

        // Approximate world dimensions for the capsule when standing by applying the standing scale.
        // Note: CapsuleCollider is aligned to local Y; scaling parent transform affects it.
        Vector3 currentLossy = transform.lossyScale;
        Vector3 standingLossy = new Vector3(
            currentLossy.x * (standingScale.x / Mathf.Max(0.0001f, scaleRoot.localScale.x)),
            currentLossy.y * (standingScale.y / Mathf.Max(0.0001f, scaleRoot.localScale.y)),
            currentLossy.z * (standingScale.z / Mathf.Max(0.0001f, scaleRoot.localScale.z))
        );

        float radiusWorld = capsuleCollider.radius * Mathf.Max(Mathf.Abs(standingLossy.x), Mathf.Abs(standingLossy.z));
        float heightWorld = capsuleCollider.height * Mathf.Abs(standingLossy.y);
        heightWorld = Mathf.Max(radiusWorld * 2f, heightWorld);

        Bounds currentBounds = capsuleCollider.bounds; // already reflects current (crouched) scale
        float currentHeightWorld = currentBounds.size.y;
        float currentRadiusWorld = Mathf.Max(0.001f, Mathf.Min(currentBounds.extents.x, currentBounds.extents.z));

        float extraHeightNeeded = heightWorld - currentHeightWorld;
        if (extraHeightNeeded <= 0.0001f)
            return true;

        extraHeightNeeded += Mathf.Max(0f, standUpCheckPadding);

        Vector3 up = transform.up;
        Vector3 currentCenterWorld = currentBounds.center;
        float currentHalfHeightWithoutCaps = Mathf.Max(0f, currentHeightWorld * 0.5f - currentRadiusWorld);
        Vector3 currentTopSphereCenter = currentCenterWorld + up * currentHalfHeightWithoutCaps;

        // Check only the space ABOVE the current crouched capsule (prevents floor from blocking stand-up).
        Vector3 p2 = currentTopSphereCenter + up * 0.01f;
        Vector3 p1 = currentTopSphereCenter + up * (extraHeightNeeded + 0.01f);

        int mask = standUpObstaclesLayers.value;
        if (IsCapsuleBlockedByOtherColliders(p1, p2, currentRadiusWorld, mask))
            return false;

        return true;
    }

    private bool IsCapsuleBlockedByOtherColliders(Vector3 p1, Vector3 p2, float radius, int layerMask)
    {
        int hits = Physics.OverlapCapsuleNonAlloc(
            p1,
            p2,
            radius,
            standUpOverlapBuffer,
            layerMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hits; i++)
        {
            Collider c = standUpOverlapBuffer[i];
            if (c == null)
                continue;

            // Ignore our own colliders (including children).
            if (c.transform == transform || c.transform.IsChildOf(transform))
                continue;

            // Also ignore anything attached to the same rigidbody (common setup).
            if (rb != null && c.attachedRigidbody == rb)
                continue;

            return true;
        }

        return false;
    }
}

