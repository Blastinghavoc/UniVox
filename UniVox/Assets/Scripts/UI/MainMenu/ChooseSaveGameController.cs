using UnityEngine;
using UnityEngine.SceneManagement;
using UniVox.Framework.Serialisation;
using UniVox.MessagePassing;

namespace UniVox.UI
{
    public class ChooseSaveGameController : MonoBehaviour
    {
        public MainMenuController mainMenu;
        public SavedGameListController saveGameList;

        // Start is called before the first frame update
        void Start()
        {
            saveGameList.Populate();
        }

        public void OnBackButtonClicked()
        {
            mainMenu.gameObject.SetActive(true);
            gameObject.SetActive(false);
        }

        public void OnLoadSelectedClicked()
        {
            if (saveGameList.TryGetSelected(out var worldName))
            {
                SaveUtils.WorldName = worldName;

                BinarySerialiser serialiser = new BinarySerialiser(SaveUtils.CurrentWorldSaveDirectory, ".seed");
                int seed = 0;
                if (serialiser.TryLoad("worldSeed", out var seedObj))
                {
                    seed = (int)seedObj;
                    SceneMessagePasser.SetMessage(new SeedMessage() { seed = seed });
                }

                SceneManager.LoadScene(mainMenu.gameScene);
            }
        }
    }
}