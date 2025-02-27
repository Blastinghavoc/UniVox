﻿using UnityEngine;
using UniVox.Framework;
using UniVox.Framework.Serialisation;
using UniVox.Gameplay;

namespace PerformanceTesting
{

    /// <summary>
    /// In test mode, disables all chunk managers that are children of the object,
    /// otherwise initialises the first active chunk manager.
    /// </summary>
    public class ManagerInitialiser : MonoBehaviour
    {
        public bool TestMode;
        public bool DoSave;

        void Awake()
        {

            SaveUtils.DoSave = DoSave && !TestMode;//Test mode never saves in the normal way

            var voxelTypeManager = FindObjectOfType<VoxelTypeManager>();
            //Ensure voxel type manager is initialised
            voxelTypeManager.Initialise();

            if (TestMode)
            {
                //Disable player, as the chunk manager won't immediately be ready for them
                VoxelPlayer player = FindObjectOfType<VoxelPlayer>();
                player.enabled = false;
                player.gameObject.GetComponent<Rigidbody>().useGravity = false;

                foreach (Transform child in transform)
                {
                    var chunkManager = child.GetComponent<IChunkManager>();
                    if (chunkManager != null)
                    {
                        var obj = child.gameObject;
                        //deactivate all world implementations before they can start
                        obj.SetActive(false);
                    }
                }
            }
            else
            {
                foreach (Transform child in transform)
                {
                    if (child.gameObject.activeSelf)
                    {
                        var chunkManager = child.GetComponent<IChunkManager>();
                        if (chunkManager != null)
                        {
                            if (SaveUtils.DoSave && SaveUtils.WorldName == null)
                            {
                                //Set the world name to the chunk manager name by default
                                SaveUtils.WorldName = child.name;
                            }

                            //Initialise the first active chunk manager found
                            chunkManager.Initialise();
                            return;
                        }
                    }
                }
            }
        }
    }
}