using UnityEngine;
using System.Collections;
using System;

namespace UniVox.Framework
{

    /// <summary>
    /// The data representation of a Chunk
    /// </summary>
    public interface IChunkData<V>: IDisposable 
        where V : IVoxelData
    {
        Vector3Int ChunkID { get; set; }

        Vector3Int Dimensions { get; set; }

        bool ModifiedSinceGeneration { get; set; }

        bool FullyGenerated { get; set; }

        V this[int i, int j, int k] { get; set; }
        V this[Vector3Int index] { get; set; }

        //void SetVoxelAtLocalCoordinates(Vector3Int coords, V voxel);
        //void SetVoxelAtLocalCoordinates(int x, int y, int z, V voxel);

        //V GetVoxelAtLocalCoordinates(Vector3Int coords);
        //V GetVoxelAtLocalCoordinates(int x, int y, int z);

        bool TryGetVoxelAtLocalCoordinates(Vector3Int coords, out V vox);
        bool TryGetVoxelAtLocalCoordinates(int x, int y, int z, out V vox);

    }
}