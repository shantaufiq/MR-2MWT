using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace WalkingTest
{
    public class CanvasManager : MonoBehaviour
    {
        [Header("All Panel Config")]
        [SerializeField] private List<GameObject> panels = new List<GameObject>();
        private int m_currentPanelIndex = 0;

        [Header("Panel | Result")]
        [SerializeField] private UserData _userData;
        [SerializeField] private TestData _2mwtData;
        [SerializeField] private TestData _6mwtData;
        [SerializeField] private GameObject _contentSection;
        [SerializeField] private UIToggleButtonsGroup _toggleMenu;
        [SerializeField] private TextMeshProUGUI _textUsername;
        [SerializeField] private TextMeshProUGUI _textAge;
        [SerializeField] private TextMeshProUGUI _textGender;
        [SerializeField] private TextMeshProUGUI _textTotalJarak;
        [SerializeField] private TextMeshProUGUI _textLaps;
        [SerializeField] private TextMeshProUGUI _textPace;
        [SerializeField] private TextMeshProUGUI _textTitleSummary;
        [SerializeField] private TextMeshProUGUI _textDescriptionSummary;

        [Header("Popup | Confirmation")]
        [SerializeField] private GameObject _popupConfirmation;
        [SerializeField] private TextMeshProUGUI _textTitle;
        [SerializeField] private TextMeshProUGUI _textMessage;
        [SerializeField] private Button _buttonAgree;
        [SerializeField] private Button _buttonDisagree;

        [Header("Popup | Time Coundown")]
        [SerializeField] private GameObject _popupCountdown;
        [SerializeField] private TextMeshProUGUI _textCountdown;
        [SerializeField] private TextMeshProUGUI _textInstruction;

        void Start()
        {
            ShowPanel(m_currentPanelIndex);
        }

        private void Update()
        {
            /* if (Input.GetKeyDown(KeyCode.Space))
            {
                m_currentPanelIndex += 1;
                ShowPanel(m_currentPanelIndex);
            } */
        }

        public void ShowPanel(int index, Action onClickMainButon = null)
        {
            if (index < 0 || index >= panels.Count)
            {
                Debug.LogWarning("Index panel di luar jangkauan: " + index);
                return;
            }

            // Matikan semua panel dulu
            foreach (GameObject p in panels)
            {
                if (p != null)
                    p.SetActive(false);
            }

            if (onClickMainButon != null)
            {
                panels[index].GetComponentInChildren<Button>().onClick.RemoveAllListeners();
                panels[index].GetComponentInChildren<Button>().onClick.AddListener(() =>
                {
                    onClickMainButon?.Invoke();
                });
            }

            // Aktifkan panel yang dipilih
            if (panels[index] != null)
                panels[index].SetActive(true);
        }

        public void ShowPopupConfirmation(string title, string message, Action onAgree, Action onDisagree)
        {
            foreach (GameObject p in panels)
            {
                if (p != null)
                    p.SetActive(false);
            }

            _popupConfirmation.SetActive(true);

            _textTitle.text = title;
            _textMessage.text = message;

            _buttonAgree.onClick.RemoveAllListeners();
            _buttonAgree.onClick.AddListener(() =>
            {
                onAgree.Invoke();
                _popupConfirmation.SetActive(false);
            });

            _buttonDisagree.onClick.RemoveAllListeners();
            _buttonDisagree.onClick.AddListener(() =>
            {
                onDisagree.Invoke();
                _popupConfirmation.SetActive(false);
            });
        }

        public void ShowResultPanel(UserData userData, TestData tesA, TestData tesB)
        {
            _userData = userData;
            _2mwtData = tesA;
            _6mwtData = tesB;
            _contentSection.SetActive(true);

            ShowPanel(9);
            _toggleMenu.SetActiveIndex(0);
        }

        public void Set2MWTData()
        {
            _textUsername.text = _userData.username;
            _textAge.text = _userData.age.ToString();
            _textGender.text = _userData.gender == UserData.Gender.Male ? $"Laki-laki" : $"Perempuan";
            _textTotalJarak.text = _2mwtData.totalDistance.ToString();
            _textLaps.text = _2mwtData.totalLaps.ToString();
            _textPace.text = $"{_2mwtData.walkingSpeed:F2}";
            _textTitleSummary.text = $"2 Minutes Walking Test";
            _textDescriptionSummary.text = $"Hasil tes ini menunjukkan seberapa jauh Anda dapat berjalan dalam 2 menit, sebagai gambaran kondisi kemampuan dan daya tahan fisik Anda saat ini.";
        }

        public void Set6MWTData()
        {
            _textUsername.text = _userData.username;
            _textAge.text = _userData.age.ToString();
            _textGender.text = _userData.gender == UserData.Gender.Male ? $"Laki-laki" : $"Perempuan";
            _textTotalJarak.text = _6mwtData.totalDistance.ToString();
            _textLaps.text = _6mwtData.totalLaps.ToString();
            _textPace.text = $"{_6mwtData.walkingSpeed:F2}";
            _textTitleSummary.text = $"6 Minutes Walking Test";
            _textDescriptionSummary.text = $"Hasil tes ini menunjukkan seberapa jauh Anda dapat berjalan dalam 6 menit, sebagai gambaran kondisi kemampuan dan daya tahan fisik Anda saat ini.";
        }

        public void SetActiveCountDown(bool isActive, string val, string instruction)
        {
            foreach (GameObject p in panels)
            {
                if (p != null)
                    p.SetActive(false);
            }

            _popupCountdown.SetActive(isActive);

            if (isActive)
            {
                _textCountdown.text = val;
                _textInstruction.text = instruction;
            }
        }
    }
}