using UnityEngine;
using TMPro;
using System;

/// <summary>
/// Quest 3 / MR Walking Tracker
/// - Distance source: OVRPlugin Head Pose + Velocity (sensor fusion Meta)
/// - Correct/Wrong path: Ground raycast to Green/Red layer masks
/// - Step count: Mixamo Foot Contact detector (LeftFoot/RightFoot)
///
/// Mixamo hierarchy expected (contoh kamu):
/// mixamorig1:Hips
///   mixamorig1:LeftFoot
///   mixamorig1:RightFoot
///
/// Recommended:
/// - trackingOrigin = OVRCameraRig/TrackingSpace
/// - hipsTransform = mixamorig1:Hips
/// - leftFoot/rightFoot = mixamorig1:LeftFoot / mixamorig1:RightFoot
/// - groundProbe = (optional) hipsTransform atau empty object di bawah pelvis
/// </summary>
public class MRWalkingTracker_Quest3_MixamoFootSteps : MonoBehaviour
{
    // =========================
    // Tracking Space
    // =========================
    [Header("Tracking Space (Recommended)")]
    [Tooltip("Isi dengan OVRCameraRig/TrackingSpace agar pose OVRPlugin dikonversi ke World Space.")]
    [SerializeField] private Transform trackingOrigin;

    // =========================
    // Mixamo Rig (Bones)
    // =========================
    [Header("Mixamo Rig (Bones)")]
    [SerializeField] private Transform hipsTransform;
    [SerializeField] private Transform leftFoot;
    [SerializeField] private Transform rightFoot;

    [Tooltip("Auto-find bone berdasarkan nama Mixamo jika kosong.")]
    [SerializeField] private bool autoFindMixamoBones = true;

    // =========================
    // Ground / Path Validation
    // =========================
    [Header("Ground / Path Validation")]
    [Tooltip("Transform untuk raycast ground status (ideal: di dekat kaki/rig root). Jika null, fallback ke hips/trackingOrigin/head.")]
    [SerializeField] private Transform groundProbe;

    [Tooltip("Layer untuk jalur benar (GREEN).")]
    [SerializeField] private LayerMask greenMask;

    [Tooltip("Layer untuk jalur salah (RED).")]
    [SerializeField] private LayerMask redMask;

    [Tooltip("Offset raycast dari probe ke atas.")]
    [SerializeField] private float groundRayUpOffset = 0.25f;

    [Tooltip("Panjang raycast ke bawah.")]
    [SerializeField] private float groundRayLength = 2.5f;

    [Tooltip("Optional renderer indikator status ground (hijau/merah/abu).")]
    [SerializeField] private Renderer groundStatusRenderer;

    // =========================
    // Distance Tracking Filters
    // =========================
    [Header("Distance Tracking (OVRPlugin)")]
    [Tooltip("Noise threshold per frame (meter).")]
    [SerializeField] private float frameThreshold = 0.005f;

    [Tooltip("Akumulasi minimum jarak agar dianggap berjalan (meter). Dikurangi dari 0.02 ke 0.01 untuk mengurangi window discard.")]
    [SerializeField] private float windowThreshold = 0.01f;

    [Tooltip("Durasi evaluasi window (detik).")]
    [SerializeField] private float windowDuration = 0.20f;

    [Tooltip("Teleport/outlier filter (meter/frame).")]
    [SerializeField] private float maxStep = 1.2f;

    [Tooltip("Minimum linear velocity agar dianggap berjalan (m/s).")]
    [SerializeField] private float minLinearVelocity = 0.10f;

    [Header("Position Smoothing")]
    [Tooltip("Time constant (detik) low-pass filter pada posisi XZ kepala. Meredam goyangan lateral alami saat berjalan cepat/percaya diri " +
        "(sway kiri-kanan antar langkah) yang kalau dijumlah mentah per-frame membuat total jarak lebih panjang dari jarak maju sebenarnya. " +
        "0 = nonaktif (raw). Terlalu besar bisa memperparah under-estimate di tikungan tajam.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float positionSmoothingTime = 0.15f;

    [Header("Calibration")]
    [Tooltip("Faktor skala jarak akhir. Gunakan untuk kalibrasi sistematis: jika jarak tercatat < aktual, naikkan nilai ini (contoh: jika 25m tercatat untuk 30m aktual, set 1.2).")]
    [Range(0.5f, 2.0f)]
    [SerializeField] private float distanceScale = 1.0f;

    [Header("Direction Validation")]
    [Tooltip("Minimal alignment (dot) antara arah gerak dan forward. Set -1.0 untuk nonaktifkan (direkomendasikan: -1.0 karena filter velocity sudah cukup, dan filter ini sering salah-reject saat tikungan karena IK avatar lag).")]
    [Range(-1f, 1f)]
    [SerializeField] private float minForwardAlignment = -1.0f;

    [Header("Hip Validation (Optional)")]
    [Tooltip("Minimum pergerakan hips per frame (meter) untuk validasi biomekanik. Set 0 untuk disable.")]
    [SerializeField] private float hipFrameThreshold = 0.003f;

    // =========================
    // Step Count (Foot Contact)
    // =========================
    [Header("Step Count (Foot Contact)")]
    [Tooltip("Aktifkan step detector dari kaki (Mixamo LeftFoot/RightFoot).")]
    [SerializeField] private bool enableFootSteps = true;

    [Tooltip("Raycast offset dari kaki ke atas.")]
    [SerializeField] private float footRayUpOffset = 0.08f;

    [Tooltip("Raycast panjang ke bawah dari kaki.")]
    [SerializeField] private float footRayLength = 0.35f;

    [Tooltip("Jika jarak kaki ke ground <= ini, dianggap contact (meter).")]
    [SerializeField] private float footContactTolerance = 0.05f;

    [Tooltip("Minimal interval antar step (detik) untuk anti double-count.")]
    [SerializeField] private float minStepInterval = 0.25f;

    [Tooltip("Minimal speed agar step dihitung (m/s).")]
    [SerializeField] private float minSpeedForStep = 0.20f;

    // =========================
    // UI
    // =========================
    [Header("UI (Optional)")]
    [SerializeField] private TextMeshProUGUI distanceText;

    // =========================
    // Results (Read Only)
    // =========================
    [Header("Result (Read Only)")]
    [SerializeField] private float totalDistance = 0f;
    [SerializeField] private float correctDistance = 0f;
    [SerializeField] private float wrongDistance = 0f;

    [SerializeField] private int totalSteps = 0;
    [SerializeField] private int correctSteps = 0;
    [SerializeField] private int wrongSteps = 0;

    public float TotalDistance => totalDistance;
    public float CorrectDistance => correctDistance;
    public float WrongDistance => wrongDistance;

    public void GetResult(Action<float> total, Action<float> correct, Action<float> wrong)
    {
        total?.Invoke(TotalDistance);
        correct?.Invoke(CorrectDistance);
        wrong?.Invoke(WrongDistance);
    }

    public int TotalSteps => totalSteps;
    public int CorrectSteps => correctSteps;
    public int WrongSteps => wrongSteps;

    public bool IsOnGreen => isOnGreen;
    public bool IsOnRed => isOnRed;

    /// <summary>Benar hanya jika kena GREEN dan tidak kena RED.</summary>
    public bool IsOnCorrectPath => isOnGreen && !isOnRed;

    // =========================
    // Internal State
    // =========================
    private bool isTracking = false;

    private Vector3 lastHeadPosWorld;
    private Vector3 lastHipPosWorld;
    private Vector3 smoothedHeadXZ;

    // window pending
    private float pendingWindowDistance = 0f;
    private float pendingWindowCorrect = 0f;
    private float pendingWindowWrong = 0f;
    private float windowTimer = 0f;

    // ground status
    private bool isOnGreen = false;
    private bool isOnRed = false;

    // indicator
    private MaterialPropertyBlock mpb;

    // foot state
    private Vector3 lastLeftFootPos;
    private Vector3 lastRightFootPos;
    private bool leftWasContact = false;
    private bool rightWasContact = false;
    private float lastStepTime = -999f;

    // =========================
    // Unity
    // =========================
    private void Awake()
    {
        mpb ??= new MaterialPropertyBlock();

        if (autoFindMixamoBones)
            AutoAssignMixamoBonesIfNeeded();
    }

    private void Start()
    {
        // StartTracking();
    }

    private void Update()
    {
        if (!isTracking) return;
        if (!OVRPlugin.initialized) return;

        UpdateGroundState();
        TrackDistanceOVRPlugin();
        TrackStepsFootContact();
        UpdateUI();
        UpdateGroundIndicator();
    }

    // =========================
    // Public API
    // =========================
    public void StartTracking()
    {
        isTracking = true;

        totalDistance = correctDistance = wrongDistance = 0f;
        totalSteps = correctSteps = wrongSteps = 0;

        ResetWindow();

        lastHeadPosWorld = GetHeadWorldPosition();
        smoothedHeadXZ = new Vector3(lastHeadPosWorld.x, 0f, lastHeadPosWorld.z);
        if (hipsTransform != null) lastHipPosWorld = hipsTransform.position;

        if (leftFoot != null) lastLeftFootPos = leftFoot.position;
        if (rightFoot != null) lastRightFootPos = rightFoot.position;

        leftWasContact = rightWasContact = false;
        lastStepTime = -999f;

        UpdateGroundState();
    }

    public void StopTracking() => isTracking = false;

    public void InitialPlayerPosition()
    {
        lastHeadPosWorld = GetHeadWorldPosition();
        smoothedHeadXZ = new Vector3(lastHeadPosWorld.x, 0f, lastHeadPosWorld.z);
        if (hipsTransform != null) lastHipPosWorld = hipsTransform.position;

        if (leftFoot != null) lastLeftFootPos = leftFoot.position;
        if (rightFoot != null) lastRightFootPos = rightFoot.position;

        leftWasContact = rightWasContact = false;
        lastStepTime = -999f;

        UpdateGroundState();
    }

    // =========================
    // 1) Distance Tracking (OVRPlugin)
    // =========================
    private void TrackDistanceOVRPlugin()
    {
        Vector3 currentHeadWorld = GetHeadWorldPosition();

        // Low-pass filter posisi XZ mentah sebelum dipakai untuk menghitung dist. Ini meredam goyangan
        // lateral alami antar langkah (sway kiri-kanan) yang kalau dijumlah mentah per-frame membuat
        // total jarak lebih panjang dari jarak maju sebenarnya, terutama saat berjalan cepat/percaya diri.
        Vector3 rawXZ = new Vector3(currentHeadWorld.x, 0f, currentHeadWorld.z);
        float smoothAlpha = positionSmoothingTime > 0.0001f ? 1f - Mathf.Exp(-Time.deltaTime / positionSmoothingTime) : 1f;
        smoothedHeadXZ = Vector3.Lerp(smoothedHeadXZ, rawXZ, smoothAlpha);
        currentHeadWorld = new Vector3(smoothedHeadXZ.x, currentHeadWorld.y, smoothedHeadXZ.z);

        Vector3 curXZ = smoothedHeadXZ;
        Vector3 lastXZ = new Vector3(lastHeadPosWorld.x, 0f, lastHeadPosWorld.z);

        float dist = Vector3.Distance(curXZ, lastXZ);

        // Teleport/outlier
        if (dist > maxStep)
        {
            ResetWindow();
            UpdateLastPoses(currentHeadWorld);
            return;
        }

        // Noise
        if (dist < frameThreshold)
        {
            UpdateLastPoses(currentHeadWorld);
            return;
        }

        // Velocity validation
        // Baseline (lastHeadPosWorld) sengaja TIDAK direset di sini. Saat pengguna melangkah pelan/ragu-ragu,
        // velocity instan dari OVRPlugin bisa dip di bawah threshold sesaat meski posisi sudah bergeser nyata.
        // Dengan membiarkan baseline tetap, jarak yang tertunda ikut terakumulasi ke "dist" frame berikutnya
        // dan baru diklaim begitu ada frame yang lolos validasi, alih-alih hilang permanen tiap kali ditolak.
        Vector3 velWorld = GetHeadLinearVelocityWorld();
        float speed = velWorld.magnitude;
        if (speed < minLinearVelocity)
        {
            return;
        }

        // Hip validation (optional)
        if (hipsTransform != null && hipFrameThreshold > 0f)
        {
            Vector3 hipCurXZ = new Vector3(hipsTransform.position.x, 0f, hipsTransform.position.z);
            Vector3 hipLastXZ = new Vector3(lastHipPosWorld.x, 0f, lastHipPosWorld.z);
            float hipDist = Vector3.Distance(hipCurXZ, hipLastXZ);

            if (hipDist < hipFrameThreshold)
            {
                return;
            }
        }

        // Direction alignment
        Vector3 forwardRef = (hipsTransform != null) ? hipsTransform.forward : GetHeadForwardWorld();
        forwardRef.y = 0f;
        forwardRef.Normalize();

        Vector3 moveDir = (curXZ - lastXZ).normalized;
        float alignment = Vector3.Dot(moveDir, forwardRef);

        // Jika tidak sesuai arah maju, kita anggap "tidak valid berjalan" → tidak commit jarak
        if (alignment < minForwardAlignment)
        {
            return;
        }

        // Accumulate window
        pendingWindowDistance += dist;
        if (IsOnCorrectPath) pendingWindowCorrect += dist;
        else pendingWindowWrong += dist;

        windowTimer += Time.deltaTime;

        // Evaluate window
        if (windowTimer >= windowDuration)
        {
            if (pendingWindowDistance >= windowThreshold)
            {
                float scale = Mathf.Max(0.01f, distanceScale);
                totalDistance += pendingWindowDistance * scale;
                correctDistance += pendingWindowCorrect * scale;
                wrongDistance += pendingWindowWrong * scale;
            }
            ResetWindow();
        }

        UpdateLastPoses(currentHeadWorld);
    }

    // =========================
    // 2) Step Count (Foot Contact)
    // =========================
    private void TrackStepsFootContact()
    {
        if (!enableFootSteps) return;

        // Pastikan bone ada
        if (leftFoot == null || rightFoot == null) return;

        // Minimal speed agar step dihitung
        float speed = GetHeadLinearVelocityWorld().magnitude;
        if (speed < minSpeedForStep)
        {
            lastLeftFootPos = leftFoot.position;
            lastRightFootPos = rightFoot.position;
            return;
        }

        // Update per foot
        bool leftContact = GetFootContact(leftFoot, out bool leftCorrect);
        bool rightContact = GetFootContact(rightFoot, out bool rightCorrect);

        // Step = transisi "no contact" -> "contact"
        // plus anti double count (minStepInterval)
        if (!leftWasContact && leftContact && CanAddStepNow())
            AddStep(leftCorrect);

        if (!rightWasContact && rightContact && CanAddStepNow())
            AddStep(rightCorrect);

        leftWasContact = leftContact;
        rightWasContact = rightContact;

        lastLeftFootPos = leftFoot.position;
        lastRightFootPos = rightFoot.position;
    }

    private bool CanAddStepNow()
    {
        return (Time.time - lastStepTime) >= minStepInterval;
    }

    private void AddStep(bool isCorrect)
    {
        totalSteps++;
        if (isCorrect) correctSteps++;
        else wrongSteps++;

        lastStepTime = Time.time;
    }

    /// <summary>
    /// Foot contact check menggunakan raycast ke ground.
    /// Mengembalikan:
    /// - contact: apakah kaki menyentuh ground
    /// - isCorrect: apakah ground yang disentuh GREEN (dan bukan RED)
    /// </summary>
    private bool GetFootContact(Transform foot, out bool isCorrect)
    {
        isCorrect = false;

        Vector3 origin = foot.position + Vector3.up * footRayUpOffset;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, footRayLength, ~0))
        {
            float footToGround = Mathf.Abs(foot.position.y - hit.point.y);

            // layer check (benar/salah)
            int hitLayerMask = 1 << hit.collider.gameObject.layer;
            bool hitGreen = (greenMask.value & hitLayerMask) != 0;
            bool hitRed = (redMask.value & hitLayerMask) != 0;

            isCorrect = hitGreen && !hitRed;

            // contact jika cukup dekat ground
            return footToGround <= footContactTolerance;
        }

        return false;
    }

    // =========================
    // Ground Status (Green / Red)
    // =========================
    private void UpdateGroundState()
    {
        Transform probe = groundProbe;

        if (probe == null && hipsTransform != null) probe = hipsTransform;
        if (probe == null && trackingOrigin != null) probe = trackingOrigin;

        Vector3 basePos = (probe != null) ? probe.position : GetHeadWorldPosition();
        Vector3 origin = basePos + Vector3.up * groundRayUpOffset;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundRayLength, ~0))
        {
            int hitLayerMask = 1 << hit.collider.gameObject.layer;
            isOnGreen = (greenMask.value & hitLayerMask) != 0;
            isOnRed = (redMask.value & hitLayerMask) != 0;
        }
        else
        {
            isOnGreen = false;
            isOnRed = false;
        }
    }

    private void UpdateGroundIndicator()
    {
        if (groundStatusRenderer == null) return;

        Color c;
        if (!isOnGreen && !isOnRed) c = Color.gray;
        else if (IsOnCorrectPath) c = Color.green;
        else c = Color.red;

        groundStatusRenderer.GetPropertyBlock(mpb);

        // Support shader property names
        if (groundStatusRenderer.sharedMaterial != null)
        {
            if (groundStatusRenderer.sharedMaterial.HasProperty("_BaseColor"))
                mpb.SetColor("_BaseColor", c);
            if (groundStatusRenderer.sharedMaterial.HasProperty("_Color"))
                mpb.SetColor("_Color", c);
        }

        groundStatusRenderer.SetPropertyBlock(mpb);
    }

    // =========================
    // UI
    // =========================
    private void UpdateUI()
    {
        if (distanceText == null) return;

        FormatMetersCm(totalDistance, out int tM, out int tCm);
        FormatMetersCm(correctDistance, out int cM, out int cCm);
        FormatMetersCm(wrongDistance, out int wM, out int wCm);

        string groundStatus =
            IsOnCorrectPath ? "GROUND: GREEN (BENAR)" :
            (isOnRed ? "GROUND: RED (SALAH)" : "GROUND: OUTSIDE (SALAH)");

        distanceText.text = $"{totalDistance:0} m";
    }

    private void FormatMetersCm(float valueInMeters, out int meters, out int centimeters)
    {
        meters = Mathf.FloorToInt(valueInMeters);
        centimeters = Mathf.RoundToInt((valueInMeters - meters) * 100f);
        if (centimeters == 100) { meters += 1; centimeters = 0; }
    }

    // =========================
    // OVRPlugin Access (signature: GetNodePoseStateImmediate(Node))
    // =========================
    private OVRPlugin.PoseStatef GetHeadPoseState()
    {
        return OVRPlugin.GetNodePoseStateImmediate(OVRPlugin.Node.Head);
    }

    // Convert OVRPlugin -> Unity (flip Z)
    private static Vector3 FromFlippedZ(OVRPlugin.Vector3f v) => new Vector3(v.x, v.y, -v.z);
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

        Vector3 trackingForward = trackingRot * Vector3.forward;
        Vector3 worldForward = TrackingToWorldDirection(trackingForward);
        return worldForward.normalized;
    }

    // =========================
    // Helpers
    // =========================
    private void ResetWindow()
    {
        pendingWindowDistance = 0f;
        pendingWindowCorrect = 0f;
        pendingWindowWrong = 0f;
        windowTimer = 0f;
    }

    private void UpdateLastPoses(Vector3 currentHeadWorld)
    {
        lastHeadPosWorld = currentHeadWorld;
        if (hipsTransform != null) lastHipPosWorld = hipsTransform.position;
    }

    private void AutoAssignMixamoBonesIfNeeded()
    {
        // cari dari root object script ini (atau karakter dipasang di parent yang sama)
        Transform root = transform;

        if (hipsTransform == null)
            hipsTransform = FindDeepChild(root, "mixamorig1:Hips") ?? FindDeepChild(root, "Hips");

        if (leftFoot == null)
            leftFoot = FindDeepChild(root, "mixamorig1:LeftFoot") ?? FindDeepChild(root, "LeftFoot");

        if (rightFoot == null)
            rightFoot = FindDeepChild(root, "mixamorig1:RightFoot") ?? FindDeepChild(root, "RightFoot");

        if (groundProbe == null && hipsTransform != null)
            groundProbe = hipsTransform;
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Ground probe ray
        Transform probe = groundProbe != null ? groundProbe : (hipsTransform != null ? hipsTransform : transform);
        Vector3 origin = probe.position + Vector3.up * groundRayUpOffset;

        Gizmos.color = Color.white;
        Gizmos.DrawLine(origin, origin + Vector3.down * Mathf.Min(groundRayLength, 1.0f));

        // Foot rays
        if (leftFoot != null)
        {
            Vector3 o = leftFoot.position + Vector3.up * footRayUpOffset;
            Gizmos.DrawLine(o, o + Vector3.down * footRayLength);
        }

        if (rightFoot != null)
        {
            Vector3 o = rightFoot.position + Vector3.up * footRayUpOffset;
            Gizmos.DrawLine(o, o + Vector3.down * footRayLength);
        }
    }
#endif
}
