using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;

namespace WalkingTest
{
    public class WalkTestManager : MonoBehaviour
    {
        [Header("Component | Countdown & Timer")]
        [SerializeField] private int _countdownDuration = 3;
        private Coroutine _countdownRoutine;

        [Header("Component References")]
        [SerializeField] private ApplicationManager _applicationManager;
        [SerializeField] private CanvasManager _canvasManager;

        public void StartTrialTest()
        {
            _canvasManager.ShowPanel(5, () =>
            {
                if (_countdownRoutine != null)
                {
                    StopCoroutine(_countdownRoutine);
                }

                _countdownRoutine = StartCoroutine(CountdownRoutine(_countdownDuration, () =>
                {
                    _canvasManager.SetActiveCountDown(false, $"Mulai", "Jalan ketika hitungan selesai");
                    Debug.Log($"starting game...");
                }));

                // _canvasManager.ShowPanel(6, () =>
                // {
                //     _applicationManager.NextStage();
                // });
            });
        }

        public void StartMainTest()
        {
            _canvasManager.ShowPanel(7, () =>
            {
                _canvasManager.ShowPanel(8, () =>
                {
                    _applicationManager.NextStage();
                });
            });
        }

        private IEnumerator CountdownRoutine(int startValue, Action onFinished)
        {
            int current = startValue;

            while (current >= 0)
            {
                // Ubah teks di "local" (UI komputer kita)

                string val = current > 0 ? current.ToString() : "Mulai";

                _canvasManager.SetActiveCountDown(true, $"{val}", "Jalan ketika hitungan selesai");

                yield return new WaitForSeconds(1f);

                current--;
            }

            onFinished?.Invoke();
        }
    }
}