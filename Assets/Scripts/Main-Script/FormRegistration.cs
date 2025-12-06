using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace WalkingTest
{
    public class FormRegistration : MonoBehaviour
    {
        [Header("Input Field")]
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_InputField ageInput;
        [SerializeField] private UserData.Gender _gender = UserData.Gender.None;

        [Header("Component References")]
        [SerializeField] private ApplicationManager _applicationManager;
        [SerializeField] private UIToggleButtonsGroup _uIToggleButtonsGroup;
        [SerializeField] private CanvasManager _canvasManager;

        public void OnClick_Save()
        {
            if (ValidateAndFillData())
            {
                _canvasManager.ShowPopupConfirmation($"Simpan Data Diri", $"Apakah data diri Kamu sudah benar?", () => StoreUserDataToAppManager(), () => _canvasManager.ShowPanel(1));
            }
        }

        private bool ValidateAndFillData()
        {
            string name = nameInput != null ? nameInput.text.Trim() : "";
            string ageStr = ageInput != null ? ageInput.text.Trim() : "";

            UserData.Gender selectedGender = UserData.Gender.None;
            if (_uIToggleButtonsGroup.GetActiveIndex() == 0)
                selectedGender = UserData.Gender.Male;
            else if (_uIToggleButtonsGroup.GetActiveIndex() == 1)
                selectedGender = UserData.Gender.Female;

            // Kumpulkan pesan error
            string errorMsg = "";

            // Validasi nama
            if (string.IsNullOrWhiteSpace(name))
            {
                errorMsg += "Nama tidak boleh kosong.\n";
            }

            // Validasi usia
            int ageValue;
            if (!int.TryParse(ageStr, out ageValue) || ageValue <= 5)
            {
                errorMsg += "Usia harus berupa angka dan lebih besar dari 5 tahun.\n ";
            }

            // Validasi gender
            if (selectedGender == UserData.Gender.None)
            {
                errorMsg += "Jenis kelamin harus dipilih.\n";
            }

            // Jika ada error, tampilkan lalu return false
            if (!string.IsNullOrEmpty(errorMsg))
            {
                Debug.LogWarning(errorMsg);

                return false;
            }

            _gender = selectedGender;

            return true;
        }

        public void StoreUserDataToAppManager()
        {
            // Di sini data sudah valid dan tersimpan di CurrentUserData
            int ageValue = 0;
            int.TryParse(ageInput.text, out ageValue);
            UserData CurrentUserData = new UserData();
            CurrentUserData.username = nameInput.text;
            CurrentUserData.age = ageValue;
            CurrentUserData.gender = _gender;

            Debug.Log($"Data valid. Nama: {CurrentUserData.username}, " + $"Usia: {CurrentUserData.age}, " + $"Gender: {CurrentUserData.gender.ToString()}");

            _applicationManager.StoreUserData(CurrentUserData);
        }
    }
}