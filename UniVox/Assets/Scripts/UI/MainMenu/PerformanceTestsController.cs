using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UniVox.Framework.Serialisation;
using UniVox.MessagePassing;

namespace UniVox.UI
{
    public class PerformanceTestsController : MonoBehaviour
    {
        public MainMenuController mainMenu;

        public Button runButton;
        public InputField filePathInput;

        private string filePath;
        private bool filePathEmpty;

        public void Clear()
        {
            filePath = default;
            filePathEmpty = true;
            filePathInput.text = string.Empty;

            UpdateRunButtonState();
        }

        public void OnFilePathChanged(string path)
        {
            filePathEmpty = string.IsNullOrEmpty(path);

            filePath = path;

            UpdateRunButtonState();
        }

        private void UpdateRunButtonState()
        {
            runButton.interactable = !filePathEmpty && IsFilePathValid();
        }

        private bool IsFilePathValid() 
        {
            bool valid = false;
            try
            {
                if (Directory.Exists(filePath))
                {
                    if (filePath.EndsWith(@"\")||filePath.EndsWith("/"))
                    {
                        valid = true;
                    }
                }
            }
            catch (System.Exception)
            {
            }
            return valid;
        }

        public void OnRunClicked()
        {         
            SceneMessagePasser.SetMessage(new PerformanceTestFilepathMessage() { filepath = filePath});
            SceneManager.LoadScene(mainMenu.performanceTestScene);
        }

        public void OnBackButtonClicked()
        {
            mainMenu.gameObject.SetActive(true);
            gameObject.SetActive(false);
        }
    }
}