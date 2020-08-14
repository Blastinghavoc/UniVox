using System.IO;
using UnityEngine;

namespace UniVox.Framework.Serialisation
{
    public static class SaveUtils
    {
        public static bool DoSave { get; set; } = false;
        public static string WorldName { get; set; } = null;


        public static string CurrentWorldSaveDirectory { get => SaveDirectoryForWorldName(WorldName); }

        public static string AllWorldsSaveDirectory { get => Application.persistentDataPath + $"/worlds/"; }
        public static string BasePath { get => Application.persistentDataPath; }

        public static string SaveDirectoryForWorldName(string worldName)
        {
            return AllWorldsSaveDirectory + $"{worldName}/";
        }

        public static string[] GetAllWorldNames()
        {
            if (!Directory.Exists(AllWorldsSaveDirectory))
            {
                return new string[0];
            }

            try
            {
                var results = Directory.GetDirectories(AllWorldsSaveDirectory);
                var prefixLength = AllWorldsSaveDirectory.Length;

                for (int i = 0; i < results.Length; i++)
                {
                    results[i] = results[i].Remove(0, prefixLength);
                }
                return results;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Exception getting world names: {e.Message} ");
                return new string[0];
            }
        }

        public static void DeleteSave(string worldName)
        {
            try
            {
                Directory.Delete(SaveDirectoryForWorldName(worldName), true);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error trying to delete world {worldName}. Message {e.Message}");
            }
        }
    }
}