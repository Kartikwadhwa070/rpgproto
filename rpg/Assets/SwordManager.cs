using UnityEnusing UnityEngine;
using System.Collections.Generic;

public class SwordManager : MonoBehaviour
{
    [Header("Sword Settings")]
    public GameObject swordPrefab;
    public int maxSwords = 6;
    public float orbitRadius = 2f;
    public float orbitSpeed = 50f;

    [Header("References")]
    public Transform orbitCenter; // usually your player’s back
    public Camera playerCamera;

    private List<GameObject> swords = new List<GameObject>();

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

        if (Input.GetMouseButtonDown(1)) // Right click
        {
            ShootSword();
        }
    }

    void OrbitSwords()
    {
        float angleStep = 360f / swords.Count;
        for (int i = 0; i < swords.Count; i++)
        {
            if (swords[i] == null) continue;

            float angle = (Time.time * orbitSpeed + i * angleStep) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0.5f, Mathf.Sin(angle)) * orbitRadius;

            swords[i].transform.localPosition = offset;
            swords[i].transform.localRotation = Quaternion.LookRotation(-offset); // face outward
        }
    }

    void ShootSword()
    {
        if (swords.Count == 0) return;

        // Take first sword
        GameObject sword = swords[0];
        swords.RemoveAt(0);

        sword.transform.parent = null; // detach from orbit
        Rigidbody rb = sword.GetComponent<Rigidbody>();
        if (rb == null) rb = sword.AddComponent<Rigidbody>();

        // Get direction from crosshair (middle of screen)
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 dir = ray.direction;

        rb.isKinematic = false;
        rb.AddForce(dir * 40f, ForceMode.Impulse); // tweak force

        // Destroy after some time
        Destroy(sword, 5f);
    }
}
