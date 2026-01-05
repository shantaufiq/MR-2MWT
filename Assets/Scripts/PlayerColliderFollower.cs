using UnityEngine;

public class PlayerColliderFollower : MonoBehaviour
{
    [SerializeField] private Transform centerEye;

    void LateUpdate()
    {
        if (!centerEye) return;

        Vector3 pos = centerEye.position;
        pos.y = 0.5f; // kira-kira dada / torso
        transform.position = pos;
    }
}
