using UnityEngine;

public class TrackCheckpoint : MonoBehaviour
{
    public int checkpointIndex;
    public TrackWaypointGenerator generator;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            generator.OnCheckpointPassed(checkpointIndex);

            Debug.Log($"player passed checkpoint number {checkpointIndex}");
        }
    }
}
