using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    public class TestData
    {
        public float totalDistance;
        public float correctDistance;
        public float wrongDistance;
        public float walkingSpeed;
        public int totalLaps;
        private int stepsCount = 0;
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
        [SerializeField] private TestData _test2MWTData = new();
        [SerializeField] private TestData _test6MWTData = new();

        [Header("Component References")]
        [SerializeField] private CanvasManager _canvasManager;
        [SerializeField] private TrackWaypointGenerator _wayPointGenerator;
        [SerializeField] private WalkTestManager _walkTestManager;

        private void Start()
        {
            UpdateAppStage(AppState.Startup);
        }

        private void UpdateAppStage(AppState newStage)
        {
            _currentAppState = newStage;

            switch (_currentAppState)
            {
                case AppState.Startup:
                    _canvasManager.ShowPanel(0);
                    break;
                case AppState.Registration:
                    _canvasManager.ShowPanel(1);
                    // show validation mechanic in registration script
                    break;
                case AppState.Instruction:
                    _canvasManager.ShowPanel(2, () => _canvasManager.ShowPanel(3, () => NextStage()));
                    // show validation mechanic in registration script
                    break;
                case AppState.Settings:
                    _canvasManager.ShowPanel(4);
                    _wayPointGenerator.gameObject.SetActive(true); // show walking area
                    // mechanic walking area settings 
                    break;
                case AppState.Trial:
                    _walkTestManager.StartTrialTest();
                    break;
                case AppState.MainTest:
                    _walkTestManager.StartMainTest();
                    break;
                case AppState.Result:
                    _canvasManager.ShowResultPanel(_userData, _test2MWTData, _test6MWTData);
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
            }
        }

        public void StoreUserData(UserData newData)
        {
            if (newData == null)
            {
                return;
            }

            _userData.username = $"{newData.username}";
            _userData.age = newData.age;
            _userData.gender = newData.gender;

            NextStage();
        }
    }
}