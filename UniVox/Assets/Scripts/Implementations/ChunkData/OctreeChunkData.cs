using Unity.Collections;
using UnityEngine;
using UniVox.Framework;
using Utils;

namespace UniVox.Implementations.ChunkData
{
    public class OctreeChunkData : AbstractChunkData
    {
        private OctreeVoxelStorage octree;

        public OctreeChunkData(Vector3Int ID, Vector3Int chunkDimensions, VoxelTypeID[] initialData = null) : base(ID, chunkDimensions, initialData)
        {
            if (initialData == null)
            {
                octree = new OctreeVoxelStorage(chunkDimensions);
            }
            else
            {
                octree = new OctreeVoxelStorage(chunkDimensions,initialData);                
            }
        }

        protected override VoxelTypeID GetVoxelID(int x, int y, int z)
        {
            return octree.Get(x, y, z);
        }

        protected override void SetVoxelID(int x, int y, int z, VoxelTypeID voxel)
        {
            octree.Set(x, y, z, voxel);
        }

        protected override VoxelTypeID[] GetVoxelArray()
        {
            return octree.ToArray();
        }

        public override NativeArray<VoxelTypeID> ToNative(Allocator allocator = Allocator.Persistent)
        {
            return octree.ToArray().ToNative(allocator);
        }
    }
}