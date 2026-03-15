using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using UnityEngine.Events;
using System.Linq;

namespace WalkingTest
{
    public class WalkTestManager : MonoBehaviour
    {
        [Header("Walking Test Result Data")]
        [SerializeField] private TestResultData _2MWTData = new();
        [SerializeField] private TestResultData _6MWTData = new();

        [Header("Score Item & Star Badge")]
        [SerializeField] private int _collectedItemCount = 0;
        [SerializeField] private List<ItemObject> _itemObjectList;
        [SerializeField] private GameObject _boxItem;
        [SerializeField] private TextMeshPro _scoreText;
        [SerializeField] private List<MeshRenderer> _starList;
        [SerializeField] private List<GameObject> _boxFill;
        [SerializeField] private Material _yellowMaterial;
        [SerializeField] private Material _greyMaterial;
        [SerializeField] private StartAreaTrigger _areaTrigger;

        public void InitGamifiAsset(
            List<ItemObject> itemList,
            GameObject boxItem,
            TextMeshPro scoreText,
            List<MeshRenderer> starList,
            List<GameObject> boxFill,
            StartAreaTrigger areaTrigger
        )
        {
            _itemObjectList = itemList;
            _boxItem = boxItem;
            _scoreText = scoreText;
            _starList = starList;
            _boxFill = boxFill;
            _areaTrigger = areaTrigger;
        }

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
        // waktu yang sudah lewat (detik)
        public float ElapsedTime { get; private set; }
        public float Duration => _duration;

        private bool _isPaused;
        private Coroutine _loop;
        private int _lastWholeSecond;

        [Header("Component References")]
        [SerializeField] private ApplicationManager _applicationManager;
        [SerializeField] private CanvasManager _canvasManager;
        [SerializeField] private TrackWaypointGenerator _wayPointGenerator;
        [SerializeField] private Smartwatch _smartwatch;
        // [SerializeField] private DistanceTracker _distanceTracker; // akan diganti denga vr distance tracker
        [SerializeField] private MRWalkingTracker_Quest3_MixamoFootSteps _MRDistanceTracker; // akan diganti denga vr distance tracker

        public void ShowTrack()
        {
            _wayPointGenerator.SpawnFloorAtPlayer();
        }

        public void StartTrialTest(Action onHideCanvas = null)
        {
            SFXManager.Main.StopAll();
            SFXManager.Main.PlayFromSFXObjectLibrary("5trialintro");

            ResetStar();
            _boxItem.SetActive(false);
            ResetScore();
            _smartwatch.ResetVisualValue();

            _areaTrigger.isStartAreaTriggerActive = true;
            _areaTrigger.mesh.enabled = true;

            _canvasManager.ShowPanel(5, () =>
            {
                _areaTrigger.isStartAreaTriggerActive = false;
                _areaTrigger.mesh.enabled = false;

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
                    _MRDistanceTracker.StartTracking();

                    onHideCanvas?.Invoke();

                    // ssetup object score
                    foreach (var obj in _itemObjectList)
                    {
                        obj.gameObject.SetActive(true);
                        obj._walkTestManager = this;
                    }

                    //! hitung apakah player sudah melewati 1 putaran
                    _wayPointGenerator.onReachingLap.RemoveAllListeners();
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
                }));
            });
        }

        public void StartMainTest(Action onHideCanvas = null)
        {
            _areaTrigger.isStartAreaTriggerActive = true;
            _areaTrigger.mesh.enabled = true;

            SFXManager.Main.StopAll();
            SFXManager.Main.PlayFromSFXObjectLibrary("7testintro");

            ResetScore();
            ResetStar();
            _boxItem.SetActive(true);

            _smartwatch.ResetVisualValue();

            _canvasManager.ShowPanel(7, () =>
            {
                _areaTrigger.isStartAreaTriggerActive = false;
                _areaTrigger.mesh.enabled = false;

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
                    onHideCanvas?.Invoke();

                    _wayPointGenerator.onReachingLap.RemoveAllListeners();
                    _wayPointGenerator.onReachingLap.AddListener((int n) =>
                    {
                        /* foreach (var obj in _itemObjectList)
                        {
                            obj.gameObject.SetActive(true);
                        } */

                        if (n > 0 && n <= _boxFill.Count)
                        {
                            _boxFill[n - 1].SetActive(true);
                        }
                    });

                    foreach (var obj in _itemObjectList)
                    {
                        obj.gameObject.SetActive(true);
                        obj._walkTestManager = this;
                    }

                    _MRDistanceTracker.StartTracking();
                    _canvasManager.SetActiveCountDown(false, $"", "J");
                    StartTimer(() =>
                    {
                        _MRDistanceTracker.StopTracking();
                        _MRDistanceTracker.GetResult((x) => _6MWTData.totalDistance = x, (x) => _6MWTData.correctWay = x, (x) => _6MWTData.wrongWay = x);
                        _6MWTData.totalLaps = _wayPointGenerator.lapsCompleted;
                        _6MWTData.walkingSpeed = _smartwatch.GetAverageSpeed();

                        SFXManager.Main.PlayFromSFXObjectLibrary("8testsuccess");
                        _canvasManager.SetActiveCountDown(false, $"", "");
                        _wayPointGenerator.HideTrack();
                        StoreTestResult();

                        _canvasManager.ShowPanel(8, () =>
                        {
                            _applicationManager.NextStage();
                        });

                        foreach (var obj in _itemObjectList)
                        {
                            obj.gameObject.SetActive(false);
                        }
                    });
                }));
            });
        }


        #region User Data Handler
        private void StoreTestResult()
        {
            _applicationManager.StoreTestResultData(_2MWTData, _6MWTData);
        }

        public void GetData2MWT() // call from event inspector
        {
            _MRDistanceTracker.GetResult((x) => _2MWTData.totalDistance = x, (x) => _2MWTData.correctWay = x, (x) => _2MWTData.wrongWay = x);
            _2MWTData.totalLaps = _wayPointGenerator.lapsCompleted;
            _2MWTData.walkingSpeed = _smartwatch.GetAverageSpeed();
        }

        private float CountAvarageSpeedPerMin(float totalDistance, float totalWalkingTimeSeconds)
        {
            if (totalWalkingTimeSeconds <= 0.0001f) return 0f;
            return (totalDistance / totalWalkingTimeSeconds) * 60f;
        }
        #endregion

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

            ElapsedTime = Mathf.Clamp(_duration - _remainingTime, 0f, _duration); // reset sesuai kondisi

            _isPaused = false;
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

                ElapsedTime = Mathf.Clamp(_duration - _remainingTime, 0f, _duration);

                TryFireThresholdEvents();

                int currentWhole = Mathf.CeilToInt(_remainingTime);
                if (currentWhole != _lastWholeSecond)
                {
                    _lastWholeSecond = currentWhole;
                    // onTickEachSecond?.Invoke(); // ! fungsi yang dipanggil tiap detik
                }

                _smartwatch.SetTime(_remainingTime, _duration); // fungsi yang akan menampilkan durasi waktu yang tersisah

                float currentDistance = _MRDistanceTracker.TotalDistance;
                _smartwatch.SetDinstance(currentDistance);
                _smartwatch.SetAverageSpeedPerMin(CountAvarageSpeedPerMin(currentDistance, ElapsedTime));

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
            ElapsedTime = 0f;

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

        #region Score & Star badge
        public void AddScore(int newPoint)
        {
            _collectedItemCount += newPoint;
            _scoreText.text = $"{_collectedItemCount}";
            Debug.Log($"score collected +{newPoint} | total : {_collectedItemCount}");

            if (_itemObjectList.Count(x => !x.gameObject.activeSelf) >= 9 && m_thresholdEvents == _mainTestInstruction)
            {
                foreach (var obj in _itemObjectList)
                {
                    obj.gameObject.SetActive(true);
                }
            }
        }

        public void ResetScore()
        {
            _collectedItemCount = 0;
            _scoreText.text = $"{_collectedItemCount}";
        }

        public void ShowStar(int starIndex)
        {
            if (starIndex > 5) return;

            SFXManager.Main.PlayFromSFXObjectLibrary("badge");
            _starList[starIndex].material = _yellowMaterial;
        }

        public void ResetStar()
        {
            foreach (var star in _starList)
            {
                star.material = _greyMaterial;
            }
        }
        #endregion
    }
}