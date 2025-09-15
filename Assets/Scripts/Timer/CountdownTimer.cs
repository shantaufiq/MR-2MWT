using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using TMPro;

[AddComponentMenu("Timers/Countdown Timer (Threshold List)")]
public class CountdownTimer : MonoBehaviour
{
    // ====== Types ======
    [System.Serializable]
    public class TimerThreshold
    {
        [Tooltip("Label opsional agar mudah dikenali di Inspector.")]
        public string name;

        [Tooltip("Saat RemainingTime <= nilai ini (detik), event dipanggil.")]
        [Min(0f)] public float thresholdSeconds = 60f;

        [Tooltip("Event yang dipanggil saat melewati/masuk ke ambang ini.")]
        public UnityEvent onThreshold;

        [HideInInspector] public bool _fired;
    }

    // ====== Inspector ======
    [Header("Setup")]
    [Tooltip("Durasi awal timer (detik).")]
    [Min(0f)] public float startDurationSeconds = 120f;

    [Tooltip("Mulai otomatis saat OnEnable.")]
    public bool autoStart = false;

    [Tooltip("Jika true, saat timer dimulai/di-reset dan sudah <= ambang, event langsung dipanggil.")]
    private bool invokeImmediatelyIfBelowThreshold = true;

    [Header("Threshold Events (custom list)")]
    [Space(2f)]
    [Tooltip("Tambah item untuk menambahkan ambang kustom. Event dipanggil saat waktu sisa <= ambang.")]
    public List<TimerThreshold> thresholdEvents = new List<TimerThreshold>()
    {
        new TimerThreshold(){ name = "1 Minute Left", thresholdSeconds = 60f },
        new TimerThreshold(){ name = "Last 15s", thresholdSeconds = 15f }
    };

    [Tooltip("Urutkan threshold secara otomatis (kecil -> besar) saat play.")]
    private bool autoSortThresholdsAscending = true;

    [Header("Other Events")]
    [Space(2f)]
    public UnityEvent onTickEachSecond;     // opsional, terpanggil tiap detik berganti
    public UnityEvent onTimerCompleted;     // terpanggil saat timer habis

    [Header("Component References")]
    [Space(2f)]
    [SerializeField] private TextMeshProUGUI text_timerText;

    // ====== Public readonly props ======
    public float RemainingTime => _remainingTime;
    public float Duration => _duration;
    public bool IsRunning => _isRunning;
    public bool IsPaused => _isPaused;

    // ====== Private state ======
    float _duration;
    float _remainingTime;
    bool _isRunning;
    bool _isPaused;
    Coroutine _loop;
    int _lastWholeSecond;

    // ====== Unity lifecycle ======
    void Awake()
    {
        _duration = Mathf.Max(0f, startDurationSeconds);
        _remainingTime = _duration;
        ResetThresholdFlags();

        if (autoSortThresholdsAscending)
            thresholdEvents.Sort((a, b) => a.thresholdSeconds.CompareTo(b.thresholdSeconds));
    }

    void OnEnable()
    {
        if (autoStart) StartTimer();
        else if (invokeImmediatelyIfBelowThreshold) TryFireThresholdEvents();
    }

    void OnDisable()
    {
        StopLoop();
    }

    // ====== Controls ======
    public void StartTimer()
    {
        if (_remainingTime <= 0f)
            _remainingTime = Mathf.Max(0f, _duration);

        _isPaused = false;
        _isRunning = true;

        if (_loop == null)
            _loop = StartCoroutine(TimerLoop());

        if (invokeImmediatelyIfBelowThreshold)
            TryFireThresholdEvents();
    }

    public void PauseTimer()
    {
        if (!_isRunning || _isPaused) return;
        _isPaused = true;
    }

    public void ResumeTimer()
    {
        if (!_isRunning || !_isPaused) return;
        _isPaused = false;
    }

    public void ResetTimer()
    {
        StopLoop();
        _remainingTime = _duration;
        _isRunning = false;
        _isPaused = false;
        ResetThresholdFlags();
        _lastWholeSecond = Mathf.CeilToInt(_remainingTime);

        if (invokeImmediatelyIfBelowThreshold)
            TryFireThresholdEvents();
    }

    /// <summary>Set durasi baru (detik). resetRemaining=true akan mengembalikan RemainingTime ke durasi baru.</summary>
    public void SetDuration(float seconds, bool resetRemaining = true)
    {
        _duration = Mathf.Max(0f, seconds);

        if (resetRemaining)
        {
            _remainingTime = _duration;
            ResetThresholdFlags();
            _lastWholeSecond = Mathf.CeilToInt(_remainingTime);
            if (invokeImmediatelyIfBelowThreshold) TryFireThresholdEvents();
        }
    }

    /// <summary>Tambah (atau kurangi bila negatif) waktu sisa.</summary>
    public void AddTime(float deltaSeconds)
    {
        _remainingTime = Mathf.Max(0f, _remainingTime + deltaSeconds);

        // Jika langsung melompat melewati ambang, boleh panggil segera
        if (invokeImmediatelyIfBelowThreshold)
            TryFireThresholdEvents();
    }

    // ====== Loop ======
    IEnumerator TimerLoop()
    {
        _lastWholeSecond = Mathf.CeilToInt(_remainingTime);

        while (_remainingTime > 0f)
        {
            if (_isPaused)
            {
                yield return null;
                continue;
            }

            _remainingTime -= Time.deltaTime;
            if (_remainingTime < 0f) _remainingTime = 0f;

            TryFireThresholdEvents();

            int currentWhole = Mathf.CeilToInt(_remainingTime);
            if (currentWhole != _lastWholeSecond)
            {
                _lastWholeSecond = currentWhole;
                onTickEachSecond?.Invoke();
            }

            UpdateTextUI(text_timerText);

            yield return null;
        }

        _isRunning = false;
        _isPaused = false;
        onTimerCompleted?.Invoke();
        StopLoop();
    }

    private void UpdateTextUI(TextMeshProUGUI _text)
    {
        var t = Mathf.CeilToInt(RemainingTime);
        int m = Mathf.Max(0, t / 60);
        int s = Mathf.Max(0, t % 60);
        _text.text = $"{m:00}:{s:00}";
    }

    // ====== Helpers ======
    void TryFireThresholdEvents()
    {
        // Kita cek semua threshold yang belum fired dan sekarang sudah berada di bawah/tepat pada ambang
        for (int i = 0; i < thresholdEvents.Count; i++)
        {
            var th = thresholdEvents[i];
            if (th._fired) continue;

            if (_remainingTime <= th.thresholdSeconds)
            {
                th._fired = true;
                th.onThreshold?.Invoke();
            }
        }
    }

    void ResetThresholdFlags()
    {
        for (int i = 0; i < thresholdEvents.Count; i++)
            thresholdEvents[i]._fired = false;
    }

    void StopLoop()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
    }
}
