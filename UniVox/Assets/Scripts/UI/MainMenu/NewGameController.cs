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

        public string worldName;
        public bool worldNameEmpty;

        public int seed;
        public bool seedEmpty;

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

        public void OnWorldNameChanged(string _)
        {
            var name = worldNameInput.text;
            worldNameEmpty = string.IsNullOrEmpty(name);

            worldName = name;

            UpdatePlayButtonState();
        }

        public void OnWorldSeedChanged(string _)
        {
            var seedString = seedInput.text;

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