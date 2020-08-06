using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UniVox.Framework.Serialisation;

namespace UniVox.UI
{
    public class ChooseSaveGameController : MonoBehaviour
    {
        public MainMenuController mainMenuController;
        public SavedGameListController saveGameList;

        // Start is called before the first frame update
        void Start()
        {
            saveGameList.Populate();
        }

        public void OnBackButtonClicked()
        {
            mainMenuController.gameObject.SetActive(true);
            gameObject.SetActive(false);
        }

        public void OnLoadSelectedClicked()
        {
            if (saveGameList.TryGetSelected(out var worldName))
            {
                SaveUtils.WorldName = worldName;
                SceneManager.LoadScene(mainMenuController.gameScene);
            }
        }
    }
}