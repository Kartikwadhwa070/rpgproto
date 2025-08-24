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
    public float rotationSpeed = 10f; // Add this for smooth rotation

    [Header("Dash Settings")]
    public float dashDistance = 10f;
    public float dashDuration = 0.3f;
    public float dashCooldown = 2f;
    public float invisibilityDuration = 0.5f;
    public float verticalDashHeight = 8f;
    public bool allowAirDash = true;
    public AnimationCurve dashCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("References")]
    public Transform cameraTransform;

    [Header("Visual Effects")]
    public Renderer[] playerRenderers;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private PlayerInputHandler input;
    private PlayerState currentState = PlayerState.Idle;

    // Dash variables
    private bool isDashing = false;
    private bool canDash = true;
    private bool hasUsedAirDash = false;
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
        if (playerRenderers.Length == 0)
        {
            playerRenderers = GetComponentsInChildren<Renderer>();
            Debug.Log($"Auto-found {playerRenderers.Length} renderers for invisibility");
        }

        for (int i = 0; i < playerRenderers.Length; i++)
        {
            Debug.Log($"Renderer {i}: {playerRenderers[i].name}");
        }
    }

    void Update()
    {
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
    }

    void HandleDashInput()
    {
        if (input.DashPressed && canDash && !isDashing)
        {
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

        if (!isGrounded)
        {
            hasUsedAirDash = true;
        }

        Vector2 moveInput = input.MoveInput;
        Vector3 inputDirection = new Vector3(moveInput.x, 0, moveInput.y);

        if (inputDirection.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            dashDirection = Quaternion.Euler(0, targetAngle, 0) * Vector3.forward;
        }
        else
        {
            dashDirection = Vector3.up;
            isVerticalDash = true;
            Debug.Log("Vertical dash activated!");
        }

        dashStartPosition = transform.position;
        StartCoroutine(HandleInvisibility());
        StartCoroutine(DashCooldownTimer());
    }

    void HandleDash()
    {
        dashTimer += Time.deltaTime;
        float normalizedTime = dashTimer / dashDuration;

        if (normalizedTime >= 1f)
        {
            isDashing = false;
            dashTimer = 0f;

            if (isVerticalDash)
            {
                velocity.y = -2f;
            }
            else
            {
                if (velocity.y > 0f)
                {
                    velocity.y = 0f;
                }
            }

            isVerticalDash = false;
            currentState = PlayerState.Idle;
            return;
        }

        float curveValue = dashCurve.Evaluate(normalizedTime);

        Vector3 targetPosition;
        if (isVerticalDash)
        {
            targetPosition = dashStartPosition + Vector3.up * verticalDashHeight;
        }
        else
        {
            targetPosition = dashStartPosition + dashDirection * dashDistance;
        }

        Vector3 currentTargetPos = Vector3.Lerp(dashStartPosition, targetPosition, curveValue);
        Vector3 movement = currentTargetPos - transform.position;
        controller.Move(movement);

        if (isVerticalDash)
        {
            velocity.y = 0f;
        }
        else
        {
            if (!isGrounded)
            {
                velocity.y = Mathf.Max(velocity.y, -5f);
            }
            else
            {
                velocity.y = 0f;
            }
        }
    }

    IEnumerator HandleInvisibility()
    {
        SetInvisibility(true);
        yield return new WaitForSeconds(invisibilityDuration);
        SetInvisibility(false);
    }

    void SetInvisibility(bool invisible)
    {
        isInvisible = invisible;
        Debug.Log($"Setting invisibility to: {invisible}");

        for (int i = 0; i < playerRenderers.Length; i++)
        {
            if (playerRenderers[i] != null)
            {
                playerRenderers[i].enabled = !invisible;
            }
        }
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
            // Calculate target rotation
            float targetAngle = Mathf.Atan2(move.x, move.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            Quaternion targetRotation = Quaternion.Euler(0, targetAngle, 0);

            // SMOOTH ROTATION - This fixes the teleporting issue
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            Vector3 moveDir = targetRotation * Vector3.forward;
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
        if (isDashing) return;

        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

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

    // Public getters
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