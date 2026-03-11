using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WalkingTest;

public class StartAreaTrigger : MonoBehaviour
{
    public MeshRenderer mesh;
    [SerializeField] private GamificationAssetContainer gameAsset;

    public bool isStartAreaTriggerActive = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && isStartAreaTriggerActive)
        {
            mesh.enabled = false;
            gameAsset.SetEnableAsset(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && isStartAreaTriggerActive)
        {
            mesh.enabled = true;
            gameAsset.SetEnableAsset(false);
        }
    }
}
