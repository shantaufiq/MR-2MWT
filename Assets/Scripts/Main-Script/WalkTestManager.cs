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
        [SerializeField] private SFXObject _countdownAudio;
        [SerializeField] private List<TimerThreshold> _trialInstruction = new List<TimerThreshold>() { };
        [SerializeField] private List<TimerThreshold> _mainTestInstruction = new List<TimerThreshold>() { };
        private List<TimerThreshold> m_thresholdEvents = new List<TimerThreshold>() { };
        private Action onTimerCompleted;
        private Coroutine _countdownRoutine;

        [System.Serializable]
        public class TimerThreshold
        {
            [Tooltip("Label opsional agar mudah dikenali di Inspector.")]
            public string name;
            public PopupType type;

            [Serializable]
            public enum PopupType
            {
                None, Popup, Hint
            }

            [TextArea]
            public string textInformation;
            public SFXObject audioClip;
            public Sprite icon;

            [Header("Settings")]
            [Tooltip("Saat RemainingTime <= nilai ini (detik), event dipanggil.")]
            [Min(0f)] public float thresholdSeconds = 60f;
            [Range(0.7f, 10f)]
            public float delayTimeToHide = 0.8f;

            [Space(8f)]
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
            SFXManager.Main.StopAll();
            SFXManager.Main.PlayFromSFXObjectLibrary("5trialintro");
            _canvasManager.ShowPanel(5, () =>
            {
                if (_countdownRoutine != null)
                {
                    StopCoroutine(_countdownRoutine);
                    _countdownRoutine = null;
                }
                SFXManager.Main.StopAll();

                if (m_thresholdEvents.Count > 0) m_thresholdEvents.Clear();
                m_thresholdEvents = _trialInstruction;

                _countdownRoutine = StartCoroutine(CountdownRoutine(() =>
                {
                    _wayPointGenerator.onReachingLap.AddListener((int n) =>
                    {
                        if (n == 1)
                        {
                            ResetTimer();

                            _canvasManager.ShowPanel(6, () =>
                            {
                                _applicationManager.NextStage();
                            });
                            SFXManager.Main.PlayFromSFXObjectLibrary("6trialsuccess");
                        }
                    });

                    _canvasManager.SetActiveCountDown(false, $"", "");
                    StartTimer(() =>
                    {
                        _canvasManager.SetActiveCountDown(false, $"", "");
                        _canvasManager.ShowPanel(6, () =>
                        {
                            _applicationManager.NextStage();
                        });
                        SFXManager.Main.PlayFromSFXObjectLibrary("6trialsuccess");
                    });

                    //! hitung apakah player sudah melewati 1 putaran
                }));
            });
        }

        public void StartMainTest()
        {
            //! check apakah player sudah di titik start atau belum

            SFXManager.Main.StopAll();
            SFXManager.Main.PlayFromSFXObjectLibrary("7testintro");
            _canvasManager.ShowPanel(7, () =>
            {
                if (_countdownRoutine != null)
                {
                    StopCoroutine(_countdownRoutine);
                    _countdownRoutine = null;
                }
                SFXManager.Main.StopAll();

                if (m_thresholdEvents.Count > 0) m_thresholdEvents.Clear();
                m_thresholdEvents = _mainTestInstruction;

                _countdownRoutine = StartCoroutine(CountdownRoutine(() =>
                {
                    _canvasManager.SetActiveCountDown(false, $"", "J");
                    StartTimer(() =>
                    {
                        SFXManager.Main.PlayFromSFXObjectLibrary("8testsuccess");
                        _canvasManager.SetActiveCountDown(false, $"", "");
                        _wayPointGenerator.HideTrackway();
                        StoreTestResult();

                        _canvasManager.ShowPanel(8, () =>
                        {
                            _applicationManager.NextStage();
                        });
                    });
                }));
            });
        }

        private void StoreTestResult()
        {
            TestResultData _2mwt = new();
            _2mwt.totalDistance = 12;
            _2mwt.correctWay = 10;
            _2mwt.wrongWay = 2;
            _2mwt.walkingSpeed = 5;
            _2mwt.totalLaps = 3;
            _2mwt.stepsCount = 50;

            TestResultData _6mwt = new();
            _6mwt.totalDistance = 24;
            _6mwt.correctWay = 20;
            _6mwt.wrongWay = 4;
            _6mwt.walkingSpeed = 3;
            _6mwt.totalLaps = 6;
            _6mwt.stepsCount = 100;

            _applicationManager.StoreTestResultData(_2mwt, _6mwt);
        }

        #region Timer & Countdown
        private IEnumerator CountdownRoutine(Action onFinished)
        {
            SFXManager.Main.Play(_countdownAudio);
            int current = 4;
            while (current >= 0)
            {
                if (current == 4)
                {
                    _canvasManager.SetActiveCountDown(true, $"Bersiap", "Jalan ketika hitungan selesai");
                }
                else
                {
                    string val = current > 0 ? current.ToString() : "Jalan";
                    _canvasManager.SetActiveCountDown(true, $"{val}", "Jalan ketika hitungan selesai");
                }

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

                    if (Mathf.Abs(_remainingTime - th.thresholdSeconds) < 3f)
                    {
                        if (th.type == TimerThreshold.PopupType.Hint && th.icon)
                        {
                            _canvasManager.SetActiveHint(th.icon, th.textInformation, th.delayTimeToHide);
                        }

                        if (th.type == TimerThreshold.PopupType.Popup)
                        {
                            _canvasManager.SetActiveCountDown(true, $"{th.textInformation}", "Berhenti ketika waktu selesai");
                        }

                        if (th.audioClip)
                            SFXManager.Main.Play(th.audioClip);
                    }
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