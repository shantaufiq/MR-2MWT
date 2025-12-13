using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WalkingTest
{
    public class Smartwatch : MonoBehaviour
    {
        [Header("Progress Bar Settings")]
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _criticalColor = Color.red;
        [SerializeField] private float _criticalThresholdSeconds = 5f;

        [Header("Component References")]
        [SerializeField] private GameObject _smartwatchModel;
        [SerializeField] private Image _progressBar;
        [SerializeField] private TextMeshProUGUI _textTimer;
        [SerializeField] private TextMeshProUGUI _textSpeed;
        [SerializeField] private TextMeshProUGUI _textDistance;

        private void Awake()
        {
            // Set warna awal sesuai setting inspector
            if (_progressBar != null)
            {
                _progressBar.color = _normalColor;
            }
        }

        public void ResetVisualValue()
        {
            _textTimer.text = $"00:00";
            _textSpeed.text = $"0.00 m/menit";
            _textDistance.text = $"0,00m";
        }

        public void SetTime(float remainingTime, float totalDuration)
        {
            if (_progressBar == null || totalDuration <= 0f)
                return;

            // 1 = penuh di awal, 0 = habis di akhir
            float normalized = Mathf.InverseLerp(0f, totalDuration, remainingTime);
            _progressBar.fillAmount = normalized;

            // Ubah warna sesuai sisa waktu
            if (remainingTime <= _criticalThresholdSeconds)
            {
                _progressBar.color = _criticalColor;
            }
            else
            {
                _progressBar.color = _normalColor;
            }

            var t = Mathf.CeilToInt(remainingTime);
            int m = Mathf.Max(0, t / 60);
            int s = Mathf.Max(0, t % 60);

            _textTimer.text = $"{m:00}:{s:00}";
        }

        public void SetAverageSpeedPerMin(float speedAverage)
        {
            _textSpeed.text = $"{speedAverage:F0} m/menit";
        }

        public void SetDinstance(float totalDistance)
        {
            int meters = Mathf.FloorToInt(totalDistance);
            int centimeters = Mathf.FloorToInt((totalDistance - meters) * 100f);
            _textDistance.text = $"{meters},{centimeters}m";
        }

        public void SetSmartwatchVisible(bool visible)
        {
            if (_smartwatchModel != null)
                _smartwatchModel.SetActive(visible);
        }
    }
}