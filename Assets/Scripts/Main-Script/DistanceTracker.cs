using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WalkingTest
{
    public class DistanceTracker : MonoBehaviour
    {
        [Header("Result Data")]
        [SerializeField] private float _totalDistance = 0f;
        [SerializeField] private float _correctDistance = 0f;
        [SerializeField] private float _wrongDistance = 0f;

        [Header("Configuration")]
        [Tooltip("Layer untuk lintasan hijau (jalur benar).")]
        [SerializeField] private LayerMask _greenMask;
        [Tooltip("Layer untuk area merah (jalur salah).")]
        [SerializeField] private LayerMask _redMask;

        [Tooltip("Abaikan gerak sangat kecil (noise).")]
        [SerializeField] private float _minStep = 0.001f; // ~1 mm
        [Tooltip("Batas maksimum jarak per frame agar teleport tidak dihitung.")]
        [SerializeField] private float _maxStep = 2.0f; // 2 meter per frame (sesuaikan)

        [Header("Component References")]
        [SerializeField] private Transform _playerTransfrom;

        private Vector3 m_lastPosition;
        private bool m_isOnGreen = false;
        private bool m_isOnRed = false;
    }
}