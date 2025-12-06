using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace WalkingTest
{
    [Serializable]
    public class ToggleItem
    {
        [Header("UI")]
        public Button button;        // Button yang diklik user
        public Image targetImage;    // Image yang akan diganti spritenya (bisa image di button)

        [Header("Sprites")]
        public Sprite activeSprite;   // Sprite ketika dipilih
        public Sprite inactiveSprite; // Sprite ketika tidak dipilih

        [Header("Event Saat Dipilih")]
        public UnityEvent onSelected; // Event khusus item ini
    }

    // Event global, mengirim index yang aktif
    [Serializable]
    public class IntEvent : UnityEvent<int> { }

    public class UIToggleButtonsGroup : MonoBehaviour
    {
        [Tooltip("List toggle item. Untuk kasusmu isi 2 item saja.")]
        [SerializeField]
        private bool _isAutoSetValue = false;

        [Space(5f)]
        [Tooltip("List toggle item. Untuk kasusmu isi 2 item saja.")]
        public ToggleItem[] items;

        [Tooltip("Index default yang aktif saat Start (0 = item pertama).")]
        public int defaultIndex = 0;

        [Header("Event Global")]
        public IntEvent onToggleChanged;  // terpanggil tiap kali pilihan berubah

        int _currentIndex = -1;

        void Awake()
        {
            // Pasang listener click untuk semua button
            for (int i = 0; i < items.Length; i++)
            {
                int index = i; // penting, supaya tidak kena masalah closure
                if (items[i].button != null)
                {
                    items[i].button.onClick.AddListener(() => OnItemClicked(index));
                }
            }
        }

        void Start()
        {
            // Set default pilihan
            if (_isAutoSetValue)
                SetActiveIndex(defaultIndex);
        }

        void OnItemClicked(int index)
        {
            SetActiveIndex(index);
        }

        /// <summary>
        /// Dipanggil untuk mengganti toggle yang aktif (bisa dari code lain juga).
        /// </summary>
        public void SetActiveIndex(int index)
        {
            if (index < 0 || index >= items.Length)
                return;

            if (_currentIndex == index)
                return; // sudah aktif, tidak perlu apa-apa

            _currentIndex = index;

            // Update sprite & interaksi untuk semua item
            for (int i = 0; i < items.Length; i++)
            {
                bool isActive = (i == _currentIndex);
                ToggleItem item = items[i];

                if (item.targetImage != null)
                {
                    item.targetImage.sprite = isActive ? item.activeSprite : item.inactiveSprite;
                }

                if (item.button != null)
                {
                    // opsional: nonaktifkan click untuk yang sedang aktif
                    item.button.interactable = !isActive;
                }
            }

            // Invoke event untuk item yang dipilih
            ToggleItem activeItem = items[_currentIndex];
            activeItem.onSelected?.Invoke();

            // Invoke event global dengan index
            onToggleChanged?.Invoke(_currentIndex);
        }

        /// <summary>
        /// Ambil index yang sedang aktif.
        /// </summary>
        public int GetActiveIndex()
        {
            return _currentIndex;
        }
    }
}