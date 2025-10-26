using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShowEyeTracking : MonoBehaviour
{
    public MeshRenderer meshRenderer;

    void OnEnable()
    {
        if (meshRenderer != null)
            meshRenderer.enabled = true;
    }

  

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
