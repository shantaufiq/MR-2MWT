using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LineController : MonoBehaviour
{
    [SerializeField] private LineRenderer _lineRenderer;
    [SerializeField] private Transform[] _points;

    private void Start()
    {
        _lineRenderer.positionCount = _points.Length;
        for (int i = 0; i < _points.Length; i++)
        {
            _lineRenderer.SetPosition(i, _points[i].position);
        }
    }

    public void UpdateLinePoints(Transform[] points)
    {
        _lineRenderer.positionCount = points.Length;
        this._points = points;
    }
}
