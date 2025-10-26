using UnityEngine;
using System.Collections;

public class DisableHandMeshRenderers : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(DisableMeshRenderersAfterInitialization());
    }

    private IEnumerator DisableMeshRenderersAfterInitialization()
    {
        // Continuously check until the SkinnedMeshRenderer is available
        while (true)
        {
            if (TXRPlayer.Instance != null)
            {
                if (TXRPlayer.Instance.HandLeft != null && TXRPlayer.Instance.HandLeft._handSMR != null)
                {
                    TXRPlayer.Instance.HandLeft._handSMR.enabled = false;
                }
                if (TXRPlayer.Instance.HandRight != null && TXRPlayer.Instance.HandRight._handSMR != null)
                {
                    TXRPlayer.Instance.HandRight._handSMR.enabled = false;
                }
                // Exit the loop once both renderers are disabled
                yield break;
            }
            // Wait for the next frame before checking again
            yield return null;
        }
    }
} 