using UnityEngine;

/// <summary>
/// Optional one-shot scene camera cleaner.
/// Attach to Maverick_Manager if TargetingPodCameraAutoSetup or duplicate cameras keep hijacking Game view.
/// </summary>
public class MaverickCameraSceneCleaner : MonoBehaviour
{
    public Camera mainCamera;
    public bool disableTargetingPodScreenCameras = true;
    public bool setMainCameraDepthHigh = true;

    private void Start()
    {
        Clean();
    }

    [ContextMenu("Clean Cameras Now")]
    public void Clean()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera != null && setMainCameraDepthHigh)
        {
            mainCamera.enabled = true;
            mainCamera.depth = 10;
        }

        Camera[] cams = FindObjectsOfType<Camera>(true);
        foreach (Camera cam in cams)
        {
            if (cam == null || cam == mainCamera)
                continue;

            if (cam.targetTexture != null)
                continue;

            if (!disableTargetingPodScreenCameras)
                continue;

            string n = cam.name.ToLowerInvariant();
            if (n.Contains("targeting") || n.Contains("pod") || n.Contains("tgp"))
            {
                cam.enabled = false;
            }
        }
    }
}
