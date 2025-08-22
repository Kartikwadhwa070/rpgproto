using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool SprintHeld { get; private set; }

    void Update()
    {
        MoveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        JumpPressed = Input.GetButtonDown("Jump");
        SprintHeld = Input.GetKey(KeyCode.LeftShift);
    }
}
