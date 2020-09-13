using Unity.Collections;
using UnityEngine;
using UniVox.Framework.Common;
using UniVox.Framework.Lighting;
using UniVox.Framework.Serialisation;

namespace UniVox.Framework
{

    /// <summary>
    /// The data representation of a Chunk
    /// </summary>
    public interface IChunkData : ISaveable
    {
        #region Properties
        Vector3Int ChunkID { get; set; }
        Vector3Int Dimensions { get; set; }
        bool ModifiedSinceGeneration { get; set; }
        bool FullyGenerated { get; set; }
        #endregion

        //Indexers
        VoxelTypeID this[int i, int j, int k] { get; set; }
        VoxelTypeID this[Vector3Int index] { get; set; }

        //Note that coords are local to the chunk
        bool TryGetVoxel(Vector3Int coords, out VoxelTypeID vox);
        bool TryGetVoxel(int x, int y, int z, out VoxelTypeID vox);

        NativeArray<VoxelTypeID> ToNative(Allocator allocator = Allocator.Persistent);

        #region Rotation methods
        void SetRotation(Vector3Int coords, VoxelRotation rotation);
        void SetRotationsFromArray(RotatedVoxelEntry[] entries);
        NativeArray<RotatedVoxelEntry> NativeRotations(Allocator allocator = Allocator.Persistent);
        #endregion

        /// <summary>
        /// Create a flattened 2D native array of all voxels on the border
        /// in the given direction.
        /// </summary>
        /// <param name="Direction"></param>
        /// <returns></returns>
        NativeArray<VoxelTypeID> BorderToNative(Direction dir, Allocator allocator = Allocator.Persistent);

        #region Lighting methods
        NativeArray<LightValue> BorderToNativeLight(Direction dir, Allocator allocator = Allocator.Persistent);
        LightValue GetLight(int x, int y, int z);
        LightValue GetLight(Vector3Int pos);
        void SetLight(int x, int y, int z, LightValue lightValue);
        void SetLight(Vector3Int pos, LightValue lightValue);
        NativeArray<LightValue> LightToNative(Allocator allocator = Allocator.Persistent);
        void SetLightMap(LightValue[] lights);
        #endregion
    }
}