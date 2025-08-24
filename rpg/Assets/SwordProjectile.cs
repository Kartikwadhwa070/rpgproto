using UnityEngine;

public class SwordController : MonoBehaviour
{
    private FloatingSwordSystem swordSystem;
    private int swordIndex;
    private bool isFloating = true;
    private bool isInEntryMode = false;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 entryPosition;
    private Quaternion entryRotation;

    // Battle mode variables
    private bool battleModeActive = false;
    private float currentVisibility = 1f;

    // Components
    private SwordVisibilityController visibilityController;
    private SwordMovementController movementController;

    public bool IsFloating => isFloating;
    public bool IsInEntryMode => isInEntryMode;

    void Awake()
    {
        InitializeComponents();
    }

    void Update()
    {
        movementController.UpdateMovement();
    }

    void InitializeComponents()
    {
        visibilityController = gameObject.AddComponent<SwordVisibilityController>();
        movementController = gameObject.AddComponent<SwordMovementController>();

        visibilityController.Initialize(this);
        movementController.Initialize(this);
    }

    public void Initialize(FloatingSwordSystem system, int index)
    {
        swordSystem = system;
        swordIndex = index;

        SetupPhysics();
    }

    void SetupPhysics()
    {
        // Add and configure rigidbody
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.useGravity = false;
        rb.isKinematic = true;

        // Add and configure collider
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            col = gameObject.AddComponent<CapsuleCollider>();
        }
        col.isTrigger = true;
    }

    public void SetBattleMode(bool enabled, float visibility)
    {
        battleModeActive = enabled;
        currentVisibility = visibility;
        visibilityController.UpdateVisibility(enabled, visibility);
    }

    public void SetFloatingPosition(Vector3 position)
    {
        targetPosition = position;
    }

    public void SetFloatingRotation(Quaternion rotation)
    {
        targetRotation = rotation;
    }

    public void SetEntryPosition(Vector3 position)
    {
        entryPosition = position;
        isInEntryMode = true;
    }

    public void SetEntryRotation(Quaternion rotation)
    {
        entryRotation = rotation;
    }

    public void SetFloatingMode(bool enabled)
    {
        isInEntryMode = !enabled;
        isFloating = enabled;
    }

    // Properties for other components to access
    public Vector3 TargetPosition => targetPosition;
    public Quaternion TargetRotation => targetRotation;
    public Vector3 EntryPosition => entryPosition;
    public Quaternion EntryRotation => entryRotation;
    public bool BattleModeActive => battleModeActive;
    public float CurrentVisibility => currentVisibility;
}