﻿using UnityEngine;
using System.Collections;
using UniVox.Framework;

namespace PerformanceTesting
{

    /// <summary>
    /// In test mode, disables all chunk managers that are children of the object,
    /// otherwise initialises the first active chunk manager.
    /// </summary>
    public class ManagerInitialiser : MonoBehaviour
    {
        public bool TestMode;
        void Awake()
        {

            if (TestMode)
            {

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