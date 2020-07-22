using UnityEngine;
using System.Collections;
using System;
using Unity.Collections;
using System.Collections.Generic;
using UniVox.Framework.Common;
using UniVox.Framework.Lighting;

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
        NativeArray<VoxelTypeID> BorderToNative(Direction dir, Allocator allocator = Allocator.Persistent);
        NativeArray<LightValue> BorderToNativeLight(Direction dir, Allocator allocator = Allocator.Persistent);


        LightValue GetLight(int x, int y, int z);
        LightValue GetLight(Vector3Int pos);
        void SetLight(int x, int y, int z, LightValue lightValue);
        void SetLight(Vector3Int pos,LightValue lightValue);
        NativeArray<LightValue> LightToNative(Allocator allocator = Allocator.Persistent);
    }
}