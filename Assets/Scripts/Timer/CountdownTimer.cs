using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;

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

        public string textInformation;
        public AudioClip audioClip;
        [Range(0.7f, 5f)]
        public float delayTimeToHide = 0.8f;

        [Tooltip("Event yang dipanggil saat melewati/masuk ke ambang ini.")]
        public UnityEvent onThreshold;

        [HideInInspector] public bool _fired;
    }

    // ====== Inspector ======
    [Header("Configuration")]
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
        /* new TimerThreshold(){ name = "1 Minute Left", thresholdSeconds = 60f },
        new TimerThreshold(){ name = "Last 15s", thresholdSeconds = 15f } */
    };

    [Tooltip("Urutkan threshold secara otomatis (kecil -> besar) saat play.")]
    private bool autoSortThresholdsAscending = true;

    [Header("Other Events")]
    [Space(2f)]
    public UnityEvent onTickEachSecond;     // opsional, terpanggil tiap detik berganti
    [Space(2f)]
    public UnityEvent onTimerCompleted;     // terpanggil saat timer habis

    [Header("Component References")]
    [Space(2f)]
    [SerializeField] private TextMeshProUGUI timer_timeText;
    [SerializeField] private GameObject popup_objectparent;
    [SerializeField] private TextMeshProUGUI popup_informationText;

    public float RemainingTime => _remainingTime;
    public float Duration => _duration;
    public bool IsRunning => _isRunning;
    public bool IsPaused => _isPaused;

    float _duration;
    float _remainingTime;
    bool _isRunning;
    bool _isPaused;
    Coroutine _loop;
    int _lastWholeSecond;

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

            UpdateTimeTextUI(timer_timeText);

            yield return null;
        }

        _isRunning = false;
        _isPaused = false;
        onTimerCompleted?.Invoke();
        StopLoop();
    }

    private void UpdateTimeTextUI(TextMeshProUGUI _text)
    {
        var t = Mathf.CeilToInt(RemainingTime);
        int m = Mathf.Max(0, t / 60);
        int s = Mathf.Max(0, t % 60);
        _text.text = $"{m:00}:{s:00}";
    }

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

                // Tampilkan popup
                popup_objectparent.SetActive(true);
                popup_informationText.text = th.textInformation;

                // Animasi scale menggunakan DOTween
                popup_objectparent.transform.localScale = Vector3.zero; // Mulai dari scale 0
                popup_objectparent.transform.DOScale(Vector3.one, 0.3f); // Animasi scale ke 1 dalam 0.3 detik

                // Setelah 0.9 detik, animasikan scale kembali ke 0
                DOTween.Sequence()
                    .AppendInterval(th.delayTimeToHide)
                    .Append(popup_objectparent.transform.DOScale(Vector3.zero, 0.2f).OnComplete(() =>
                    {
                        popup_objectparent.SetActive(false);
                    })); // Animasi kembali ke scale 0 dalam 0.3 detik
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
