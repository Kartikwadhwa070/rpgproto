using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    public Transform target;
    public Vector3 offset = new Vector3(0, 3, -6);
    public float smoothSpeed = 10f;
    public float mouseSensitivity = 120f;

    [Header("Zoom Settings")]
    public float distance = 6f;
    public float minDistance = 2f;
    public float maxDistance = 12f;
    public float zoomSpeed = 2f;

    public float minY = -20f;
    public float maxY = 60f;

    private float yaw;
    private float pitch;
    private bool cursorLocked = false;

    void Update()
    {
        HandleCursorLock();
        HandleZoom();
    }

    void LateUpdate()
    {
        if (cursorLocked)
        {
            HandleRotation();
            HandlePosition();
        }
    }

    void HandleCursorLock()
    {
        if (Input.GetMouseButtonDown(0)) // Left click
        {
            LockCursor();
        }
        else if (Input.GetKeyDown(KeyCode.Escape)) // Esc to release
        {
            UnlockCursor();
        }
    }

    void LockCursor()
    {
        cursorLocked = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UnlockCursor()
    {
        cursorLocked = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance -= scroll * zoomSpeed;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }
    }

    void HandleRotation()
    {
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minY, maxY);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void HandlePosition()
    {
        Vector3 desiredPosition = target.position - transform.forward * distance + Vector3.up * offset.y;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }
}
