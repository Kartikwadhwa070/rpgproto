using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    public Transform target;
    public Vector3 offset = new Vector3(0, 3, -6);
    public float smoothSpeed = 10f;
    public float mouseSensitivity = 120f;
    public float distance = 6f;
    public float minY = -20f;
    public float maxY = 60f;

    private float yaw;
    private float pitch;

    void LateUpdate()
    {
        HandleRotation();
        HandlePosition();
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
