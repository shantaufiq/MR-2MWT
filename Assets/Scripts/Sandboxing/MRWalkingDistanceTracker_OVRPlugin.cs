using Autohand;
using TMPro;
using UnityEngine;

/// <summary>
/// MR Walking Distance Tracker (Meta Quest / Oculus Integration)
///
/// - Ambil HEAD pose + velocity langsung dari OVRPlugin (sensor fusion Meta)
/// - Hitung jarak dari pergerakan XZ (abaikan Y)
/// - Filter noise, teleport, dan validasi velocity
/// - Opsional: validasi hips/pelvis untuk “biomechanical validation”
///
/// Catatan:
/// - OVRPlugin pose biasanya berada di tracking space. Jika butuh world-space yang benar,
///   isi field trackingOrigin (misal: OVRCameraRig.trackingSpace).
/// </summary>
public class MRWalkingDistanceTracker_OVRPlugin : MonoBehaviour
{
    [Header("Tracking Space (Optional)")]
    [Tooltip("Jika diisi, pose dari OVRPlugin akan di-convert ke World Space melalui transform ini (misal OVRCameraRig/TrackingSpace).")]
    [SerializeField] private Transform trackingOrigin;

    [Header("Body Validation (Optional)")]
    [Tooltip("Hips / Pelvis joint dari character (mixamorig:Hips / Meta Body Rig). Kosongkan jika tidak ada.")]
    [SerializeField] private Transform hipsTransform;

    [Header("Movement Thresholds")]
    [Tooltip("Noise per-frame threshold (meter).")]
    [SerializeField] private float frameThreshold = 0.005f;

    [Tooltip("Akumulasi minimum jarak agar dianggap berjalan (meter).")]
    [SerializeField] private float windowThreshold = 0.02f;

    [Tooltip("Durasi evaluasi window (detik).")]
    [SerializeField] private float windowDuration = 0.2f;

    [Tooltip("Teleport/outlier filter (meter / frame). Jika lebih besar dari ini dianggap teleport dan di-skip.")]
    [SerializeField] private float maxStep = 1.2f;

    [Header("Velocity Validation")]
    [Tooltip("Minimum linear velocity agar dianggap berjalan (m/s).")]
    [SerializeField] private float minLinearVelocity = 0.1f;

    [Header("Hip Validation")]
    [Tooltip("Minimum pergerakan hips per frame (meter).")]
    [SerializeField] private float hipFrameThreshold = 0.003f;

    [Header("Result (Read Only)")]
    [SerializeField] private float totalDistance = 0f;
    public float TotalDistance => totalDistance;

    // ===== Internal State =====
    private Vector3 lastHeadPosWorld;
    private Vector3 lastHipPosWorld;

    private float accumulatedDistance = 0f;
    private float timer = 0f;
    private bool isTracking = false;

    [Header("")]
    [SerializeField] private TextMeshProUGUI text_distace;

    // =======================================================
    // Unity Loop
    // =======================================================

    private void Start()
    {
        StartTracking();
    }

    private void Update()
    {
        if (!isTracking) return;

        // Jika OVRPlugin belum siap, skip agar tidak baca data invalid
        if (!OVRPlugin.initialized) return;

        TrackWalkingUsingOVRPlugin();
    }

    // =======================================================
    // Public API
    // =======================================================

    public void StartTracking()
    {
        isTracking = true;

        totalDistance = 0f;
        accumulatedDistance = 0f;
        timer = 0f;

        lastHeadPosWorld = GetHeadWorldPosition();

        if (hipsTransform != null)
            lastHipPosWorld = hipsTransform.position;
    }

    public void StopTracking()
    {
        isTracking = false;
    }

    // =======================================================
    // Core Logic
    // =======================================================

    private void TrackWalkingUsingOVRPlugin()
    {
        // 1) HEAD world position dari OVRPlugin
        Vector3 currentHeadWorld = GetHeadWorldPosition();

        // Hitung jarak pada bidang XZ (abaikan naik-turun)
        Vector3 currentHeadXZ = new Vector3(currentHeadWorld.x, 0f, currentHeadWorld.z);
        Vector3 lastHeadXZ = new Vector3(lastHeadPosWorld.x, 0f, lastHeadPosWorld.z);

        float dist = Vector3.Distance(currentHeadXZ, lastHeadXZ);

        // Teleport/outlier filter
        if (dist > maxStep)
        {
            ResetWindow();
            UpdateLastPoses(currentHeadWorld);
            return;
        }

        // Noise filter per frame
        if (dist < frameThreshold)
        {
            UpdateLastPoses(currentHeadWorld);
            return;
        }

        // 2) Velocity validation (sensor-level)
        Vector3 linearVelocity = GetHeadLinearVelocityWorld();
        if (linearVelocity.magnitude < minLinearVelocity)
        {
            UpdateLastPoses(currentHeadWorld);
            return;
        }

        // 3) Hip validation (opsional)
        if (hipsTransform != null)
        {
            Vector3 hipCurXZ = new Vector3(hipsTransform.position.x, 0f, hipsTransform.position.z);
            Vector3 hipLastXZ = new Vector3(lastHipPosWorld.x, 0f, lastHipPosWorld.z);

            float hipDist = Vector3.Distance(hipCurXZ, hipLastXZ);

            if (hipDist < hipFrameThreshold)
            {
                UpdateLastPoses(currentHeadWorld);
                return;
            }
        }

        // 4) Akumulasi window
        accumulatedDistance += dist;
        timer += Time.deltaTime;

        // 5) Evaluasi window
        if (timer >= windowDuration)
        {
            if (accumulatedDistance >= windowThreshold)
            {
                // Opsional: cek arah gerak vs forward (biar gerak random kecil tidak dihitung)
                Vector3 forwardRef = (hipsTransform != null) ? hipsTransform.forward : GetHeadForwardWorld();
                forwardRef.y = 0f;
                forwardRef.Normalize();

                Vector3 moveDir = (currentHeadXZ - lastHeadXZ).normalized;
                float alignment = Vector3.Dot(moveDir, forwardRef);

                // alignment > 0.2 berarti cukup “searah” untuk dianggap berjalan
                if (alignment > 0.2f)
                {
                    totalDistance += accumulatedDistance;

                    text_distace.text = $"{totalDistance:F0}m";
                }
            }

            ResetWindow();
        }

        UpdateLastPoses(currentHeadWorld);
    }

    // =======================================================
    // OVRPlugin Access (compatible: GetNodePoseStateImmediate(Node))
    // =======================================================

    private OVRPlugin.PoseStatef GetHeadPoseState()
    {
        // ✅ Signature sesuai error kamu: hanya 1 argumen
        return OVRPlugin.GetNodePoseStateImmediate(OVRPlugin.Node.Head);
    }

    // Konversi koordinat OVRPlugin -> Unity (flip Z)
    private static Vector3 FromFlippedZ(OVRPlugin.Vector3f v) => new Vector3(v.x, v.y, -v.z);

    // Untuk quaternion biasanya X & Y perlu di-flip juga
    private static Quaternion FromFlippedZ(OVRPlugin.Quatf q) => new Quaternion(-q.x, -q.y, q.z, q.w);

    private Vector3 TrackingToWorld(Vector3 trackingPos)
    {
        if (trackingOrigin == null) return trackingPos;
        return trackingOrigin.TransformPoint(trackingPos);
    }

    private Vector3 TrackingToWorldDirection(Vector3 trackingDir)
    {
        if (trackingOrigin == null) return trackingDir;
        return trackingOrigin.TransformDirection(trackingDir);
    }

    private Vector3 GetHeadWorldPosition()
    {
        var state = GetHeadPoseState();
        Vector3 trackingPos = FromFlippedZ(state.Pose.Position);
        return TrackingToWorld(trackingPos);
    }

    private Vector3 GetHeadLinearVelocityWorld()
    {
        var state = GetHeadPoseState();
        Vector3 trackingVel = FromFlippedZ(state.Velocity);
        return TrackingToWorldDirection(trackingVel);
    }

    private Vector3 GetHeadForwardWorld()
    {
        var state = GetHeadPoseState();
        Quaternion trackingRot = FromFlippedZ(state.Pose.Orientation);

        // Forward di tracking-space
        Vector3 trackingForward = trackingRot * Vector3.forward;
        Vector3 worldForward = TrackingToWorldDirection(trackingForward);

        return worldForward.normalized;
    }

    // =======================================================

    private void ResetWindow()
    {
        accumulatedDistance = 0f;
        timer = 0f;
    }

    private void UpdateLastPoses(Vector3 currentHeadWorld)
    {
        lastHeadPosWorld = currentHeadWorld;

        if (hipsTransform != null)
            lastHipPosWorld = hipsTransform.position;
    }
}
