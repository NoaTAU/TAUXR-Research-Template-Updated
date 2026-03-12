using UnityEngine;

/// <summary>
/// Scene singleton: controls passthrough (OVRManager) and camera background mode.
/// Assign camera in Inspector; optionally assign OVRManager and OVRPassthroughLayer.
/// </summary>
public class PassthroughManager : ResXRSingleton<PassthroughManager>
{
    [Header("Camera (assign in Inspector)")]
    [SerializeField] private Camera _camera;

    [Header("Menu scene appearance")]
    [Tooltip("When in the menu scene: true = passthrough on, false = skybox. Applied at start and when returning from a portal.")]
    public bool menuPassthroughEnabled;

    [Header("Optional Meta XR references")]
    [SerializeField] private OVRManager _ovrManager;
    [SerializeField] private OVRPassthroughLayer _passthroughLayer;

    private CameraClearFlags _savedClearFlags;
    private Color _savedBackgroundColor;
    private bool _hasSavedCameraState;

    private void Start()
    {
        ApplyMenuPassthrough();
    }

    /// <summary>
    /// Applies the menu scene's preferred passthrough state (menuPassthroughEnabled).
    /// Call when returning to menu so the menu always appears in passthrough or skybox as configured.
    /// </summary>
    public void ApplyMenuPassthrough()
    {
        SetPassthroughEnabled(menuPassthroughEnabled);
    }

    public void SetPassthroughEnabled(bool enabled)
    {
        if (_ovrManager == null)
            _ovrManager = FindFirstObjectByType<OVRManager>();
        if (_ovrManager != null)
        {
            _ovrManager.isInsightPassthroughEnabled = enabled;
            Debug.Log($"[PassthroughManager] OVRManager passthrough set to {enabled}");
        }
        else
            Debug.LogWarning("[PassthroughManager] OVRManager not found; passthrough state not applied.");

        if (_passthroughLayer != null)
        {
            _passthroughLayer.enabled = enabled;
            Debug.Log($"[PassthroughManager] OVRPassthroughLayer.enabled = {enabled}");
        }

        if (_camera == null)
            _camera = Camera.main;
        if (_camera != null)
            ApplyCameraBackground(enabled);
        else
            Debug.LogWarning("[PassthroughManager] No camera assigned or found; camera background not updated.");
    }

    private void ApplyCameraBackground(bool passthroughOn)
    {
        if (passthroughOn)
        {
            _savedClearFlags = _camera.clearFlags;
            _savedBackgroundColor = _camera.backgroundColor;
            _hasSavedCameraState = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(_savedBackgroundColor.r, _savedBackgroundColor.g, _savedBackgroundColor.b, 0f);
        }
        else
        {
            if (_hasSavedCameraState)
            {
                _camera.clearFlags = _savedClearFlags;
                _camera.backgroundColor = _savedBackgroundColor;
            }
            else
            {
                _camera.clearFlags = CameraClearFlags.Skybox;
                _camera.backgroundColor = new Color(0f, 0f, 0f, 1f);
            }
        }
    }
}
