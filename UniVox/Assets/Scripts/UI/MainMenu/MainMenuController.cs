using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniVox.UI
{
    public class MainMenuController : MonoBehaviour
    {
        public string gameScene;
        public string perfTestSceneName;

        public GameObject loadGamePanel;
        public NewGameController newGameController;

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        public void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit ();
#endif
        }

        public void OnNewGameClicked()
        {
            newGameController.gameObject.SetActive(true);
            newGameController.Clear();
            gameObject.SetActive(false);
        }

        public void OnPerfTestClicked()
        {
            SceneManager.LoadScene(perfTestSceneName);
        }

        public void OnLoadGameClicked()
        {
            loadGamePanel.SetActive(true);
            gameObject.SetActive(false);
        }
    }
}