using UnityEngine;
using ResXR.Menu;

/// <summary>
/// Attach to a "back" portal object with a trigger collider. On touch by object with "Toucher" tag, calls ResXRMenuController.ReturnToMenu().
/// </summary>
[RequireComponent(typeof(Collider))]
public class BackPortal : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Toucher"))
            return;

        if (ResXRMenuController.Instance == null)
        {
            Debug.LogError("[BackPortal] ResXRMenuController.Instance is null; cannot return to menu.");
            return;
        }

        ResXRMenuController.Instance.ReturnToMenu();
    }
}
