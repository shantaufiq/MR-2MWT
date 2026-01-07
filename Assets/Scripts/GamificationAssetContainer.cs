using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace WalkingTest
{
    public class GamificationAssetContainer : MonoBehaviour
    {
        [Header("Score Item & Star Badge")]
        [SerializeField] private List<ItemObject> _itemObjectList;
        [SerializeField] private TextMeshPro _scoreText;
        [SerializeField] private List<MeshRenderer> _starList;
        [SerializeField] private GameObject _boxItem;
        [SerializeField] private List<GameObject> _boxFill;

        private void Awake()
        {
            WalkTestManager _walkTestManager = FindObjectOfType<WalkTestManager>();

            _walkTestManager.InitGamifiAsset(
                _itemObjectList,
                _boxItem,
                _scoreText,
                _starList,
                _boxFill
            );
        }
    }
}