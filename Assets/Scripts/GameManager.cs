using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private TimeManager timeManager;                 // hubungkan di Inspector
    [SerializeField] private PlayerDistanceTracker distanceTracker;   // hubungkan di Inspector
    [SerializeField] private TrackWaypointGenerator waypointGenerator;// hubungkan di Inspector

    [Header("UI Panels")]
    [SerializeField] private GameObject starterPanel; // Panel awal (tampilkan saat Start)
    [SerializeField] private GameObject endPanel;     // Panel akhir (tampilkan saat waktu habis)

    [Header("UI Buttons")]
    [SerializeField] private Button startButton;      // Tombol "Mulai" di panel starter
    [SerializeField] private Button restartButton;    // (Opsional) tombol "Main Lagi" di panel akhir

    [Header("UI Result Texts")]
    [SerializeField] private TextMeshProUGUI totalDistanceText; // Teks hasil total jarak
    [SerializeField] private TextMeshProUGUI lapCountText;      // Teks hasil jumlah putaran

    [Header("Options")]
    [Tooltip("Set durasi tes (detik) saat mulai game.")]
    [SerializeField] private float gameDurationSeconds = 120f;

    void Awake()
    {
        // State awal UI
        if (starterPanel) starterPanel.SetActive(true);
        if (endPanel) endPanel.SetActive(false);

        // Pastikan komponen ada
        if (!timeManager) Debug.LogError("[GameManager] TimeManager belum diset.");
        if (!distanceTracker) Debug.LogError("[GameManager] PlayerDistanceTracker belum diset.");
        if (!waypointGenerator) Debug.LogError("[GameManager] TrackWaypointGenerator belum diset.");

        // Pastikan distance tracker tidak menghitung sebelum game dimulai
        //! if (distanceTracker) distanceTracker.enabled = false;

        // Siapkan timer (jangan auto start)
        if (timeManager)
        {
            timeManager.SetDuration(gameDurationSeconds, resetRemaining: true);
            timeManager.onTimerCompleted.AddListener(OnTimerCompleted);
        }

        // Hook tombol
        if (startButton) startButton.onClick.AddListener(StartGame);
        if (restartButton) restartButton.onClick.AddListener(RestartGame);
    }

    // --- FLOW ---

    public void StartGame()
    {
        // Tutup panel awal
        if (starterPanel) starterPanel.SetActive(false);

        // Reset data penting
        ResetRunStats();

        // Mulai hitung jarak (komponen diaktifkan setelah posisi terakhir ikut terset di frame berikutnya)
        if (distanceTracker) distanceTracker.enabled = true;

        // Mulai timer
        if (timeManager)
        {
            timeManager.SetDuration(gameDurationSeconds, resetRemaining: true);
            timeManager.StartTimer();  // pakai TimeManager agar tick & threshold tetap berjalan
        }

        waypointGenerator?.ResetLapsAndCheckpoints();
        waypointGenerator?.EnableLapCounting(true);

        distanceTracker.InitialPlayerPosition();
    }

    private void OnTimerCompleted()
    {
        // Hentikan perhitungan jarak
        if (distanceTracker) distanceTracker.enabled = false;

        // Tampilkan hasil
        ShowEndPanel();
    }

    private void ShowEndPanel()
    {
        // Ambil total jarak & lap dari komponen terkait
        float totalMeters = distanceTracker ? distanceTracker.TotalDistance : 0f; // PlayerDistanceTracker expose TotalDistance
        int laps = waypointGenerator ? waypointGenerator.lapsCompleted : 0;       // TrackWaypointGenerator.lapsCompleted

        // Format meter & cm
        FormatMetersCm(totalMeters, out int m, out int cm);

        if (totalDistanceText) totalDistanceText.text = $"{m:0} m {cm:00} cm";
        if (lapCountText) lapCountText.text = $"{laps} putaran";

        if (endPanel) endPanel.SetActive(true);
    }

    public void RestartGame()
    {
        // Tutup panel akhir, buka panel awal lagi
        if (endPanel) endPanel.SetActive(false);
        if (starterPanel) starterPanel.SetActive(true);

        // Hentikan & reset timer sepenuhnya
        if (timeManager)
        {
            timeManager.ResetTimer();
            timeManager.SetDuration(gameDurationSeconds, resetRemaining: true);
        }

        // Pastikan distance tracker tidak aktif sebelum user menekan Mulai
        if (distanceTracker) distanceTracker.enabled = false;

        // Reset lap agar perhitungan mulai dari nol
        if (waypointGenerator) waypointGenerator.lapsCompleted = 0;

        waypointGenerator?.EnableLapCounting(false);
        waypointGenerator?.ResetLapsAndCheckpoints();
    }

    private void ResetRunStats()
    {
        // Reset jarak: cara paling aman adalah menonaktifkan lalu mengaktifkan ulang komponen
        // (PlayerDistanceTracker menghindari lonjakan awal dengan maxStep filter)
        if (distanceTracker)
        {
            distanceTracker.enabled = false;
            distanceTracker.enabled = true;
        }

        // Reset lap
        if (waypointGenerator) waypointGenerator.lapsCompleted = 0;
    }

    // --- Helpers ---
    private void FormatMetersCm(float valueInMeters, out int meters, out int centimeters)
    {
        meters = Mathf.FloorToInt(valueInMeters);
        centimeters = Mathf.RoundToInt((valueInMeters - meters) * 100f);
        if (centimeters == 100) { meters += 1; centimeters = 0; }
    }
}
