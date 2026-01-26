using System;
using System.Collections;
using System.Collections.Generic;
using Autohand;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace WalkingTest
{
    public class CanvasManager : MonoBehaviour
    {
        [Header("Panel Config")]
        [SerializeField] private List<PanelComponent> panels = new List<PanelComponent>();

        [Serializable]
        public struct PanelComponent
        {
            public GameObject gameObject;
            public Button buttonNext;
        }

        private int m_currentPanelIndex = 0;

        [Header("Component References")]
        [SerializeField] private WorldCanvasFollower _canvasFollowingPlayer;

        [Header("Panel | Result")]
        [SerializeField] private UserData _userData;
        [SerializeField] private TestResultData _2mwtData;
        [SerializeField] private TestResultData _6mwtData;
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
        private float m_timeToHide = 0f;
        private Coroutine m_hideRoutine;

        [Header("Popup | Hint")]
        [SerializeField] private GameObject _popupHint;
        [SerializeField] private Image _imageHint;
        [SerializeField] private TextMeshProUGUI _textHint;

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
            foreach (var p in panels)
            {
                if (p.gameObject != null)
                    p.gameObject.SetActive(false);
            }

            var button = panels[index].buttonNext;

            if (onClickMainButon != null)
                button.onClick.RemoveAllListeners();

            button.onClick.AddListener(() =>
            {
                onClickMainButon?.Invoke();
                SFXManager.Main.PlayFromSFXObjectLibrary("button-click");
            });

            // Aktifkan panel yang dipilih
            if (panels[index].gameObject != null)
                panels[index].gameObject.SetActive(true);
        }

        public void ShowPopupConfirmation(string title, string message, Action onAgree, Action onDisagree)
        {
            foreach (var p in panels)
            {
                if (p.gameObject != null)
                    p.gameObject.SetActive(false);
            }

            _popupConfirmation.SetActive(true);

            _textTitle.text = title;
            _textMessage.text = message;

            _buttonAgree.onClick.RemoveAllListeners();
            _buttonAgree.onClick.AddListener(() =>
            {
                SFXManager.Main.PlayFromSFXObjectLibrary("button-click");
                onAgree.Invoke();
                _popupConfirmation.SetActive(false);
            });

            _buttonDisagree.onClick.RemoveAllListeners();
            _buttonDisagree.onClick.AddListener(() =>
            {
                SFXManager.Main.PlayFromSFXObjectLibrary("button-click");
                onDisagree.Invoke();
                _popupConfirmation.SetActive(false);
            });
        }

        public void ShowResultPanel(UserData userData, TestResultData tesA, TestResultData tesB, Action onClickNext)
        {
            _userData = userData;
            _2mwtData = tesA;
            _6mwtData = tesB;
            _contentSection.SetActive(true);

            ShowPanel(9);
            _toggleMenu.SetActiveIndex(0);

            panels[9].buttonNext.onClick.AddListener(() =>
            {
                onClickNext?.Invoke();
                SFXManager.Main.PlayFromSFXObjectLibrary("button-click");
            });
        }

        public void Set2MWTData()
        {
            _textUsername.text = _userData.username;
            _textAge.text = _userData.age.ToString();
            _textGender.text = _userData.gender == UserData.Gender.Male ? $"Laki-laki" : $"Perempuan";
            _textTotalJarak.text = $"{_2mwtData.totalDistance:F2} m";
            _textLaps.text = $"{_2mwtData.totalLaps.ToString()} kali";
            _textPace.text = $"{_2mwtData.walkingSpeed:F1} m/menit";
            _textTitleSummary.text = $"2 Minutes Walking Test";
            _textDescriptionSummary.text = $"Hasil tes ini menunjukkan seberapa jauh Anda dapat berjalan dalam 2 menit, sebagai gambaran kondisi kemampuan dan daya tahan fisik Anda saat ini.";

            SFXManager.Main.PlayFromSFXObjectLibrary("button-click");
        }

        public void Set6MWTData()
        {
            _textUsername.text = _userData.username;
            _textAge.text = _userData.age.ToString();
            _textGender.text = _userData.gender == UserData.Gender.Male ? $"Laki-laki" : $"Perempuan";
            _textTotalJarak.text = $"{_6mwtData.totalDistance:F2} m";
            _textLaps.text = $"{_6mwtData.totalLaps.ToString()} kali";
            _textPace.text = $"{_6mwtData.walkingSpeed:F1} m/menit";
            _textTitleSummary.text = $"6 Minutes Walking Test";
            _textDescriptionSummary.text = $"Hasil tes ini menunjukkan seberapa jauh Anda dapat berjalan dalam 6 menit, sebagai gambaran kondisi kemampuan dan daya tahan fisik Anda saat ini.";

            SFXManager.Main.PlayFromSFXObjectLibrary("button-click");
        }

        public void SetActiveCountDown(bool isActive, string val, string instruction)
        {
            _canvasFollowingPlayer.ShowCanvas();

            foreach (var p in panels)
            {
                if (p.gameObject != null)
                    p.gameObject.SetActive(false);
            }

            if (!isActive)
            {
                // Kalau dipanggil dengan false -> langsung matikan popup & reset timer
                _popupCountdown.SetActive(false);
                m_timeToHide = 0f;

                if (m_hideRoutine != null)
                {
                    StopCoroutine(m_hideRoutine);
                    m_hideRoutine = null;
                }

                return;
            }

            _popupCountdown.SetActive(true);

            // Set teks
            if (_textCountdown != null)
                _textCountdown.text = val;

            if (_textInstruction != null)
                _textInstruction.text = instruction;

            // Tambah waktu hide 2 detik setiap dipanggil
            m_timeToHide += 2f;

            // Kalau belum ada coroutine jalan, mulai
            if (m_hideRoutine == null)
            {
                m_hideRoutine = StartCoroutine(HideAfterCooldown(() =>
                {
                    // Waktu habis -> hide popup
                    _popupCountdown.SetActive(false);
                    m_hideRoutine = null;
                }));
            }
        }

        private IEnumerator HideAfterCooldown(Action onTimeisUp)
        {
            while (m_timeToHide > 0f)
            {
                m_timeToHide -= Time.deltaTime;
                yield return null;
            }

            onTimeisUp?.Invoke();
        }

        public void SetActiveHint(Sprite icon, string msg, float timeToHide)
        {
            _canvasFollowingPlayer.ShowCanvas();
            
            if (_popupCountdown.activeSelf)
                _popupCountdown.SetActive(false);

            if (m_hideRoutine != null)
            {
                StopCoroutine(m_hideRoutine);
                m_hideRoutine = null;
            }

            _popupHint.SetActive(true);

            _imageHint.sprite = icon;
            _textHint.text = msg;

            m_timeToHide += timeToHide;

            if (m_hideRoutine == null)
            {
                m_hideRoutine = StartCoroutine(HideAfterCooldown(() =>
                {
                    // Waktu habis -> hide popup
                    _popupHint.SetActive(false);
                    m_hideRoutine = null;
                }));
            }
        }
    }
}