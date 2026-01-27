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

        private WalkTestManager m_walkTestManager;

        private void Awake()
        {
            m_walkTestManager = FindObjectOfType<WalkTestManager>();
        }

        public void SpawnAsset(bool isClockWize)
        {

            if (isClockWize)
            {
                m_clockWiseAsset.SetActive(true);
                m_nonClockWiseAsset.SetActive(false);

                m_walkTestManager.InitGamifiAsset(
                    _CW_itemObjectList,
                    _CW_boxItem,
                    _CW_scoreText,
                    _CW_starList,
                    _CW_boxFill
                );
            }
            else
            {
                m_clockWiseAsset.SetActive(false);
                m_nonClockWiseAsset.SetActive(true);

                m_walkTestManager.InitGamifiAsset(
                    _itemObjectList,
                    _boxItem,
                    _scoreText,
                    _starList,
                    _boxFill
                );
            }
        }
    }
}