using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class PanelController : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private GameObject _panel;

    [Header("Animation Settings")]
    public float onActiveValue = 0f;
    public float onDeactiveValue = -1103f;

    public void SetActivePanelController(bool setActive)
    {
        _panel.SetActive(setActive);

        if (setActive)
        {
            _panel.GetComponent<RectTransform>().DOAnchorPosY(onActiveValue, 0.8f).SetEase(Ease.OutCubic);
        }
        else
        {
            _panel.GetComponent<RectTransform>().DOAnchorPosY(onDeactiveValue, 0.8f).SetEase(Ease.OutCubic);
        }
    }
}
