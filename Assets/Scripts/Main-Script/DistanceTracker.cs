using System;
using UnityEngine;

public class DistanceTracker : MonoBehaviour
{
    [Header("Result Data")]
    [SerializeField] private float _totalDistance = 0f;
    [SerializeField] private float _correctDistance = 0f;
    [SerializeField] private float _wrongDistance = 0f;

    [Header("Configuration")]
    [SerializeField] private LayerMask _greenMask;
    [SerializeField] private LayerMask _redMask;

    [SerializeField] private float _minStep = 0.001f;
    [SerializeField] private float _maxStep = 2.0f;

    [Header("Component References")]
    [SerializeField] private CharacterController _controller;
    [SerializeField] private Transform _playerTransform;

    private bool m_isOnGreen = false;
    private bool m_isOnRed = false;
    private bool m_isTracking = false;

    private void Awake()
    {
        if (_controller == null)
            _controller = GetComponent<CharacterController>();

        if (_playerTransform == null)
            _playerTransform = transform;
    }

    private void Update()
    {
        if (m_isTracking)
            UpdateGroundState();
    }

    private void FixedUpdate()
    {
        if (m_isTracking)
            TrackDistance();
    }

    public void StartTracking()
    {
        m_isTracking = true;

        Debug.Log($"start tracking: {m_isTracking}");

        _totalDistance = 0f;
        _correctDistance = 0f;
        _wrongDistance = 0f;

        UpdateGroundState(); // Set ground state awal
    }

    public void StopTracking()
    {
        m_isTracking = false;
    }

    public void GetResult(Action<float> total, Action<float> correct, Action<float> wrong)
    {
        total?.Invoke(_totalDistance);
        correct?.Invoke(_correctDistance);
        wrong?.Invoke(_wrongDistance);
    }

    private void UpdateGroundState()
    {
        Vector3 origin = _playerTransform.position + Vector3.up * 0.2f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2f))
        {
            int layerMask = 1 << hit.collider.gameObject.layer;

            m_isOnGreen = (_greenMask.value & layerMask) != 0;
            m_isOnRed = (_redMask.value & layerMask) != 0;
        }
        else
        {
            m_isOnGreen = false;
            m_isOnRed = false;
        }

        // Debug.Log($"Is correct way: {m_isOnGreen}");
    }

    private void TrackDistance()
    {
        Vector3 v = _controller.velocity;
        v.y = 0f;

        float distanceThisFrame = v.magnitude * Time.deltaTime;

        if (distanceThisFrame >= _minStep && distanceThisFrame <= _maxStep)
        {
            _totalDistance += distanceThisFrame;

            // Debug.Log($"jarak: {_totalDistance}");

            if (m_isOnGreen && !m_isOnRed)
                _correctDistance += distanceThisFrame;
            else
                _wrongDistance += distanceThisFrame;
        }
    }
}