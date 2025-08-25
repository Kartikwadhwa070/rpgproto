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

    void Start()
    {
        // Initialize rotation based on current transform
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;

        // Lock cursor by default for better gameplay experience
        LockCursor();
    }

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
        }
        HandlePosition();
    }

    void HandleCursorLock()
    {
        // Left click or right click to lock cursor (so sword shooting works)
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            if (!cursorLocked)
            {
                LockCursor();
            }
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            UnlockCursor();
        }

        // Alternative: Tab key to toggle cursor lock
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (cursorLocked)
                UnlockCursor();
            else
                LockCursor();
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
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        yaw += mouseX * mouseSensitivity * Time.deltaTime;
        pitch -= mouseY * mouseSensitivity * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minY, maxY);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void HandlePosition()
    {
        Vector3 desiredPosition = target.position - transform.forward * distance + Vector3.up * offset.y;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }

    // Public method for other scripts to check if camera is ready for shooting
    public bool IsReadyForShooting()
    {
        return cursorLocked;
    }

    // Public method to get the current look direction for shooting
    public Ray GetCenterRay()
    {
        return new Ray(transform.position, transform.forward);
    }
}