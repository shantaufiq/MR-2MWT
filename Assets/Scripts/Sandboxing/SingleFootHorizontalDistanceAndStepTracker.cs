using System;
using UnityEngine;
using Unity.Collections;
using TMPro;
using Meta.XR.Movement.Retargeting; // pastikan namespace ini sesuai paket CharacterRetargeter Anda

[RequireComponent(typeof(CharacterRetargeter))]
public class SingleFootDistanceSpeedTracker : MonoBehaviour
{
    [Header("References")]
    public CharacterRetargeter characterRetargeter;
    public TextMeshProUGUI distanceText; // tampilan jarak (m cm)
    public TextMeshProUGUI stepsText;    // tampilan langkah
    public TextMeshProUGUI speedText;    // tampilan kecepatan m/menit
    public TextMeshProUGUI paceText;     // tampilan pace s/m

    [Header("Foot selection (priority: Index > NamePattern)")]
    [Tooltip("Jika >=0, pakai index joint ini dari characterRetargeter.JointPairs")]
    public int footJointIndex = -1;
    [Tooltip("Jika index tidak diisi, akan mencari joint berdasarkan pola nama (case-insensitive). Contoh: \"right_foot\" atau \"rightankle\".")]
    public string footJointNamePattern = "right_foot";

    [Header("Step and filter settings")]
    [Tooltip("Ambang minimal satu langkah (meter). Default 0.5 m = 50 cm")]
    [Range(0.2f, 1.5f)]
    public float stepThresholdMeters = 0.5f;

    [Tooltip("Noise deadzone: abaikan pergerakan di bawah nilai ini (meter)")]
    public float deadzoneMeters = 0.01f;

    [Tooltip("Jika perpindahan antar-frame lebih besar dari ini, anggap teleport dan abaikan frame tersebut (meter)")]
    public float teleportThresholdMeters = 3.0f;

    [Header("Behaviour")]
    [Tooltip("Jika true, timer berjalan hanya saat tracking aktif.")]
    public bool autoStartOnFirstValidPose = true;

    // internal state
    private int _resolvedFootIndex = -1;
    private Vector3 _lastFootPos;
    private bool _hasLastPos = false;

    private float _totalDistanceMeters = 0f;
    private float _accumulatorSinceLastStep = 0f;
    private int _stepsCount = 0;

    private float _elapsedTimeSeconds = 0f; // total waktu berjalan
    private bool _isTracking = false;

    // Public read-only accessors
    public float TotalDistanceMeters => _totalDistanceMeters;
    public int StepsCount => _stepsCount;
    public float ElapsedTimeSeconds => _elapsedTimeSeconds;
    public bool IsTracking => _isTracking;

    void Awake()
    {
        if (characterRetargeter == null)
            characterRetargeter = GetComponent<CharacterRetargeter>();

        if (characterRetargeter == null)
        {
            Debug.LogError("[SingleFootDistanceSpeedTracker] CharacterRetargeter tidak ditemukan pada GameObject ini. Pasang komponen atau assign di inspector.");
            enabled = false;
            return;
        }
    }

    void Start()
    {
        ResolveFootIndex();
        UpdateUI();
    }

    void ResolveFootIndex()
    {
        _resolvedFootIndex = -1;

        // Prioritas: jika user menyetelnya secara manual, gunakan itu
        if (footJointIndex >= 0)
        {
            _resolvedFootIndex = footJointIndex;
            Debug.Log($"[SingleFootDistanceSpeedTracker] Menggunakan footJointIndex (inspector): {_resolvedFootIndex}");
            return;
        }

        // Kalau tidak, coba cari berdasarkan nama pola di JointPairs
        var pairs = characterRetargeter.JointPairs;
        if (pairs != null && pairs.Length > 0 && !string.IsNullOrEmpty(footJointNamePattern))
        {
            string pattern = footJointNamePattern.Trim().ToLowerInvariant();
            for (int i = 0; i < pairs.Length; i++)
            {
                var t = pairs[i].Joint;
                if (t == null) continue;
                string name = t.name.ToLowerInvariant();
                if (name.Contains(pattern))
                {
                    _resolvedFootIndex = i;
                    Debug.Log($"[SingleFootDistanceSpeedTracker] Menemukan joint cocok pada index {_resolvedFootIndex}: {t.name}");
                    break;
                }
            }
        }

        if (_resolvedFootIndex == -1)
        {
            // fallback ke index 0 (root) agar tidak crash, tapi beri peringatan
            _resolvedFootIndex = 0;
            Debug.LogWarning("[SingleFootDistanceSpeedTracker] Gagal menemukan joint berdasarkan pola. Menggunakan fallback index 0 (root). Periksa mapping JointPairs jika ingin akurasi.");
        }
    }

    void Update()
    {
        if (!_isTracking) return;

        // Pastikan retargeter valid
        if (!characterRetargeter.IsValid)
        {
            // ketika tracking hilang, jangan lanjut; ketika tracking kembali, kita akan re-start apabila autoStart diaktifkan
            _hasLastPos = false;
            if (!characterRetargeter.IsValid && _isTracking && !autoStartOnFirstValidPose)
            {
                // jika user memilih manual tracking control, kita bisa stop; tapi default biarkan _isTracking tidak berubah
            }
            return;
        }

        // Ambil semua joint dalam world-space.
        // NOTE: Jika paket Anda memakai nama enum JointType berbeda, ganti WorldSpaceAllJoints sesuai paket.
        var bodyPose = characterRetargeter.GetCurrentBodyPose(JointType.WorldSpaceAllJoints);
        try
        {
            if (!bodyPose.IsCreated || bodyPose.Length == 0)
                return;

            if (_resolvedFootIndex < 0 || _resolvedFootIndex >= bodyPose.Length)
            {
                // Jika resolved index out of range (mungkin mapping berubah), coba resolve ulang sekali
                ResolveFootIndex();
                if (_resolvedFootIndex < 0 || _resolvedFootIndex >= bodyPose.Length)
                    return;
            }

            Vector3 footPos = bodyPose[_resolvedFootIndex].Position;

            // Auto-start tracking pada first valid pose jika diinginkan
            if (!_isTracking && autoStartOnFirstValidPose)
            {
                _isTracking = true;
                _elapsedTimeSeconds = 0f;
                _hasLastPos = false; // agar posisi awal diset tanpa menghitung jarak besar
            }

            if (!_hasLastPos)
            {
                _lastFootPos = footPos;
                _hasLastPos = true;
                return;
            }

            // Hitung pergeseran horizontal X,Z
            float delta = HorizontalDistanceXZ(_lastFootPos, footPos);

            // Teleport filter
            if (delta > teleportThresholdMeters)
            {
                // abaikan frame ini, tetapi update last pos agar tidak menghitung lompatan berikutnya
                _lastFootPos = footPos;
                return;
            }

            // deadzone noise
            if (delta < deadzoneMeters) delta = 0f;

            // update waktu jika tracking aktif
            if (_isTracking)
            {
                _elapsedTimeSeconds += Time.deltaTime;
            }

            if (delta > 0f)
            {
                // tambahkan jarak dan cek langkah
                _totalDistanceMeters += delta;
                _accumulatorSinceLastStep += delta;

                while (_accumulatorSinceLastStep >= stepThresholdMeters)
                {
                    _stepsCount += 1;
                    _accumulatorSinceLastStep -= stepThresholdMeters;
                }

                UpdateUI();
            }

            _lastFootPos = footPos;
        }
        finally
        {
            if (bodyPose.IsCreated)
                bodyPose.Dispose();
        }
    }

    private float HorizontalDistanceXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private void UpdateUI()
    {
        if (distanceText != null)
        {
            int meters = Mathf.FloorToInt(_totalDistanceMeters);
            int centimeters = Mathf.FloorToInt((_totalDistanceMeters - meters) * 100f);
            distanceText.text = $"Jarak: {meters} m {centimeters} cm";
        }

        if (stepsText != null)
            stepsText.text = $"Langkah: {_stepsCount}";

        // Kecepatan: meter per menit (m/menit)
        if (speedText != null)
        {
            if (_elapsedTimeSeconds > 0f)
            {
                float speedMPerMin = (_totalDistanceMeters / _elapsedTimeSeconds) * 60f;
                speedText.text = $"Kecepatan: {speedMPerMin:F2} m/menit";
            }
            else
            {
                speedText.text = $"Kecepatan: 0.00 m/menit";
            }
        }

        // Pace: detik per meter (s/m)
        if (paceText != null)
        {
            if (_totalDistanceMeters > 0f)
            {
                float paceSecPerMeter = _elapsedTimeSeconds / _totalDistanceMeters;
                paceText.text = $"Pace: {paceSecPerMeter:F2} s/m";
            }
            else
            {
                paceText.text = $"Pace: 0.00 s/m";
            }
        }
    }

    /// <summary>
    /// Reset semua pengukuran (jarak, langkah, waktu). Tidak mengubah resolvedFootIndex.
    /// </summary>
    public void ResetAll()
    {
        _totalDistanceMeters = 0f;
        _accumulatorSinceLastStep = 0f;
        _stepsCount = 0;
        _elapsedTimeSeconds = 0f;
        _hasLastPos = false;
        _isTracking = false;
        UpdateUI();
    }

    /// <summary>
    /// Mulai tracking manual (jika autoStartOnFirstValidPose = false)
    /// </summary>
    public void StartTracking()
    {
        _isTracking = true;
        _elapsedTimeSeconds = 0f;
        _hasLastPos = false;
    }

    /// <summary>
    /// Stop tracking (pauses time accumulation)
    /// </summary>
    public void StopTracking()
    {
        _isTracking = false;
    }

    // Optional helper: tampilkan nilai di OnGUI saat testing di Editor
    void OnGUI()
    {
#if UNITY_EDITOR
        GUI.Label(new Rect(10, 10, 300, 20), $"Distance: {_totalDistanceMeters:F2} m");
        GUI.Label(new Rect(10, 30, 300, 20), $"Steps: {_stepsCount}");
        GUI.Label(new Rect(10, 50, 300, 20), $"Elapsed: {_elapsedTimeSeconds:F2} s");
#endif
    }
}
