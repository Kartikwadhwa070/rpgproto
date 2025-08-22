using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SwordManager : MonoBehaviour
{
    [Header("Sword Settings")]
    public GameObject swordPrefab;
    public int maxSwords = 6;
    public float orbitRadius = 2f;
    public float orbitSpeed = 50f;
    public float shootForce = 40f;
    public float cooldown = 0.5f;   // time between shots
    public float respawnDelay = 3f; // time for sword to return

    [Header("References")]
    public Transform orbitCenter; // empty object on player's back
    public Camera playerCamera;

    private List<GameObject> swords = new List<GameObject>();
    private bool canShoot = true;

    void Start()
    {
        // Spawn swords
        for (int i = 0; i < maxSwords; i++)
        {
            GameObject sword = Instantiate(swordPrefab, orbitCenter.position, Quaternion.identity, orbitCenter);
            swords.Add(sword);
        }
    }

    void Update()
    {
        OrbitSwords();

        if (Input.GetMouseButtonDown(1) && canShoot)
        {
            ShootSword();
        }
    }

    void OrbitSwords()
    {
        float angleStep = 360f / maxSwords;
        for (int i = 0; i < swords.Count; i++)
        {
            if (swords[i] == null) continue;

            float angle = (Time.time * orbitSpeed + i * angleStep) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0.5f, Mathf.Sin(angle)) * orbitRadius;

            swords[i].transform.localPosition = offset;
            swords[i].transform.localRotation = Quaternion.LookRotation(-offset);
        }
    }

    void ShootSword()
    {
        if (swords.Count == 0) return;

        // Take the first available sword
        GameObject sword = swords[0];
        swords.RemoveAt(0);

        // Detach from orbit
        sword.transform.parent = null;
        Rigidbody rb = sword.GetComponent<Rigidbody>();
        if (rb == null) rb = sword.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero; // reset in case reused

        // Raycast from crosshair
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 targetDir = ray.direction;

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            targetDir = (hit.point - sword.transform.position).normalized;
        }

        rb.AddForce(targetDir * shootForce, ForceMode.Impulse);

        // Destroy sword after impact or respawn
        StartCoroutine(RespawnSword(sword));

        // Apply cooldown
        StartCoroutine(ShootCooldown());
    }

    IEnumerator ShootCooldown()
    {
        canShoot = false;
        yield return new WaitForSeconds(cooldown);
        canShoot = true;
    }

    IEnumerator RespawnSword(GameObject oldSword)
    {
        yield return new WaitForSeconds(respawnDelay);

        if (orbitCenter != null && swordPrefab != null)
        {
            GameObject newSword = Instantiate(swordPrefab, orbitCenter.position, Quaternion.identity, orbitCenter);
            swords.Add(newSword);
        }

        // cleanup old sword
        if (oldSword != null) Destroy(oldSword);
    }
}
