using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


namespace WalkingTest
{
    public class CanvasManager : MonoBehaviour
    {
        [Header("Daftar Panel")]
        public List<GameObject> panels = new List<GameObject>();
        public List<GameObject> popups = new List<GameObject>();
        private int m_currentPanelIndex = 0;

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

        public void ShowPanel(int index)
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

            foreach (GameObject p in popups)
            {
                if (p != null)
                    p.SetActive(false);
            }

            // Aktifkan panel yang dipilih
            if (panels[index] != null)
                panels[index].SetActive(true);
        }

        public void OnClickShowPopup(int index)
        {
            ShowPopup(index, true);
        }

        public void ShowPopup(int index, bool hideAllPanel = false)
        {
            if (index < 0 || index >= popups.Count)
            {
                Debug.LogWarning("Index panel di luar jangkauan: " + index);
                return;
            }

            // Matikan semua panel dulu
            foreach (GameObject p in popups)
            {
                if (p != null)
                    p.SetActive(false);
            }

            if (hideAllPanel)
            {
                foreach (GameObject p in panels)
                {
                    if (p != null)
                        p.SetActive(false);
                }
            }

            // Aktifkan panel yang dipilih
            if (popups[index] != null)
                popups[index].SetActive(true);
        }
    }
}