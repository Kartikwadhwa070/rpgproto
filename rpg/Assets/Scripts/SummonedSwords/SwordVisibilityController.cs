using UnityEngine;

public class SwordVisibilityController : MonoBehaviour
{
    private SwordController swordController;
    private Renderer[] swordRenderers;
    private Material[] originalMaterials;
    private Material[] fadeMaterials;

    public void Initialize(SwordController controller)
    {
        swordController = controller;
        SetupVisibilitySystem();
    }

    void SetupVisibilitySystem()
    {
        swordRenderers = GetComponentsInChildren<Renderer>();

        if (swordRenderers.Length > 0)
        {
            originalMaterials = new Material[swordRenderers.Length];
            fadeMaterials = new Material[swordRenderers.Length];

            for (int i = 0; i < swordRenderers.Length; i++)
            {
                if (swordRenderers[i] != null && swordRenderers[i].material != null)
                {
                    originalMaterials[i] = swordRenderers[i].material;
                    fadeMaterials[i] = CreateFadeMaterial(originalMaterials[i]);
                }
            }
        }
    }

    Material CreateFadeMaterial(Material originalMaterial)
    {
        Material fadeMaterial = new Material(originalMaterial);

        // Make material support transparency
        if (fadeMaterial.HasProperty("_Mode"))
        {
            fadeMaterial.SetFloat("_Mode", 2); // Fade mode
            fadeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            fadeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            fadeMaterial.SetInt("_ZWrite", 0);
            fadeMaterial.DisableKeyword("_ALPHATEST_ON");
            fadeMaterial.EnableKeyword("_ALPHABLEND_ON");
            fadeMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            fadeMaterial.renderQueue = 3000;
        }

        return fadeMaterial;
    }

    public void UpdateVisibility(bool battleModeActive, float visibility)
    {
        UpdateRendererVisibility(visibility);
        UpdateColliderVisibility(battleModeActive, visibility);
    }

    void UpdateRendererVisibility(float visibility)
    {
        for (int i = 0; i < swordRenderers.Length; i++)
        {
            if (swordRenderers[i] == null) continue;

            if (visibility <= 0f)
            {
                swordRenderers[i].enabled = false;
            }
            else
            {
                swordRenderers[i].enabled = true;

                if (visibility >= 1f)
                {
                    // Fully visible - use original material
                    if (originalMaterials[i] != null)
                    {
                        swordRenderers[i].material = originalMaterials[i];
                    }
                }
                else
                {
                    // Partially visible - use fade material
                    if (fadeMaterials[i] != null)
                    {
                        swordRenderers[i].material = fadeMaterials[i];
                        Color color = fadeMaterials[i].color;
                        color.a = visibility;
                        fadeMaterials[i].color = color;
                    }
                }
            }
        }
    }

    void UpdateColliderVisibility(bool battleModeActive, float visibility)
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = battleModeActive && visibility > 0.5f;
            col.isTrigger = true; // Always keep as trigger
        }
    }
}