using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UniVox.Framework.Serialisation;
using UniVox.MessagePassing;

namespace UniVox.UI
{
    public class NewGameController : MonoBehaviour
    {
        public MainMenuController mainMenu;

        public Button playButton;
        public InputField worldNameInput;
        public InputField seedInput;

        private string worldName;
        private bool worldNameEmpty;

        private int seed;
        private bool seedEmpty;

        public void Clear()
        {
            worldName = default;
            worldNameEmpty = true;
            worldNameInput.text = string.Empty;

            seed = default;
            seedEmpty = true;
            seedInput.text = string.Empty;

            UpdatePlayButtonState();
        }

        public void OnWorldNameChanged(string name)
        {
            worldNameEmpty = string.IsNullOrEmpty(name);

            worldName = name;

            UpdatePlayButtonState();
        }

        public void OnWorldSeedChanged(string seedString)
        {
            seedEmpty = string.IsNullOrEmpty(seedString);

            if (seedEmpty)
            {
                seed = default;
            }
            else
            {
                seed = int.Parse(seedString);
            }

            UpdatePlayButtonState();
        }

        private void UpdatePlayButtonState()
        {
            playButton.interactable = !worldNameEmpty && !seedEmpty;
        }

        public void OnPlayClicked()
        {
            SaveUtils.WorldName = worldName;

            BinarySerialiser serialiser = new BinarySerialiser(SaveUtils.CurrentWorldSaveDirectory, ".seed");
            serialiser.Save(seed, "worldSeed");

            SceneMessagePasser.SetMessage(new SeedMessage() { seed = seed });
            SceneManager.LoadScene(mainMenu.gameScene);
        }

        public void OnBackButtonClicked()
        {
            mainMenu.gameObject.SetActive(true);
            gameObject.SetActive(false);
        }
    }
}