using UnityEngine;
using UniVox.Framework;
using Utils;

namespace UniVox.Implementations.ChunkData
{
    public class MultiDimensionalArrayVoxelStorage : IVoxelStorageImplementation
    {
        private VoxelTypeID[,,] voxels;
        public VoxelTypeID Get(int x, int y, int z)
        {
            return voxels[x, y, z];
        }

        public void InitialiseEmpty(Vector3Int dimensions)
        {
            voxels = new VoxelTypeID[dimensions.x, dimensions.y, dimensions.z];
        }

        public void InitialiseWithData(Vector3Int dimensions, VoxelTypeID[] initialData)
        {
            voxels = initialData.Expand(dimensions);
        }

        public void Set(int x, int y, int z, VoxelTypeID typeID)
        {
            voxels[x, y, z] = typeID;
        }

        public VoxelTypeID[] ToArray()
        {
            return voxels.Flatten();
        }
    }
}