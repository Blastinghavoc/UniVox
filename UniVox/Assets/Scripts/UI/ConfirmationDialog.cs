using System;
using UnityEngine;
using UnityEngine.UI;

namespace UniVox.UI
{
    public class ConfirmationDialog : MonoBehaviour
    {
        public Action<bool> ChoiceMadeCallback;
        public Text Message;

        public void OnChoiceMade(bool choice)
        {
            ChoiceMadeCallback(choice);
            Close();
        }

        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}