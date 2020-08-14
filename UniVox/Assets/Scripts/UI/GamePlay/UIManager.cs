using System.Collections.Generic;
using UnityEngine;

namespace UniVox.UI
{
    public class UIManager : MonoBehaviour
    {

        [SerializeField] private KeyMenuPair[] menuKeybindings = null;

        Dictionary<KeyCode, AbstractUIController> keybindings;
        private HashSet<AbstractUIController> visibleUisUsingCursor;

        public bool CursorInUseByUI { get => visibleUisUsingCursor.Count > 0; }

        private void Start()
        {
            keybindings = new Dictionary<KeyCode, AbstractUIController>();
            foreach (var pair in menuKeybindings)
            {
                keybindings.Add(pair.key, pair.controller);
                pair.controller.SetVisibility(false);
            }
            visibleUisUsingCursor = new HashSet<AbstractUIController>();
        }


        private void Update()
        {
            foreach (var keycode in keybindings.Keys)
            {
                if (Input.GetKeyDown(keycode))
                {
                    var ui = keybindings[keycode];
                    ui.ToggleVisibility();
                    if (ui.IsVisible && ui.EnableCursorWhileVisible)
                    {
                        visibleUisUsingCursor.Add(ui);
                    }
                    else
                    {
                        visibleUisUsingCursor.Remove(ui);
                    }
                }
            }

            visibleUisUsingCursor.RemoveWhere(_ => !_.IsVisible);

            if (visibleUisUsingCursor.Count > 0)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDestroy()
        {
            //Make sure the cursor becomes usable again
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        [System.Serializable]
        private class KeyMenuPair
        {
            public AbstractUIController controller = null;
            public KeyCode key = KeyCode.None;
        }

    }
}