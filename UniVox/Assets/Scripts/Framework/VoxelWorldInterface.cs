using UnityEngine;
using System.Collections;
using System;
using UnityEngine.UIElements;

namespace UniVox.Framework
{

    /// <summary>
    /// Global interface to the voxel world to be accessed by gameplay scripts
    /// </summary>
    public class VoxelWorldInterface : MonoBehaviour
    {
        protected IChunkManager chunkManager;
        protected VoxelTypeManager voxelTypeManager;

        public void Intialise(IChunkManager manager, VoxelTypeManager voxelTypeManager)
        {
            chunkManager = manager;
            this.voxelTypeManager = voxelTypeManager;
        }

        public void PlaceVoxel(Vector3 position, ushort voxelTypeID)
        {
            chunkManager.TrySetVoxel(position, voxelTypeID);
        }

        public void PlaceVoxel(Vector3 position, SOVoxelTypeDefinition voxelType)
        {
            chunkManager.TrySetVoxel(position, voxelTypeManager.GetId(voxelType));
        }

        public void RemoveVoxel(Vector3 position)
        {
            chunkManager.TrySetVoxel(position, VoxelTypeManager.AIR_ID, true);
        }

        public Vector3 CenterOfVoxelAt(Vector3 position)
        {
            return chunkManager.SnapToVoxelCenter(position);
        }
    }
}