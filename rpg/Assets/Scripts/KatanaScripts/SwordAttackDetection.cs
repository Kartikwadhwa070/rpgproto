using UnityEngine;
using System.Collections.Generic;

public class SwordAttackDetection : MonoBehaviour
{
    private MeleeSwordSystem swordSystem;
    private float attackRange;
    private float attackAngle;
    private LayerMask enemyLayer;
    private string enemyTag;

    [Header("Attack Settings")]
    public float damage = 20f; // <-- NEW: damage per hit

    [Header("Debug Visualization")]
    public bool showDebugGizmos = true;
    public Color gizmoColor = Color.red;

    public void Initialize(MeleeSwordSystem system, float range, float angle, LayerMask layer, string tag)
    {
        swordSystem = system;
        attackRange = range;
        attackAngle = angle;
        enemyLayer = layer;
        enemyTag = tag;
    }

    /// <summary>
    /// Detect enemies and apply damage
    /// </summary>
    public void PerformAttack()
    {
        List<Collider> hitEnemies = DetectEnemies();

        foreach (Collider enemy in hitEnemies)
        {
            EnemyHP hp = enemy.GetComponent<EnemyHP>();
            if (hp != null)
            {
                hp.TakeDamage(damage);
            }
        }
    }

    public List<Collider> DetectEnemies()
    {
        List<Collider> hitEnemies = new List<Collider>();

        // Get attack origin and direction
        Transform attackOrigin = swordSystem.AttackOrigin;
        Vector3 attackDirection = attackOrigin.forward;
        Vector3 attackPosition = attackOrigin.position;

        // Find all colliders in range
        Collider[] nearbyColliders = Physics.OverlapSphere(attackPosition, attackRange, enemyLayer);

        foreach (Collider collider in nearbyColliders)
        {
            if (!collider.CompareTag(enemyTag)) continue;

            if (IsWithinAttackCone(attackPosition, attackDirection, collider.transform.position))
            {
                if (HasLineOfSight(attackPosition, collider.transform.position))
                {
                    hitEnemies.Add(collider);
                }
            }
        }

        return hitEnemies;
    }

    bool IsWithinAttackCone(Vector3 attackPosition, Vector3 attackDirection, Vector3 enemyPosition)
    {
        Vector3 directionToEnemy = (enemyPosition - attackPosition).normalized;
        float angleToEnemy = Vector3.Angle(attackDirection, directionToEnemy);
        return angleToEnemy <= attackAngle * 0.5f;
    }

    bool HasLineOfSight(Vector3 attackPosition, Vector3 enemyPosition)
    {
        Vector3 directionToEnemy = enemyPosition - attackPosition;
        float distanceToEnemy = directionToEnemy.magnitude;

        RaycastHit hit;
        if (Physics.Raycast(attackPosition, directionToEnemy.normalized, out hit, distanceToEnemy))
        {
            return hit.collider.CompareTag(enemyTag);
        }

        return true;
    }


    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos || swordSystem == null) return;

        Transform attackOrigin = swordSystem.AttackOrigin;
        if (attackOrigin == null) return;

        Gizmos.color = gizmoColor;

        // Draw attack range sphere
        Gizmos.DrawWireSphere(attackOrigin.position, attackRange);

        // Draw attack cone
        Vector3 forward = attackOrigin.forward;
        Vector3 right = attackOrigin.right;
        Vector3 up = attackOrigin.up;

        // Calculate cone edges
        float halfAngle = attackAngle * 0.5f * Mathf.Deg2Rad;

        // Draw cone lines
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 coneDirection = forward;

            // Rotate around the forward axis
            Vector3 rotatedRight = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
            coneDirection = (forward + rotatedRight * Mathf.Tan(halfAngle)).normalized;

            Gizmos.DrawLine(attackOrigin.position, attackOrigin.position + coneDirection * attackRange);
        }

        // Draw cone circle at max range
        Vector3 coneCenter = attackOrigin.position + forward * attackRange;
        float coneRadius = attackRange * Mathf.Tan(halfAngle);

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);

        // Draw cone circle (simplified)
        for (int i = 0; i < 16; i++)
        {
            float angle1 = i * 22.5f * Mathf.Deg2Rad;
            float angle2 = (i + 1) * 22.5f * Mathf.Deg2Rad;

            Vector3 point1 = coneCenter + (right * Mathf.Cos(angle1) + up * Mathf.Sin(angle1)) * coneRadius;
            Vector3 point2 = coneCenter + (right * Mathf.Cos(angle2) + up * Mathf.Sin(angle2)) * coneRadius;

            Gizmos.DrawLine(point1, point2);
        }
    }

    // Method to get enemies in range for UI/other systems
    public List<Collider> GetEnemiesInRange()
    {
        List<Collider> enemiesInRange = new List<Collider>();

        if (swordSystem.AttackOrigin == null) return enemiesInRange;

        Collider[] nearbyColliders = Physics.OverlapSphere(
            swordSystem.AttackOrigin.position,
            attackRange,
            enemyLayer
        );

        foreach (Collider collider in nearbyColliders)
        {
            if (collider.CompareTag(enemyTag))
            {
                enemiesInRange.Add(collider);
            }
        }

        return enemiesInRange;
    }

    // Method to check if specific enemy is in attack range
    public bool IsEnemyInRange(Transform enemy)
    {
        if (swordSystem.AttackOrigin == null || enemy == null) return false;

        float distance = Vector3.Distance(swordSystem.AttackOrigin.position, enemy.position);
        return distance <= attackRange &&
               IsWithinAttackCone(swordSystem.AttackOrigin.position, swordSystem.AttackOrigin.forward, enemy.position);
    }
}