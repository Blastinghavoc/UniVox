using UnityEngine;
using System.Collections;
using System;
using Unity.Collections;
using System.Collections.Generic;

namespace UniVox.Framework
{

    /// <summary>
    /// The data representation of a Chunk
    /// </summary>
    public interface IChunkData
    {
        Vector3Int ChunkID { get; set; }

        Vector3Int Dimensions { get; set; }

        bool ModifiedSinceGeneration { get; set; }

        bool FullyGenerated { get; set; }

        VoxelTypeID this[int i, int j, int k] { get; set; }
        VoxelTypeID this[Vector3Int index] { get; set; }

        //Coords are local to the chunk
        bool TryGetVoxelID(Vector3Int coords, out VoxelTypeID vox);
        bool TryGetVoxelID(int x, int y, int z, out VoxelTypeID vox);

        NativeArray<VoxelTypeID> ToNative(Allocator allocator = Allocator.Persistent);

        void SetRotation(Vector3Int coords, VoxelRotation rotation);

        NativeArray<RotatedVoxelEntry> NativeRotations(Allocator allocator = Allocator.Persistent);

        /// <summary>
        /// Create a flattened 2D native array of all voxels on the border
        /// in the given direction.
        /// </summary>
        /// <param name="Direction"></param>
        /// <returns></returns>
        NativeArray<VoxelTypeID> BorderToNative(int Direction);
    }
}