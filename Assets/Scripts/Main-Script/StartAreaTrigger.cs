using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StartAreaTrigger : MonoBehaviour
{
    [SerializeField] private MeshRenderer mesh;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            mesh.enabled = false;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            mesh.enabled = true;
        }
    }
}
