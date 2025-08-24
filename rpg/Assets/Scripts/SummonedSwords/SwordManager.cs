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

    [Header("Battle Mode Settings")]
    public KeyCode battleModeToggleKey = KeyCode.X;

    [Header("References")]
    public Camera playerCamera;
    public Transform shootOrigin;
    public CameraController cameraController;

    private List<SwordController> swords = new List<SwordController>();
    private float rotationAngle = 0f;
    private bool isBattleMode = false;
    private bool isPerformingEntry = false;

    private SwordEntryManager entryManager;
    private SwordShootingSystem shootingSystem;

    // Public properties for other components to access
    public bool IsBattleMode => isBattleMode;
    public bool IsPerformingEntry => isPerformingEntry;
    public List<SwordController> Swords => swords;
    public float FloatDistance => floatDistance;
    public float FloatHeight => floatHeight;

    void Start()
    {
        InitializeReferences();
        InitializeComponents();
        SetBattleMode(false, true); // Start with swords hidden
    }

    void Update()
    {
        HandleBattleModeInput();
        if (!isPerformingEntry)
        {
            UpdateSwordPositions();
        }
        HandleShootingInput();
    }

    void InitializeReferences()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (shootOrigin == null)
            shootOrigin = playerCamera.transform;

        if (cameraController == null)
            cameraController = playerCamera.GetComponent<CameraController>();
    }

    void InitializeComponents()
    {
        entryManager = gameObject.AddComponent<SwordEntryManager>();
        entryManager.Initialize(this);

        shootingSystem = gameObject.AddComponent<SwordShootingSystem>();
        shootingSystem.Initialize(this, playerCamera, cameraController);
    }

    void HandleBattleModeInput()
    {
        if (Input.GetKeyDown(battleModeToggleKey) && !isPerformingEntry)
        {
            ToggleBattleMode();
        }
    }

    void HandleShootingInput()
    {
        if (!isBattleMode || isPerformingEntry) return;

        if (Input.GetMouseButtonDown(1)) // Right click
        {
            shootingSystem.TryShoot();
        }
    }

    void ToggleBattleMode()
    {
        SetBattleMode(!isBattleMode, false);
        Debug.Log($"Battle Mode: {(isBattleMode ? "ON" : "OFF")}");
    }

    public void SetBattleMode(bool enabled, bool immediate = false)
    {
        if (isBattleMode == enabled && !immediate) return;

        isBattleMode = enabled;

        if (immediate)
        {
            if (enabled)
            {
                CreateAllSwords();
                foreach (SwordController sword in swords)
                {
                    sword.SetBattleMode(enabled, 1f);
                }
            }
            else
            {
                foreach (SwordController sword in swords)
                {
                    sword.SetBattleMode(enabled, 0f);
                }
            }
        }
        else
        {
            if (enabled)
            {
                CreateAllSwords();
                StartCoroutine(entryManager.PerformDramaticEntry(() => isPerformingEntry = false));
                isPerformingEntry = true;
            }
            else
            {
                StartCoroutine(entryManager.PerformDramaticExit(() => {
                    isPerformingEntry = false;
                    ClearSwords();
                }));
                isPerformingEntry = true;
            }
        }
    }

    void CreateAllSwords()
    {
        if (swords.Count >= swordCount) return;

        ClearSwords();

        Vector3 spawnPosition = transform.position + Vector3.up * 7f;

        for (int i = 0; i < swordCount; i++)
        {
            GameObject swordObj = Instantiate(swordPrefab, spawnPosition, Quaternion.identity);
            SwordController sword = swordObj.GetComponent<SwordController>();

            if (sword == null)
                sword = swordObj.AddComponent<SwordController>();

            sword.Initialize(this, i);
            swords.Add(sword);
        }
    }

    void ClearSwords()
    {
        for (int i = 0; i < swords.Count; i++)
        {
            if (swords[i] != null && swords[i].gameObject != null)
            {
                Destroy(swords[i].gameObject);
            }
        }
        swords.Clear();
    }

    void UpdateSwordPositions()
    {
        if (!isBattleMode) return;

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

    public SwordController GetAvailableSword()
    {
        foreach (SwordController sword in swords)
        {
            if (sword.IsFloating)
                return sword;
        }
        return null;
    }
}