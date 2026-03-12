using UnityEngine;
using ResXR.Menu;

/// <summary>
/// Attach to a portal object with a trigger collider. On touch by object with "Toucher" tag, loads the target scene via ResXRMenuController.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MenuPortal : MonoBehaviour
{
    [Header("Target scene (must be in Build Settings)")]
    [SerializeField] private string _targetSceneName;

    [Header("Launch with passthrough ON or OFF")]
    [SerializeField] private bool _launchWithPassthrough;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Toucher"))
            return;

        if (ResXRMenuController.Instance == null)
        {
            Debug.LogError("[MenuPortal] ResXRMenuController.Instance is null; cannot load portal.");
            return;
        }

        if (string.IsNullOrEmpty(_targetSceneName))
        {
            Debug.LogError("[MenuPortal] targetSceneName is not set.");
            return;
        }

        ResXRMenuController.Instance.LoadPortal(_targetSceneName, _launchWithPassthrough);
    }
}
