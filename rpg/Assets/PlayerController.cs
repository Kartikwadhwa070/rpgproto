using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpHeight = 2f;
    public float gravity = -9.81f;

    [Header("Dash Settings")]
    public float dashDistance = 10f;
    public float dashDuration = 0.3f;
    public float dashCooldown = 2f;
    public float invisibilityDuration = 0.5f; // How long to stay invisible during dash
    public float verticalDashHeight = 8f; // Height for vertical dash
    public bool allowAirDash = true; // Can dash once in mid-air
    public AnimationCurve dashCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("References")]
    public Transform cameraTransform;

    [Header("Visual Effects")]
    public Renderer[] playerRenderers; // Assign player mesh renderers for invisibility

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private PlayerInputHandler input;
    private PlayerState currentState = PlayerState.Idle;

    // Dash variables
    private bool isDashing = false;
    private bool canDash = true;
    private bool hasUsedAirDash = false; // Track if air dash was used
    private Vector3 dashDirection;
    private float dashTimer = 0f;
    private Vector3 dashStartPosition;
    private bool isVerticalDash = false;

    // Invisibility variables
    private bool isInvisible = false;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<PlayerInputHandler>();
        SetupInvisibilityMaterials();
    }

    void Start()
    {
        // Auto-find player renderers if not assigned
        if (playerRenderers.Length == 0)
        {
            playerRenderers = GetComponentsInChildren<Renderer>();
            Debug.Log($"Auto-found {playerRenderers.Length} renderers for invisibility");
        }

        // Debug: List all found renderers
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            Debug.Log($"Renderer {i}: {playerRenderers[i].name}");
        }
    }

    void Update()
    {
        // Reset air dash when grounded
        if (isGrounded && hasUsedAirDash)
        {
            hasUsedAirDash = false;
        }

        HandleDashInput();

        if (isDashing)
        {
            HandleDash();
        }
        else
        {
            HandleMovement();
            HandleGravity();
            HandleJump();
        }

        controller.Move(velocity * Time.deltaTime);
    }

    void SetupInvisibilityMaterials()
    {
        // Simple approach - we'll just enable/disable renderers
        // This is more reliable than material manipulation
    }

    void HandleDashInput()
    {
        if (input.DashPressed && canDash && !isDashing)
        {
            // Can dash if grounded OR if in air and haven't used air dash yet
            bool canDashNow = isGrounded || (allowAirDash && !hasUsedAirDash);

            if (canDashNow)
            {
                StartDash();
            }
        }
    }

    void StartDash()
    {
        isDashing = true;
        canDash = false;
        dashTimer = 0f;
        currentState = PlayerState.Dashing;
        isVerticalDash = false;

        // Mark air dash as used if we're in the air
        if (!isGrounded)
        {
            hasUsedAirDash = true;
        }

        // Determine dash direction
        Vector2 moveInput = input.MoveInput;
        Vector3 inputDirection = new Vector3(moveInput.x, 0, moveInput.y);

        if (inputDirection.magnitude >= 0.1f)
        {
            // Dash in movement direction relative to camera (horizontal)
            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            dashDirection = Quaternion.Euler(0, targetAngle, 0) * Vector3.forward;
        }
        else
        {
            // No input - vertical dash upward
            dashDirection = Vector3.up;
            isVerticalDash = true;
            Debug.Log("Vertical dash activated!");
        }

        dashStartPosition = transform.position;

        // Start invisibility
        StartCoroutine(HandleInvisibility());

        // Start cooldown timer
        StartCoroutine(DashCooldownTimer());
    }

    void HandleDash()
    {
        dashTimer += Time.deltaTime;
        float normalizedTime = dashTimer / dashDuration;

        if (normalizedTime >= 1f)
        {
            // Dash finished
            isDashing = false;
            dashTimer = 0f;

            // Reset velocity properly after dash
            if (isVerticalDash)
            {
                // For vertical dash, set a small downward velocity to start falling immediately
                velocity.y = -2f;
            }
            else
            {
                // For horizontal dash, preserve some downward velocity if we were falling
                if (velocity.y > 0f)
                {
                    velocity.y = 0f; // Stop any upward momentum
                }
            }

            isVerticalDash = false;
            currentState = PlayerState.Idle;
            return;
        }

        // Calculate dash movement using animation curve
        float curveValue = dashCurve.Evaluate(normalizedTime);

        Vector3 targetPosition;
        if (isVerticalDash)
        {
            // Vertical dash - go upward
            targetPosition = dashStartPosition + Vector3.up * verticalDashHeight;
        }
        else
        {
            // Horizontal dash
            targetPosition = dashStartPosition + dashDirection * dashDistance;
        }

        Vector3 currentTargetPos = Vector3.Lerp(dashStartPosition, targetPosition, curveValue);

        // Calculate movement delta
        Vector3 movement = currentTargetPos - transform.position;

        // Apply movement directly through CharacterController
        controller.Move(movement);

        // Control velocity during dash to prevent weird physics interactions
        if (isVerticalDash)
        {
            // During vertical dash, suppress gravity completely
            velocity.y = 0f;
        }
        else
        {
            // During horizontal dash, maintain slight downward velocity if in air
            if (!isGrounded)
            {
                velocity.y = Mathf.Max(velocity.y, -5f); // Cap falling speed during dash
            }
            else
            {
                velocity.y = 0f;
            }
        }
    }

    IEnumerator HandleInvisibility()
    {
        // Start invisibility
        SetInvisibility(true);

        // Wait for invisibility duration
        yield return new WaitForSeconds(invisibilityDuration);

        // End invisibility
        SetInvisibility(false);
    }

    void SetInvisibility(bool invisible)
    {
        isInvisible = invisible;

        Debug.Log($"Setting invisibility to: {invisible}"); // Debug line to check if method is called

        for (int i = 0; i < playerRenderers.Length; i++)
        {
            if (playerRenderers[i] != null)
            {
                // Simple approach: just disable/enable the renderer
                playerRenderers[i].enabled = !invisible;
            }
        }

        // Alternative: If you want partial transparency instead of full invisibility
        /*
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            if (playerRenderers[i] != null)
            {
                Material mat = playerRenderers[i].material;
                if (mat.HasProperty("_Color"))
                {
                    Color color = mat.color;
                    color.a = invisible ? 0.1f : 1f;
                    mat.color = color;
                }
            }
        }
        */
    }

    IEnumerator DashCooldownTimer()
    {
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    private void HandleMovement()
    {
        isGrounded = controller.isGrounded;
        Vector2 moveInput = input.MoveInput;
        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y);

        if (move.magnitude >= 0.1f)
        {
            // Rotate relative to camera
            float targetAngle = Mathf.Atan2(move.x, move.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            Quaternion rotation = Quaternion.Euler(0, targetAngle, 0);
            transform.rotation = rotation;

            Vector3 moveDir = rotation * Vector3.forward;
            float speed = input.SprintHeld ? sprintSpeed : walkSpeed;
            controller.Move(moveDir.normalized * speed * Time.deltaTime);

            currentState = input.SprintHeld ? PlayerState.Sprinting : PlayerState.Walking;
        }
        else
        {
            currentState = PlayerState.Idle;
        }
    }

    private void HandleGravity()
    {
        // Don't apply gravity during dash
        if (isDashing) return;

        if (isGrounded && velocity.y < 0)
            velocity.y = -2f; // keeps player grounded

        velocity.y += gravity * Time.deltaTime;
    }

    private void HandleJump()
    {
        if (isGrounded && input.JumpPressed)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            currentState = PlayerState.Jumping;
        }
    }

    // Public getters for other systems
    public bool IsInvisible => isInvisible;
    public bool IsDashing => isDashing;
    public bool CanDash => canDash;
    public bool HasUsedAirDash => hasUsedAirDash;
    public float DashCooldownRemaining => canDash ? 0f : dashCooldown - (Time.time % dashCooldown);
}

public enum PlayerState
{
    Idle,
    Walking,
    Sprinting,
    Jumping,
    Falling,
    Crouching,
    Dashing
}