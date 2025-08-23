using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FloatingSwordSystem : MonoBehaviour
{
    [Header("Sword Settings")]
    public GameObject swordPrefab;
    public int swordCount = 6;
    public float floatDistance = 2f;
    public float floatHeight = 0.5f;
    [Header("Visual Settings")]
    public float rotationSpeed = 30f;
    public float bobSpeed = 2f;
    public float bobAmount = 0.2f;
    public float shootRotationSpeed = 180f; // Rotation speed during flight

    [Header("Shooting Settings")]
    public float shootSpeed = 20f;
    public float shootRange = 100f;
    public float swordReturnDelay = 3f;
    public float shootTiltAngle = 45f; // Angle to tilt sword forward when shooting
    public LayerMask enemyLayer = -1;
    public string enemyTag = "Enemy";

    [Header("References")]
    public Camera playerCamera;
    public Transform shootOrigin;
    public CameraController cameraController;

    private List<SwordController> swords = new List<SwordController>();
    private float rotationAngle = 0f;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (shootOrigin == null)
            shootOrigin = playerCamera.transform;

        if (cameraController == null)
            cameraController = playerCamera.GetComponent<CameraController>();

        CreateSwords();
    }

    void Update()
    {
        UpdateSwordPositions();
        HandleInput();
    }

    void CreateSwords()
    {
        for (int i = 0; i < swordCount; i++)
        {
            GameObject swordObj = Instantiate(swordPrefab, transform.position, Quaternion.identity);
            SwordController sword = swordObj.GetComponent<SwordController>();

            if (sword == null)
                sword = swordObj.AddComponent<SwordController>();

            sword.Initialize(this, i);
            swords.Add(sword);
        }
    }

    void UpdateSwordPositions()
    {
        rotationAngle += rotationSpeed * Time.deltaTime;

        for (int i = 0; i < swords.Count; i++)
        {
            if (swords[i].IsFloating)
            {
                float angle = (360f / swordCount) * i + rotationAngle;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * floatDistance,
                    floatHeight + Mathf.Sin(Time.time * bobSpeed + i) * bobAmount,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * floatDistance
                );

                swords[i].SetFloatingPosition(transform.position + offset);
                swords[i].SetFloatingRotation(Quaternion.LookRotation(offset.normalized));
            }
        }
    }

    void HandleInput()
    {
        if (Input.GetMouseButtonDown(1)) // Right click
        {
            // Check if camera is ready for shooting (cursor locked)
            if (cameraController != null && !cameraController.IsReadyForShooting())
            {
                Debug.Log("Lock cursor first to shoot swords!");
                return;
            }

            ShootSword();
        }
    }

    void ShootSword()
    {
        SwordController availableSword = GetAvailableSword();
        if (availableSword == null) return;

        // Raycast from camera center to find target
        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        RaycastHit hit;

        Vector3 targetPoint;
        if (Physics.Raycast(ray, out hit, shootRange, enemyLayer))
        {
            targetPoint = hit.point;

            // Check if hit object is an enemy
            if (hit.collider.CompareTag(enemyTag))
            {
                // Handle enemy hit (you can expand this)
                Debug.Log("Hit enemy: " + hit.collider.name);
            }
        }
        else
        {
            targetPoint = ray.origin + ray.direction * shootRange;
        }

        // Shoot from sword's current position toward target
        availableSword.ShootFromCurrentPosition(targetPoint, shootSpeed, shootTiltAngle, shootRotationSpeed);
        StartCoroutine(ReturnSwordAfterDelay(availableSword, swordReturnDelay));
    }

    SwordController GetAvailableSword()
    {
        foreach (SwordController sword in swords)
        {
            if (sword.IsFloating)
                return sword;
        }
        return null;
    }

    IEnumerator ReturnSwordAfterDelay(SwordController sword, float delay)
    {
        yield return new WaitForSeconds(delay);
        sword.ReturnToFloat();
    }
}

public class SwordController : MonoBehaviour
{
    private FloatingSwordSystem swordSystem;
    private int swordIndex;
    private bool isFloating = true;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 shootTarget;
    private float shootSpeed;
    private float shootRotationSpeed;
    private bool isShooting = false;
    private Quaternion initialShootRotation;

    public bool IsFloating => isFloating;

    void Update()
    {
        if (isShooting)
        {
            MoveDuringShoot();
        }
        else if (isFloating)
        {
            MoveToFloatingPosition();
        }
    }

    public void Initialize(FloatingSwordSystem system, int index)
    {
        swordSystem = system;
        swordIndex = index;

        // Add a rigidbody if it doesn't exist
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.useGravity = false;
        rb.isKinematic = true;

        // Add collider if it doesn't exist
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<CapsuleCollider>();
        }
    }

    public void SetFloatingPosition(Vector3 position)
    {
        targetPosition = position;
    }

    public void SetFloatingRotation(Quaternion rotation)
    {
        targetRotation = rotation;
    }

    void MoveToFloatingPosition()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 5f);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
    }

    public void ShootFromCurrentPosition(Vector3 target, float speed, float tiltAngle, float rotSpeed)
    {
        isFloating = false;
        isShooting = true;
        shootTarget = target;
        shootSpeed = speed;
        shootRotationSpeed = rotSpeed;

        // Calculate direction to target
        Vector3 direction = (target - transform.position).normalized;

        // Create rotation that points toward target with tilt
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        // Tilt the sword forward (around the right axis) so the sharp end points forward
        Quaternion tiltRotation = Quaternion.AngleAxis(-tiltAngle, Vector3.right);
        initialShootRotation = lookRotation * tiltRotation;
        transform.rotation = initialShootRotation;

        // Add player layer to ignore collision with player
        Collider swordCollider = GetComponent<Collider>();
        if (swordCollider != null)
        {
            // Ignore collision with player temporarily
            Collider playerCollider = FindObjectOfType<CharacterController>();
            if (playerCollider == null)
                playerCollider = GameObject.FindGameObjectWithTag("Player")?.GetComponent<Collider>();

            if (playerCollider != null)
            {
                Physics.IgnoreCollision(swordCollider, playerCollider, true);
                StartCoroutine(ReenablePlayerCollisionAfterDelay(swordCollider, playerCollider, 0.5f));
            }
        }
    }

    void MoveDuringShoot()
    {
        Vector3 direction = (shootTarget - transform.position).normalized;
        transform.position += direction * shootSpeed * Time.deltaTime;

        // Add spinning rotation during flight for visual effect
        if (shootRotationSpeed > 0)
        {
            Quaternion spinRotation = Quaternion.AngleAxis(shootRotationSpeed * Time.deltaTime, transform.forward);
            transform.rotation = spinRotation * transform.rotation;
        }

        // Check if we've reached the target or passed it
        if (Vector3.Distance(transform.position, shootTarget) < 0.5f)
        {
            isShooting = false;
            // Handle hitting the target
            OnReachTarget();
        }
    }

    void OnReachTarget()
    {
        // Perform a small raycast check for enemies at target location
        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, 1f);
        foreach (Collider col in hitEnemies)
        {
            if (col.CompareTag(swordSystem.enemyTag))
            {
                EnemyHealth enemyHealth = col.GetComponent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.TakeDamage(25f);
                }
                Debug.Log("Sword hit enemy: " + col.name);
                break; // Only hit one enemy per sword
            }
        }
    }

    IEnumerator ReenablePlayerCollisionAfterDelay(Collider swordCol, Collider playerCol, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (swordCol != null && playerCol != null)
        {
            Physics.IgnoreCollision(swordCol, playerCol, false);
        }
    }

    public void ReturnToFloat()
    {
        isFloating = true;
        isShooting = false;

        // Smoothly return to floating rotation (will be handled in MoveToFloatingPosition)
        // The floating rotation will be set by the main system
    }

    void OnTriggerEnter(Collider other)
    {
        if (isShooting && other.CompareTag(swordSystem.enemyTag))
        {
            // Additional hit detection if needed
            Debug.Log("Sword hit: " + other.name);
        }
    }
}

// Optional: Simple enemy health script for testing
public class EnemyHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        Debug.Log($"{gameObject.name} took {damage} damage. Health: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log($"{gameObject.name} died!");
        // Add death effects, destroy object, etc.
        Destroy(gameObject);
    }
}