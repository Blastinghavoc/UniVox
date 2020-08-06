using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UniVox.Framework;
using UniVox.Framework.Serialisation;

namespace UniVox.UI
{
    public class PauseMenuController : MonoBehaviour
    {
        public void Toggle()
        {
            if (gameObject.activeSelf)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            gameObject.SetActive(true);
            Time.timeScale = 0;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Close()
        {
            gameObject.SetActive(false);
            Time.timeScale = 1;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void OnResumeButtonClicked()
        {
            Close();
        }

        public void OnSaveAndExitButtonClicked()
        {
            if (SaveUtils.DoSave)
            {
                var chunkManager = FindObjectOfType<ChunkManager>();
                chunkManager.StoreAllModifiedChunks();
            }
            SceneManager.LoadScene("MainMenu");
        }
    }
}