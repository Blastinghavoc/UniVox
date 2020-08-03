using UnityEngine;

namespace UniVox.Framework.Serialisation
{
    public class SaveManager 
    {
        public static SaveManager Instance;

        public SaveManager(string baseSaveDirectory)
        {
            BaseSaveDirectory = baseSaveDirectory;
        }

        public string BaseSaveDirectory { get; private set; }

        public static void Initialise(string worldName) 
        {
            SaveManager sm = new SaveManager(Application.persistentDataPath + $"/{worldName}/");
          
            if (Instance != null)
            {
                Debug.LogWarning("Initialising a new save manager when one already existed!");
            }
            Instance = sm;
        }        
    }
}