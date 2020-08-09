using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UniVox.Framework;
using UniVox.Framework.Serialisation;

namespace UniVox.UI
{
    public class PauseMenuController : AbstractUIController
    {
        public override void SetVisibility(bool visible)
        {
            IsVisible = visible;
            if (visible)
            {
                Open();
            }
            else
            {
                Close();
            }
        }

        public void Open()
        {
            gameObject.SetActive(true);
            Time.timeScale = 0;            
        }

        public void Close()
        {
            gameObject.SetActive(false);
            Time.timeScale = 1;            
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