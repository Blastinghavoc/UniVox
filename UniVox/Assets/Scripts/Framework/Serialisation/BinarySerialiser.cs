﻿using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace UniVox.Framework.Serialisation
{
    /// <summary>
    /// Based on REF: https://www.youtube.com/watch?v=5roZtuqZyuw
    /// </summary>
    public class BinarySerialiser
    {
        private string subDirectory;
        private string saveFileExtension;
        private bool compressed;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="subDirectory">Sub directory path, with trailing "/" </param>
        /// <param name="saveFileExtension">File extension, with preceding "." </param>
        /// <param name="compressed"></param>
        public BinarySerialiser(string subDirectory, string saveFileExtension, bool compressed = true)
        {
            this.subDirectory = subDirectory;
            this.saveFileExtension = saveFileExtension;
            this.compressed = compressed;
        }

        public void Save(object obj, string fileName)
        {
            string directoryPath = SaveManager.Instance.BaseSaveDirectory + subDirectory;

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string fullPath = directoryPath+ fileName + saveFileExtension;

            using (var file = File.Create(fullPath))
            {
                if (compressed)
                {
                    using (var compressor = new DeflateStream(file, CompressionMode.Compress))
                    {
                        BinaryFormatter formatter = new BinaryFormatter();

                        formatter.Serialize(compressor, obj);
                    }
                }
                else
                {
                    BinaryFormatter formatter = new BinaryFormatter();

                    formatter.Serialize(file, obj);
                }
            }


        }

        public bool TryLoad(string fileName, out object data)
        {
            string fullPath = SaveManager.Instance.BaseSaveDirectory + subDirectory + fileName + saveFileExtension;

            data = null;

            if (!File.Exists(fullPath))
            {
                return false;
            }

            using (var file = File.OpenRead(fullPath))
            {
                BinaryFormatter formatter = new BinaryFormatter();

                try
                {
                    if (compressed)
                    {
                        using (var compressor = new DeflateStream(file, CompressionMode.Decompress))
                        {
                            data = formatter.Deserialize(compressor);
                            return true;
                        }
                    }
                    else
                    {
                        data = formatter.Deserialize(file);
                        return true;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to load file: {fullPath}. Cause {e.Message}");
                    return false;
                }

            }

        }
    }
}