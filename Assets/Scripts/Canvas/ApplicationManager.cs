using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
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

    public class ApplicationManager : MonoBehaviour
    {
        [Serializable]
        public enum AppState
        {
            Startup = 1, Settings = 2, Registration = 3, Instruction = 4, Trial = 5, MainTest = 6, Result = 7
        }

        [Header("APP DATA")]
        [SerializeField] private AppState _currentAppState = AppState.Startup;
        [SerializeField] private UserData _userData = new UserData();

        [Header("Component References")]
        [SerializeField] private CanvasManager _canvasManager;

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
                case AppState.Settings:
                    _canvasManager.ShowPanel(1);
                    // show walking area
                    // mechanic walking area settings 
                    break;
                case AppState.Registration:
                    _canvasManager.ShowPanel(2);
                    // show validation mechanic in registration script
                    break;
                case AppState.Instruction:
                    _canvasManager.ShowPanel(3);
                    // show validation mechanic in registration script
                    break;
            }
        }

        public void NextStage()
        {
            switch (_currentAppState)
            {
                case AppState.Startup:
                    UpdateAppStage(AppState.Settings);
                    break;
                case AppState.Settings:
                    /* if () // cek kondisi sudah setup apa belum ?
                    {

                    } */
                    UpdateAppStage(AppState.Registration);
                    break;
                case AppState.Registration:
                    UpdateAppStage(AppState.Instruction);
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