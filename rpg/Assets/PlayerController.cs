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
    private Vector3 dashDirection;
    private float dashTimer = 0f;
    private Vector3 dashStartPosition;

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
        if (input.DashPressed && canDash && !isDashing && isGrounded)
        {
            StartDash();
        }
    }

    void StartDash()
    {
        isDashing = true;
        canDash = false;
        dashTimer = 0f;
        currentState = PlayerState.Dashing;

        // Determine dash direction
        Vector2 moveInput = input.MoveInput;
        Vector3 inputDirection = new Vector3(moveInput.x, 0, moveInput.y);

        if (inputDirection.magnitude >= 0.1f)
        {
            // Dash in movement direction relative to camera
            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            dashDirection = Quaternion.Euler(0, targetAngle, 0) * Vector3.forward;
        }
        else
        {
            // Dash forward if no input
            dashDirection = transform.forward;
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
            currentState = PlayerState.Idle;
            return;
        }

        // Calculate dash movement using animation curve
        float curveValue = dashCurve.Evaluate(normalizedTime);
        Vector3 targetPosition = dashStartPosition + dashDirection * dashDistance;
        Vector3 currentTargetPos = Vector3.Lerp(dashStartPosition, targetPosition, curveValue);

        // Calculate movement delta
        Vector3 movement = currentTargetPos - transform.position;

        // Apply movement (without gravity during dash)
        controller.Move(movement);

        // Reset vertical velocity during dash to prevent gravity buildup
        velocity.y = 0f;
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