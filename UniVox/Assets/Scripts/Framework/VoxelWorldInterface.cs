using UnityEngine;
using System.Collections;
using System;
using UnityEngine.UIElements;
using PerformanceTesting;
using UniVox.UI;

namespace UniVox.Framework
{

    /// <summary>
    /// Global interface to the voxel world to be accessed by gameplay scripts
    /// </summary>
    public class VoxelWorldInterface : MonoBehaviour,IDebugWorld
    {
        protected ITestableChunkManager chunkManager;
        protected VoxelTypeManager voxelTypeManager;

        public void Intialise(ITestableChunkManager manager, VoxelTypeManager voxelTypeManager)
        {
            chunkManager = manager;
            this.voxelTypeManager = voxelTypeManager;
        }

        public void PlaceVoxel(Vector3 position, SOVoxelTypeDefinition voxelType,VoxelRotation rotation = default)
        {
            if (voxelType.rotationConfiguration == null)
            {
                if (rotation.x != 0 || rotation.y != 0 || rotation.z != 0)
                {
                    return;//Cannot place non-rotable voxel with a rotation value
                }
                else
                {
                    chunkManager.TrySetVoxel(position, voxelTypeManager.GetId(voxelType));
                }
            }
            else
            {
                if (voxelType.rotationConfiguration.RotationValid(rotation))
                {
                    Debug.Log($"Placed voxel {voxelType.DisplayName} with rotation x:{rotation.x}, y:{rotation.y}, z{rotation.z}");

                    chunkManager.TrySetVoxel(position, voxelTypeManager.GetId(voxelType),rotation);
                }
                else
                {
                    Debug.Log($"Failed to place voxel {voxelType.DisplayName} because the rotation was not valid");
                }
            }
        }

        public void RemoveVoxel(Vector3 position)
        {
            chunkManager.TrySetVoxel(position, (VoxelTypeID)VoxelTypeManager.AIR_ID,default, true);
        }

        public Vector3 CenterOfVoxelAt(Vector3 position)
        {
            return chunkManager.SnapToVoxelCenter(position);
        }

        public bool TryGetVoxelType(Vector3 position,out SOVoxelTypeDefinition voxelType) 
        {
            voxelType = null;
            if (chunkManager.TryGetVoxel(position,out var voxelID))
            {
                voxelType = voxelTypeManager.GetDefinition(voxelID);
                return true;
            }
            return false;
        }

        public Vector3Int WorldToChunkPosition(Vector3 pos) 
        {
            return chunkManager.WorldToChunkPosition(pos);
        }

        public string GetPipelineStatus()
        {
            return chunkManager.GetPipelineStatus();
        }

        public void GetChunkManagerCoroutineStatus(out int waitingForUpdate,out int waitingForDeactivate) 
        {
            waitingForUpdate = chunkManager.WaitingForPlayAreaUpdate;
            waitingForDeactivate = chunkManager.WaitingForPlayAreaDeactivate;
        }
    }
}