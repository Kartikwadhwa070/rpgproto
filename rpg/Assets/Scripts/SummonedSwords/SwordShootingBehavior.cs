using UnityEngine;

public class SwordShootingBehavior : MonoBehaviour
{
    private SwordMovementController movementController;
    private Vector3 shootTarget;
    private float shootSpeed;
    private float shootRotationSpeed;
    private bool isShooting = false;
    private Quaternion initialShootRotation;

    public bool IsShooting => isShooting;

    public void Initialize(SwordMovementController controller)
    {
        movementController = controller;
    }

    public void StartShooting(Vector3 target, float speed, float tiltAngle, float rotSpeed)
    {
        isShooting = true;
        shootTarget = target;
        shootSpeed = speed;
        shootRotationSpeed = rotSpeed;

        SetupShootingRotation(target, tiltAngle);
        EnableColliderForShooting();
    }

    void SetupShootingRotation(Vector3 target, float tiltAngle)
    {
        Vector3 direction = (target - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        Quaternion tiltRotation = Quaternion.AngleAxis(-tiltAngle, Vector3.right);
        initialShootRotation = lookRotation * tiltRotation;
        transform.rotation = initialShootRotation;
    }

    void EnableColliderForShooting()
    {
        Collider swordCollider = GetComponent<Collider>();
        if (swordCollider != null)
        {
            swordCollider.enabled = true;
            swordCollider.isTrigger = true;
        }
    }

    public void UpdateShooting()
    {
        if (!isShooting) return;

        MoveTowardsTarget();
        ApplySpinRotation();
        CheckTargetReached();
    }

    void MoveTowardsTarget()
    {
        Vector3 direction = (shootTarget - transform.position).normalized;
        transform.position += direction * shootSpeed * Time.deltaTime;
    }

    void ApplySpinRotation()
    {
        if (shootRotationSpeed > 0)
        {
            Quaternion spinRotation = Quaternion.AngleAxis(shootRotationSpeed * Time.deltaTime, transform.forward);
            transform.rotation = spinRotation * transform.rotation;
        }
    }

    void CheckTargetReached()
    {
        if (Vector3.Distance(transform.position, shootTarget) < 0.5f)
        {
            OnReachTarget();
            StopShooting();
        }
    }

    void OnReachTarget()
    {
        // Check for enemy hits in the area
        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, 1f);
        foreach (Collider col in hitEnemies)
        {
            if (col.CompareTag("Enemy"))
            {
                EnemyHealth enemyHealth = col.GetComponent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.TakeDamage(25f);
                }
                Debug.Log("Sword hit enemy: " + col.name);
                break;
            }
        }
    }

    public void StopShooting()
    {
        isShooting = false;
        ResetCollider();
    }

    void ResetCollider()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (isShooting && other.CompareTag("Enemy"))
        {
            Debug.Log("Sword hit: " + other.name);
        }
    }
}