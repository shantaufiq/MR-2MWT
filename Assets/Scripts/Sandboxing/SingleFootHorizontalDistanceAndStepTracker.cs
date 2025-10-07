using System;
using Unity.Collections;
using UnityEngine;
using TMPro;
using Meta.XR.Movement.Retargeting; // sesuaikan jika namespace berbeda

[RequireComponent(typeof(CharacterRetargeter))]
public class SingleFootHorizontalDistanceAndStepTracker : MonoBehaviour
{
    [Header("References")]
    public CharacterRetargeter characterRetargeter;
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI stepsText;

    [Header("Foot Selection (priority: Index > NamePattern)")]
    [Tooltip("Jika >= 0, gunakan index joint ini dari characterRetargeter.JointPairs")]
    public int footJointIndex = -1;
    [Tooltip("Jika index tidak diset, cari joint berdasarkan pola nama (case-insensitive). Contoh: \"right_foot\", \"left_ankle\", atau \"foot\".")]
    public string footJointNamePattern = "right_foot";

    [Header("Step Settings")]
    [Tooltip("Ambang minimum untuk satu langkah (meter). Default = 0.5 m (50 cm).")]
    [Range(0.2f, 1.5f)]
    public float stepThresholdMeters = 0.5f;

    [Header("Filters")]
    [Tooltip("Abaikan pergerakan sangat kecil (noise) di bawah nilai ini (meter).")]
    public float deadzoneMeters = 0.01f;
    [Tooltip("Jika perpindahan antar-frame lebih besar dari ini (teleport), abaikan frame tersebut).")]
    public float teleportThresholdMeters = 3.0f;

    // internal state
    private int _resolvedFootIndex = -1;
    private Vector3 _lastFootPos;
    private bool _hasLastPos = false;

    private float _totalDistanceMeters = 0f;
    private float _accumulatorSinceLastStep = 0f;
    private int _stepsCount = 0;

    void Awake()
    {
        if (characterRetargeter == null)
            characterRetargeter = GetComponent<CharacterRetargeter>();

        if (characterRetargeter == null)
        {
            Debug.LogError("[SingleFootTracker] CharacterRetargeter tidak ditemukan. Pasang komponen atau assign reference di inspector.");
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

        // Prioritas: gunakan footJointIndex jika user menyetelnya
        if (footJointIndex >= 0)
        {
            _resolvedFootIndex = footJointIndex;
            Debug.Log($"[SingleFootTracker] Menggunakan footJointIndex dari inspector: {_resolvedFootIndex}");
            return;
        }

        // Jika tidak, coba cari berdasarkan nama pola pada JointPairs
        var pairs = characterRetargeter.JointPairs;
        if (pairs == null || pairs.Length == 0)
        {
            Debug.LogWarning("[SingleFootTracker] JointPairs kosong atau null. Mapping di Retargeting Editor mungkin belum dilakukan.");
        }
        else
        {
            string pattern = (footJointNamePattern ?? "").Trim().ToLowerInvariant();
            if (pattern.Length > 0)
            {
                for (int i = 0; i < pairs.Length; i++)
                {
                    var t = pairs[i].Joint;
                    if (t == null) continue;
                    string name = t.name.ToLowerInvariant();
                    if (name.Contains(pattern))
                    {
                        _resolvedFootIndex = i;
                        Debug.Log($"[SingleFootTracker] Menemukan joint cocok pada index {_resolvedFootIndex} : {t.name}");
                        break;
                    }
                }
            }
        }

        if (_resolvedFootIndex == -1)
        {
            // fallback ke root index 0 agar tidak crash (tidak ideal untuk langkah)
            _resolvedFootIndex = 0;
            Debug.LogWarning($"[SingleFootTracker] Gagal menemukan joint berdasarkan pola '{footJointNamePattern}'. Menggunakan fallback index 0 (root). Periksa mapping agar hasil lebih akurat.");
        }
    }

    void Update()
    {
        if (!characterRetargeter.IsValid)
        {
            // tracking invalid -> jangan hitung, tapi jangan reset total agar sesi tetap ada
            _hasLastPos = false;
            return;
        }

        // Ambil semua joint dalam world-space
        var bodyPose = characterRetargeter.GetCurrentBodyPose(JointType.WorldSpaceAllJoints);
        try
        {
            if (!bodyPose.IsCreated || bodyPose.Length == 0)
                return;

            if (_resolvedFootIndex < 0 || _resolvedFootIndex >= bodyPose.Length)
            {
                Debug.LogWarning("[SingleFootTracker] resolved foot index out of range for bodyPose. Coba ResolveFootIndex lagi.");
                ResolveFootIndex();
                if (_resolvedFootIndex < 0 || _resolvedFootIndex >= bodyPose.Length)
                    return;
            }

            Vector3 footPos = bodyPose[_resolvedFootIndex].Position;

            if (!_hasLastPos)
            {
                _lastFootPos = footPos;
                _hasLastPos = true;
                return;
            }

            float delta = HorizontalDistance(_lastFootPos, footPos);

            // teleport filter
            if (delta > teleportThresholdMeters)
            {
                _lastFootPos = footPos;
                return;
            }

            if (delta < deadzoneMeters) delta = 0f;

            if (delta > 0f)
            {
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

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    public void ResetCounts()
    {
        _totalDistanceMeters = 0f;
        _accumulatorSinceLastStep = 0f;
        _stepsCount = 0;
        _hasLastPos = false;
        UpdateUI();
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
    }
}
