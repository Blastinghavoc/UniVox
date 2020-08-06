using UnityEngine;

namespace UniVox.UI
{
    public class UIInputListener : MonoBehaviour
    {
        public PauseMenuController pauseMenu;
        public KeyCode pauseKey = KeyCode.Escape;

        public DebugPanel debugPanel;
        public KeyCode debugPanelKey = KeyCode.F1;

        private void Start()
        {
            pauseMenu.Close();
        }

        private void Update()
        {
            if (Input.GetKeyDown(pauseKey))
            {
                pauseMenu.Toggle();
            }

            if (Input.GetKeyDown(debugPanelKey))
            {
                debugPanel.Toggle();
            }
        }

    }
}