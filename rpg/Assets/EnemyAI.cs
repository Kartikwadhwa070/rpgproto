using UnityEngine;
using UnityEngine.AI;

public class BasicEnemyAI : MonoBehaviour
{
    [Header("Movement Settings")]
    public float followSpeed = 3f;
    public float rotationSpeed = 5f;
    public float stoppingDistance = 2f;

    public NavMeshAgent agent;
    public Transform player;

    void Start()
    {
        // Get NavMeshAgent component
        agent = GetComponent<NavMeshAgent>();

        // Find the player
        player = FindObjectOfType<PlayerController>()?.transform;

        if (agent != null)
        {
            agent.speed = followSpeed;
            agent.stoppingDistance = stoppingDistance;
        }

        if (player == null)
        {
            Debug.LogWarning("Player not found! Make sure PlayerController exists in the scene.");
        }
    }

    void Update()
    {
        if (player == null || agent == null) return;

        // Follow the player
        agent.SetDestination(player.position);

        // Turn towards the player
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        directionToPlayer.y = 0; // Keep rotation on horizontal plane

        if (directionToPlayer != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
}