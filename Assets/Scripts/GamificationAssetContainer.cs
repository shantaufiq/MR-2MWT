using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace WalkingTest
{
    public class GamificationAssetContainer : MonoBehaviour
    {
        [Header("Not CW| Score Item & Star Badge")]
        [SerializeField] private List<ItemObject> _itemObjectList;
        [SerializeField] private TextMeshPro _scoreText;
        [SerializeField] private List<MeshRenderer> _starList;
        [SerializeField] private GameObject _boxItem;
        [SerializeField] private List<GameObject> _boxFill;

        [Header("CW | Score Item & Star Badge")]
        [SerializeField] private List<ItemObject> _CW_itemObjectList;
        [SerializeField] private TextMeshPro _CW_scoreText;
        [SerializeField] private List<MeshRenderer> _CW_starList;
        [SerializeField] private GameObject _CW_boxItem;
        [SerializeField] private List<GameObject> _CW_boxFill;

        [Header("Asset Parent")]
        [SerializeField] private GameObject m_clockWiseAsset;
        [SerializeField] private GameObject m_nonClockWiseAsset;
        [SerializeField] private StartAreaTrigger areaTrigger;

        private WalkTestManager m_walkTestManager;
        private bool _isClockWize;

        private void Awake()
        {
            if (m_walkTestManager == null)
            {
                m_walkTestManager = FindObjectOfType<WalkTestManager>();
            }
        }

        public void SpawnAsset(bool isClockWize)
        {
            if (m_walkTestManager == null)
            {
                m_walkTestManager = FindObjectOfType<WalkTestManager>();
            }

            m_clockWiseAsset.SetActive(isClockWize);
            m_nonClockWiseAsset.SetActive(!isClockWize);

            if (isClockWize)
            {
                m_walkTestManager.InitGamifiAsset(
                    _CW_itemObjectList,
                    _CW_boxItem,
                    _CW_scoreText,
                    _CW_starList,
                    _CW_boxFill,
                    areaTrigger
                );
            }
            else
            {
                m_walkTestManager.InitGamifiAsset(
                    _itemObjectList,
                    _boxItem,
                    _scoreText,
                    _starList,
                    _boxFill,
                    areaTrigger
                );
            }

            _isClockWize = isClockWize;
        }

        public void SetEnableAsset(bool isActive)
        {
            if (_isClockWize)
            {
                m_clockWiseAsset.SetActive(isActive);
            }
            else
            {
                m_nonClockWiseAsset.SetActive(isActive);
            }
        }
    }
}