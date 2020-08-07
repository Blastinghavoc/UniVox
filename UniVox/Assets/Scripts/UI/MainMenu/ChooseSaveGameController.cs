using Boo.Lang;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UniVox.Framework.Serialisation;
using UniVox.MessagePassing;

namespace UniVox.UI
{
    public class ChooseSaveGameController : MonoBehaviour
    {
        public MainMenuController mainMenu;
        public SavedGameListController saveGameList;
        public ConfirmationDialog dialog;

        private Button[] buttons;

        // Start is called before the first frame update
        void Start()
        {
            saveGameList.Populate();
            buttons = GetComponentsInChildren<Button>();
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

        public void OnDeleteSelectedClicked() 
        {
            if (saveGameList.TryGetSelected(out var worldName))
            {
                dialog.Message.text = $"Are you sure you want to delete {worldName}?";
                dialog.ChoiceMadeCallback = (bool choice) =>
                {
                    if (choice)
                    {
                        DeleteSave(worldName);
                    }
                    saveGameList.SetInteractable(true);
                    SetButtonsInteractable(true);
                    dialog.Close();
                };
                SetButtonsInteractable(false);
                saveGameList.SetInteractable(false);
                dialog.Open();
            }
        }

        private void DeleteSave(string worldName) 
        {
            try
            {
                Directory.Delete(SaveUtils.SaveDirectoryForWorldName(worldName), true);
                saveGameList.DeleteSelected();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error trying to delete world {worldName}. Message {e.Message}");
            }
        }

        private void SetButtonsInteractable(bool value) 
        {
            foreach (var button in buttons)
            {
                button.interactable = value;
            }
        }
    }
}