using UnityEngine;
using System.Collections;

public class SwordShootingSystem : MonoBehaviour
{
    [Header("Shooting Settings")]
    public float shootSpeed = 20f;
    public float shootRange = 100f;
    public float swordReturnDelay = 3f;
    public float shootTiltAngle = 45f;
    public float shootRotationSpeed = 180f;
    public LayerMask enemyLayer = -1;
    public string enemyTag = "Enemy";

    private FloatingSwordSystem swordSystem;
    private Camera playerCamera;
    private CameraController cameraController;

    public void Initialize(FloatingSwordSystem system, Camera camera, CameraController camController)
    {
        swordSystem = system;
        playerCamera = camera;
        cameraController = camController;
    }

    public void TryShoot()
    {
        if (!CanShoot()) return;

        SwordController availableSword = swordSystem.GetAvailableSword();
        if (availableSword == null) return;

        Vector3 targetPoint = CalculateShootTarget();
        ShootSword(availableSword, targetPoint);
    }

    bool CanShoot()
    {
        if (cameraController != null && !cameraController.IsReadyForShooting())
        {
            Debug.Log("Lock cursor first to shoot swords!");
            return false;
        }
        return true;
    }

    Vector3 CalculateShootTarget()
    {
        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, shootRange, enemyLayer))
        {
            if (hit.collider.CompareTag(enemyTag))
            {
                Debug.Log("Hit enemy: " + hit.collider.name);
            }
            return hit.point;
        }
        else
        {
            return ray.origin + ray.direction * shootRange;
        }
    }

    void ShootSword(SwordController sword, Vector3 targetPoint)
    {
        SwordMovementController movementController = sword.GetComponent<SwordMovementController>();
        if (movementController != null)
        {
            movementController.StartShooting(targetPoint, shootSpeed, shootTiltAngle, shootRotationSpeed);
            StartCoroutine(ReturnSwordAfterDelay(movementController, swordReturnDelay));
        }
    }

    IEnumerator ReturnSwordAfterDelay(SwordMovementController movementController, float delay)
    {
        yield return new WaitForSeconds(delay);
        movementController.StopShooting();
    }
}