using UnityEngine;

public class Force60FPS : MonoBehaviour
{
    void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 240; 
    }
}
