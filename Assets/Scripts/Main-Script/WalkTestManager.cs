using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using UnityEngine.Events;

namespace WalkingTest
{
    public class WalkTestManager : MonoBehaviour
    {
        [Header("Component | Countdown & Timer")]
        [SerializeField] private float _testDuration = 360f;
        [SerializeField] private int _countdownDuration = 3;
        [SerializeField] private List<TimerThreshold> _2MWTInstruction = new List<TimerThreshold>() { };
        [SerializeField] private List<TimerThreshold> _6MWTInstruction = new List<TimerThreshold>() { };
        private List<TimerThreshold> m_thresholdEvents = new List<TimerThreshold>() { };
        private Action onTimerCompleted;
        private Coroutine _countdownRoutine;

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

        private float _duration;
        private float _remainingTime;
        // private bool _isRunning;
        private bool _isPaused;
        private Coroutine _loop;
        private int _lastWholeSecond;

        [Header("Component References")]
        [SerializeField] private ApplicationManager _applicationManager;
        [SerializeField] private CanvasManager _canvasManager;
        [SerializeField] private TrackWaypointGenerator _wayPointGenerator;
        [SerializeField] private Smartwatch _smartwatch;

        public void StartTrialTest()
        {
            _canvasManager.ShowPanel(5, () =>
            {
                if (_countdownRoutine != null)
                {
                    StopCoroutine(_countdownRoutine);
                    _countdownRoutine = null;
                }

                if (m_thresholdEvents.Count > 0) m_thresholdEvents.Clear();
                m_thresholdEvents = _2MWTInstruction;

                _countdownRoutine = StartCoroutine(CountdownRoutine(_countdownDuration, () =>
                {
                    _canvasManager.SetActiveCountDown(false, $"Jalan", "Jalan ketika hitungan selesai");
                    StartTimer(() =>
                    {
                        _canvasManager.ShowPanel(6, () =>
                        {
                            _applicationManager.NextStage();
                        });
                    });

                    //! hitung apakah player sudah melewati 1 putaran
                }));
            });
        }

        public void StartMainTest()
        {
            //! check apakah player sudah di titik start atau belum

            _canvasManager.ShowPanel(7, () =>
            {
                if (_countdownRoutine != null)
                {
                    StopCoroutine(_countdownRoutine);
                    _countdownRoutine = null;
                }

                if (m_thresholdEvents.Count > 0) m_thresholdEvents.Clear();
                m_thresholdEvents = _6MWTInstruction;

                _countdownRoutine = StartCoroutine(CountdownRoutine(_countdownDuration, () =>
                {
                    _canvasManager.SetActiveCountDown(false, $"Jalan", "Jalan ketika hitungan selesai");
                    StartTimer(() =>
                    {
                        _canvasManager.ShowPanel(8, () =>
                        {
                            _applicationManager.NextStage();
                        });

                        _wayPointGenerator.HideTrackway();
                        //! send player score
                    });
                }));
            });
        }

        #region Timer & Countdown
        private IEnumerator CountdownRoutine(int startValue, Action onFinished)
        {
            int current = startValue;
            while (current >= 0)
            {
                string val = current > 0 ? current.ToString() : "Jalan";
                _canvasManager.SetActiveCountDown(true, $"{val}", "Jalan ketika hitungan selesai");

                yield return new WaitForSeconds(1f);

                current--;
            }

            onFinished?.Invoke();
        }

        private void StartTimer(Action onFinish)
        {
            _duration = Mathf.Max(0f, _testDuration);

            if (_remainingTime <= 0f)
                _remainingTime = _duration;

            _isPaused = false;
            // _isRunning = true;
            _lastWholeSecond = Mathf.CeilToInt(_remainingTime);

            onTimerCompleted = null;
            onTimerCompleted = () => onFinish?.Invoke();

            ResetThresholdFlags();

            if (_loop == null)
                _loop = StartCoroutine(TimerLoop());

            TryFireThresholdEvents();
        }

        private IEnumerator TimerLoop()
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
                    // onTickEachSecond?.Invoke(); // ! fungsi yang dipanggil tiap detik
                }

                _smartwatch.SetTime(_remainingTime, _duration);

                yield return null;
            }

            // _isRunning = false;
            _isPaused = false;
            onTimerCompleted?.Invoke();
            StopLoop();
        }

        private void TryFireThresholdEvents()
        {
            // Kita cek semua threshold yang belum fired dan sekarang sudah berada di bawah/tepat pada ambang
            for (int i = 0; i < m_thresholdEvents.Count; i++)
            {
                var th = m_thresholdEvents[i];
                if (th._fired) continue;

                if (_remainingTime <= th.thresholdSeconds)
                {
                    th._fired = true;
                    th.onThreshold?.Invoke();

                    //! Tampilkan hint

                    // Animasi scale menggunakan DOTween
                    // Mulai dari scale 0
                    // Animasi scale ke 1 dalam 0.3 detik

                    // Setelah 0.9 detik, animasikan scale kembali ke 0
                    // Animasi kembali ke scale 0 dalam 0.3 detik
                }
            }
        }

        private void StopLoop()
        {
            if (_loop != null)
            {
                StopCoroutine(_loop);
                _loop = null;
            }
        }

        private void ResetTimer()
        {
            StopLoop();
            _remainingTime = _duration;
            // _isRunning = false;
            _isPaused = false;
            ResetThresholdFlags();
            _lastWholeSecond = Mathf.CeilToInt(_remainingTime);

            TryFireThresholdEvents();
        }

        private void ResetThresholdFlags()
        {
            for (int i = 0; i < m_thresholdEvents.Count; i++)
                m_thresholdEvents[i]._fired = false;
        }

        #endregion
    }
}