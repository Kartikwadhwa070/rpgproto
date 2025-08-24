using UnityEngine;

public class SwordMovementController : MonoBehaviour
{
    private SwordController swordController;
    private SwordShootingBehavior shootingBehavior;

    public void Initialize(SwordController controller)
    {
        swordController = controller;
        shootingBehavior = gameObject.AddComponent<SwordShootingBehavior>();
        shootingBehavior.Initialize(this);
    }

    public void UpdateMovement()
    {
        if (shootingBehavior.IsShooting)
        {
            shootingBehavior.UpdateShooting();
        }
        else if (swordController.IsInEntryMode)
        {
            MoveToEntryPosition();
        }
        else if (swordController.IsFloating)
        {
            MoveToFloatingPosition();
        }
    }

    void MoveToEntryPosition()
    {
        transform.position = Vector3.Lerp(transform.position, swordController.EntryPosition, Time.deltaTime * 8f);
        if (swordController.EntryRotation != Quaternion.identity)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, swordController.EntryRotation, Time.deltaTime * 8f);
        }
    }

    void MoveToFloatingPosition()
    {
        if (!swordController.BattleModeActive && swordController.CurrentVisibility <= 0f) return;

        transform.position = Vector3.Lerp(transform.position, swordController.TargetPosition, Time.deltaTime * 5f);
        transform.rotation = Quaternion.Lerp(transform.rotation, swordController.TargetRotation, Time.deltaTime * 5f);
    }

    public void StartShooting(Vector3 target, float speed, float tiltAngle, float rotSpeed)
    {
        swordController.SetFloatingMode(false);
        shootingBehavior.StartShooting(target, speed, tiltAngle, rotSpeed);
    }

    public void StopShooting()
    {
        shootingBehavior.StopShooting();
        swordController.SetFloatingMode(true);
    }

    public bool IsShooting => shootingBehavior.IsShooting;
}