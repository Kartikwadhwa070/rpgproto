using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    [Header("Input Settings")]
    public KeyCode sprintKey = KeyCode.LeftControl; // Changed to Ctrl for sprinting
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode dashKey = KeyCode.LeftShift; // Shift for instant dash

    // Input properties
    public Vector2 MoveInput { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool DashPressed { get; private set; }

    void Update()
    {
        HandleMovementInput();
        HandleSprintInput();
        HandleJumpInput();
        HandleDashInput();
    }

    void HandleMovementInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        MoveInput = new Vector2(horizontal, vertical).normalized;
    }

    void HandleSprintInput()
    {
        SprintHeld = Input.GetKey(sprintKey);
    }

    void HandleJumpInput()
    {
        JumpPressed = Input.GetKeyDown(jumpKey);
    }

    void HandleDashInput()
    {
        // Instant dash on shift press
        DashPressed = Input.GetKeyDown(dashKey);
    }
}