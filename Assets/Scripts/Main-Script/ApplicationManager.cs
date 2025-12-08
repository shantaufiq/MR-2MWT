using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WalkingTest
{
    [Serializable]
    public class UserData
    {
        public string username;
        public int age;
        public Gender gender;
        [Serializable]
        public enum Gender
        {
            None, Male, Female
        }
    }

    [Serializable]
    public class TestResultData
    {
        public float totalDistance;
        public float correctWay;
        public float wrongWay;
        public float walkingSpeed;
        public int totalLaps;
        public int stepsCount = 0;
    }

    public class ApplicationManager : MonoBehaviour
    {
        [Serializable]
        public enum AppState
        {
            Startup = 1, Registration = 2, Instruction = 3, Settings = 4, Trial = 5, MainTest = 6, Result = 7
        }

        [Header("APP DATA")]
        [SerializeField] private AppState _currentAppState = AppState.Startup;
        [SerializeField] private UserData _userData = new();
        [SerializeField] private TestResultData _test2MWTData = new();
        [SerializeField] private TestResultData _test6MWTData = new();

        [Header("Component References")]
        [SerializeField] private CanvasManager _canvasManager;
        [SerializeField] private TrackWaypointGenerator _wayPointGenerator;
        [SerializeField] private WalkTestManager _walkTestManager;

        private void Start()
        {
            MusicManager.Main.SetVolume(.2f, 0f);
            SFXManager.Main.SetVolume(.8f, 0f);
            MusicManager.Main.PlayFromLibrary("backsound");

            UpdateAppStage(AppState.Startup);
        }

        private void UpdateAppStage(AppState newStage)
        {
            _currentAppState = newStage;

            switch (_currentAppState)
            {
                case AppState.Startup:
                    _canvasManager.ShowPanel(0, () =>
                    {
                        NextStage();
                    });
                    break;
                case AppState.Registration:
                    SFXManager.Main.PlayFromSFXObjectLibrary("1datadiri");
                    _canvasManager.ShowPanel(1);
                    // show validation mechanic in registration script
                    break;
                case AppState.Instruction:
                    SFXManager.Main.StopAll();
                    SFXManager.Main.PlayFromSFXObjectLibrary("2tentang");
                    _canvasManager.ShowPanel(2, () => _canvasManager.ShowPanel(3, () =>
                    {
                        SFXManager.Main.StopAll();
                        NextStage();
                    }));
                    // show validation mechanic in registration script
                    break;
                case AppState.Settings:
                    _canvasManager.ShowPanel(4, () => NextStage());
                    _wayPointGenerator.gameObject.SetActive(true); // show walking area
                    break;
                case AppState.Trial:
                    _walkTestManager.StartTrialTest();
                    break;
                case AppState.MainTest:
                    _walkTestManager.StartMainTest();
                    break;
                case AppState.Result:
                    SFXManager.Main.PlayFromSFXObjectLibrary("9hasiltest");
                    _canvasManager.ShowResultPanel(_userData, _test2MWTData, _test6MWTData, () =>
                    {
                        SFXManager.Main.StopAll();
                        NextStage();
                    });
                    break;
            }
        }

        public void NextStage()
        {
            switch (_currentAppState)
            {
                case AppState.Startup:
                    UpdateAppStage(AppState.Registration);
                    break;
                case AppState.Registration:
                    if (_userData == null)
                    {
                        UpdateAppStage(AppState.Registration);
                    }
                    UpdateAppStage(AppState.Instruction);
                    break;
                case AppState.Instruction:
                    UpdateAppStage(AppState.Settings);
                    break;
                case AppState.Settings:
                    SFXManager.Main.PlayFromSFXObjectLibrary("4corfirtrial");
                    _canvasManager.ShowPopupConfirmation(
                        title: $"Mulai Percobaan",
                        message: $"Sebelum tes, apakah Kamu ingin melakukan percobaan dulu?",
                        onAgree: () =>
                        {
                            UpdateAppStage(AppState.Trial);
                        },
                        onDisagree: () =>
                        {
                            UpdateAppStage(AppState.MainTest);
                        }
                    );
                    break;
                case AppState.Trial:
                    UpdateAppStage(AppState.MainTest);
                    break;
                case AppState.MainTest:
                    UpdateAppStage(AppState.Result);
                    break;
                case AppState.Result:
                    SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
                    break;
            }
        }

        public void StoreUserData(UserData newData)
        {
            if (newData == null)
            {
                return;
            }

            _userData = newData;

            NextStage();
        }

        public void StoreTestResultData(TestResultData _new2MWT, TestResultData _new6MWT)
        {
            if (_new2MWT == null || _new6MWT == null)
            {
                return;
            }

            _test2MWTData = _new2MWT;
            _test6MWTData = _new6MWT;
        }
    }
}