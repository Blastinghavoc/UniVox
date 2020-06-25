using UnityEngine;
using System.Collections;
using Unity.Collections;

namespace UniVox.Framework
{
    public class ReadOnlyChunkData : IChunkData
    {
        private IChunkData realData;

        public ReadOnlyChunkData(IChunkData realData)
        {
            this.realData = realData;
        }

        public VoxelTypeID this[Vector3Int index] { get => realData[index]; set => throw new System.NotImplementedException(); }
        public VoxelTypeID this[int i, int j, int k] { get => realData[i,j,k]; set => throw new System.NotImplementedException(); }

        public Vector3Int ChunkID { get => realData.ChunkID; set => throw new System.NotImplementedException(); }
        public Vector3Int Dimensions { get => realData.Dimensions; set => throw new System.NotImplementedException(); }
        public bool ModifiedSinceGeneration { get => realData.ModifiedSinceGeneration; set => throw new System.NotImplementedException(); }
        public bool FullyGenerated { get => realData.FullyGenerated; set => throw new System.NotImplementedException(); }

        public NativeArray<VoxelTypeID> ToNative(Allocator allocator = Allocator.Persistent)
        {
            return realData.ToNative(allocator);
        }

        public bool TryGetVoxelID(Vector3Int coords, out VoxelTypeID vox)
        {
            return realData.TryGetVoxelID(coords, out vox);
        }

        public bool TryGetVoxelID(int x, int y, int z, out VoxelTypeID vox)
        {
            return realData.TryGetVoxelID(x,y,z, out vox);
        }
    }
}