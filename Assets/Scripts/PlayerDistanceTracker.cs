using UnityEngine;
using TMPro;

public class PlayerDistanceTracker : MonoBehaviour
{
    [Header("Player")]
    public Transform playerTransform;

    [Header("Area Masks (set via Inspector)")]
    [Tooltip("Layer untuk lintasan hijau (jalur benar).")]
    public LayerMask greenMask;
    [Tooltip("Layer untuk area merah (jalur salah).")]
    public LayerMask redMask;

    [Header("UI")]
    public TextMeshProUGUI distanceText;

    [Header("Settings")]
    [Tooltip("Abaikan gerak sangat kecil (noise).")]
    public float minStep = 0.001f;              // ~1 mm
    [Tooltip("Batas maksimum jarak per frame agar teleport tidak dihitung.")]
    public float maxStep = 2.0f;                // 2 meter per frame (sesuaikan)

    private Vector3 lastPosition;
    private float totalDistance = 0f;

    // Properti tambahan
    [SerializeField] private float correctDistance = 0f; // di area hijau
    [SerializeField] private float wrongDistance = 0f; // di luar hijau atau di merah

    // Cache status pijakan
    private bool isOnGreen = false;
    private bool isOnRed = false;

    void Start()
    {
        if (playerTransform == null)
            playerTransform = transform;

        lastPosition = playerTransform.position;
        UpdateGroundState();
    }

    void Update()
    {
        UpdateGroundState();
        TrackDistance();
        UpdateUI();
    }

    // --- Helper untuk cek lantai di bawah player ---
    void UpdateGroundState()
    {
        // Raycast sedikit dari atas kaki ke bawah
        Vector3 origin = playerTransform.position + Vector3.up * 0.2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2f, ~0))
        {
            int hitLayerMask = 1 << hit.collider.gameObject.layer;
            isOnGreen = (greenMask.value & hitLayerMask) != 0;
            isOnRed = (redMask.value & hitLayerMask) != 0;
        }
        else
        {
            // Tidak mengenai apapun -> dianggap di luar lintasan hijau
            isOnGreen = false;
            isOnRed = false;
        }
    }

    void TrackDistance()
    {
        // Ambil posisi sekarang & posisi terakhir tapi abaikan Y
        Vector3 currentPosXZ = new Vector3(playerTransform.position.x, 0f, playerTransform.position.z);
        Vector3 lastPosXZ = new Vector3(lastPosition.x, 0f, lastPosition.z);

        float distanceThisFrame = Vector3.Distance(currentPosXZ, lastPosXZ);

        // Saringan noise/teleport
        if (distanceThisFrame >= minStep && distanceThisFrame <= maxStep)
        {
            totalDistance += distanceThisFrame;

            // Atur alokasi jarak:
            if (isOnGreen && !isOnRed)
                correctDistance += distanceThisFrame;
            else
                wrongDistance += distanceThisFrame;
        }

        // Update posisi terakhir (tetap pakai posisi asli, bukan yg diproyeksi ke XZ)
        lastPosition = playerTransform.position;
    }

    void UpdateUI()
    {
        // Konversi ke meter & cm (1 unit = 1 meter)
        FormatMetersCm(totalDistance, out int tM, out int tCm);
        FormatMetersCm(correctDistance, out int cM, out int cCm);
        FormatMetersCm(wrongDistance, out int wM, out int wCm);

        // Contoh tampilan:
        // Total: 12 m 34 cm | Benar: 10 m 20 cm | Salah: 2 m 14 cm
        if (distanceText != null)
            distanceText.text = $"Total: {tM:0} m {tCm:00} cm  |  Benar: {cM:0} m {cCm:00} cm  |  Salah: {wM:0} m {wCm:00} cm";
    }

    void FormatMetersCm(float valueInMeters, out int meters, out int centimeters)
    {
        meters = Mathf.FloorToInt(valueInMeters);
        centimeters = Mathf.RoundToInt((valueInMeters - meters) * 100f);
        if (centimeters == 100) { meters += 1; centimeters = 0; } // pembulatan aman
    }

#if UNITY_EDITOR
    // Debug kecil di Scene View
    void OnDrawGizmosSelected()
    {
        if (playerTransform == null) return;
        Gizmos.color = Color.white;
        Vector3 origin = playerTransform.position + Vector3.up * 0.2f;
        Gizmos.DrawLine(origin, origin + Vector3.down * 0.4f);
    }
#endif

    // --- Getter opsional bila perlu diakses script lain ---
    public float TotalDistance => totalDistance;
    public float CorrectDistance => correctDistance;
    public float WrongDistance => wrongDistance;
}
