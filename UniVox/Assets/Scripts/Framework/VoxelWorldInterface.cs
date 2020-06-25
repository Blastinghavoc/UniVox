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

        public void PlaceVoxel(Vector3 position, VoxelTypeID voxelTypeID)
        {
            chunkManager.TrySetVoxel(position, voxelTypeID);
        }

        public void PlaceVoxel(Vector3 position, SOVoxelTypeDefinition voxelType)
        {
            chunkManager.TrySetVoxel(position, voxelTypeManager.GetId(voxelType));
        }

        public void RemoveVoxel(Vector3 position)
        {
            chunkManager.TrySetVoxel(position, (VoxelTypeID)VoxelTypeManager.AIR_ID, true);
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
    }
}