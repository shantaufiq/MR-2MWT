using UnityEngine;

namespace WalkingTest
{
    public class WorldCanvasFollower : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("World-space canvas root")]
        public GameObject canvasRoot;

        [Tooltip("Player camera (CenterEyeAnchor / Main Camera)")]
        public Transform playerCamera;

        [Header("Placement")]
        [Tooltip("Distance in front of player (meters)")]
        public float distanceFromPlayer = 1.2f;

        private bool m_isTracking = false;

        private void FixedUpdate()
        {
            if (m_isTracking)
            {
                TrackCanvas();
            }
        }

        public void ShowCanvas()
        {
            m_isTracking = true;

            if (!canvasRoot)
                canvasRoot.SetActive(false);
        }

        private void TrackCanvas()
        {
            if (!canvasRoot) return;

            canvasRoot.SetActive(true);
            canvasRoot.transform.position = playerCamera.position + new Vector3(playerCamera.forward.x, 0, playerCamera.forward.z).normalized * distanceFromPlayer;

            HandleCanvasLook(canvasRoot, playerCamera, distanceFromPlayer + 0.5f);
        }

        public void HideCanvas()
        {
            if (canvasRoot)
                canvasRoot.SetActive(false);
        }

        public void HandleCanvasLook(GameObject canvasTarget, Transform playerHead, float maxDistance)
        {
            if (canvasTarget.activeSelf == true)
            {
                canvasTarget.transform.LookAt(new Vector3(playerHead.position.x, canvasTarget.transform.position.y, playerHead.position.z));
                canvasTarget.transform.forward *= -1;
            }

            float distanceBetweenObjects = 0f;

            if (playerHead != null)
            {
                distanceBetweenObjects = Vector3.Distance(playerHead.position, canvasTarget.transform.position);

                if (distanceBetweenObjects < maxDistance)
                    Debug.DrawLine(playerHead.position, canvasTarget.transform.position, Color.green);
            }
            else Debug.LogWarning("HeadCanvas has not been assigned");

            if (distanceBetweenObjects > maxDistance && canvasTarget.activeSelf == true)
                canvasTarget.SetActive(false);
        }
    }
}