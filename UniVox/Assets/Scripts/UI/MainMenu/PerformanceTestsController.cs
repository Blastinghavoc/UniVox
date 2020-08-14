using PerformanceTesting;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UniVox.MessagePassing;

namespace UniVox.UI
{
    public class PerformanceTestsController : MonoBehaviour
    {
        public MainMenuController mainMenu;

        public Button runButton;
        public InputField filePathInput;

        public ToggleListController suitesList;

        public AbstractTestSuite[] testSuites;

        private string filePath;
        private bool filePathEmpty;


        private int numRepeats;
        private bool numRepeatsEmpty;

        private void Start()
        {
            string[] labels = new string[testSuites.Length];
            for (int i = 0; i < testSuites.Length; i++)
            {
                labels[i] = testSuites[i].gameObject.name;
            }

            suitesList.Populate(labels);
            suitesList.OnChanged += UpdateRunButtonState;

            Clear();
        }

        public void Clear()
        {
            filePath = default;
            filePathEmpty = true;
            filePathInput.text = string.Empty;

            numRepeats = 0;
            numRepeatsEmpty = true;


            UpdateRunButtonState();
        }

        public void OnFilePathChanged(string path)
        {
            filePathEmpty = string.IsNullOrEmpty(path);

            filePath = path;

            UpdateRunButtonState();
        }

        public void OnNumRepeatsChanged(string value)
        {
            numRepeatsEmpty = string.IsNullOrEmpty(value);
            if (!numRepeatsEmpty)
            {
                numRepeats = int.Parse(value);
            }

            UpdateRunButtonState();
        }

        private void UpdateRunButtonState()
        {
            runButton.interactable = !filePathEmpty &&
                IsFilePathValid() &&
                suitesList.TryGetSelected(out var _) &&
                !numRepeatsEmpty;
        }

        private bool IsFilePathValid()
        {
            bool valid = false;
            try
            {
                if (Directory.Exists(filePath))
                {
                    if (filePath.EndsWith(@"\") || filePath.EndsWith("/"))
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
            SceneMessagePasser.SetMessage(new PerformanceTestParametersMessage()
            {
                filepath = filePath,
                selectedTestSuiteNames = suitesList.GetAllSelected(),
                numRepeats = numRepeats
            });
            SceneManager.LoadScene(mainMenu.performanceTestScene);
        }

        public void OnBackButtonClicked()
        {
            mainMenu.gameObject.SetActive(true);
            gameObject.SetActive(false);
        }
    }
}