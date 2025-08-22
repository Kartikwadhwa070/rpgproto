using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpHeight = 2f;
    public float gravity = -9.81f;

    [Header("References")]
    public Transform cameraTransform;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    private PlayerInputHandler input;
    private PlayerState currentState = PlayerState.Idle;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<PlayerInputHandler>();
    }

    void Update()
    {
        HandleMovement();
        HandleGravity();
        HandleJump();

        controller.Move(velocity * Time.deltaTime);
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
}

public enum PlayerState
{
    Idle,
    Walking,
    Sprinting,
    Jumping,
    Falling,
    Crouching
}
