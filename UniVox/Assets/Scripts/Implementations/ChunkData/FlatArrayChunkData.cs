using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UniVox.Framework;
using UniVox.Framework.Jobified;
using Utils;

namespace UniVox.Implementations.ChunkData
{
    /// <summary>
    /// Similar to arrayChunkData, but implented with a flat array.
    /// This allows it to be easily constructed from the result of a job.
    /// </summary>
    public class FlatArrayChunkData : AbstractChunkData 
    {
        protected FlatArrayVoxelStorage storage;
            
        public FlatArrayChunkData(Vector3Int ID, Vector3Int chunkDimensions,VoxelTypeID[] initialData = null) : base(ID, chunkDimensions,initialData) 
        {
            storage = new FlatArrayVoxelStorage();
            if (initialData == null)
            {
                storage.InitialiseEmpty(chunkDimensions);
            }
            else
            {
                storage.InitialiseWithData(chunkDimensions,initialData);
            }
        }

        protected override VoxelTypeID GetVoxelID(int x, int y, int z)
        {
            return storage.Get(x,y,z);
        }

        protected override void SetVoxelID(int x, int y, int z, VoxelTypeID voxel)
        {
            storage.Set(x, y, z, voxel);
        }

        public override NativeArray<VoxelTypeID> ToNative(Allocator allocator = Allocator.Persistent)
        {
            return storage.ToArray().ToNative(allocator);
        }
    }

    public class FlatArrayVoxelStorage : IVoxelStorageImplementation
    {
        private VoxelTypeID[] voxels;
        private int dx;
        private int dxdy;

        public VoxelTypeID Get(int x, int y, int z)
        {
            return voxels[Utils.Helpers.MultiIndexToFlat(x, y, z, dx, dxdy)];
        }

        public void InitialiseEmpty(Vector3Int dimensions)
        {
            dx = dimensions.x;
            dxdy = dimensions.x * dimensions.y;
            voxels = new VoxelTypeID[dxdy * dimensions.z];
        }

        public void InitialiseWithData(Vector3Int dimensions, VoxelTypeID[] initialData)
        {
            dx = dimensions.x;
            dxdy = dimensions.x * dimensions.y;
            voxels = initialData;
        }

        public void Set(int x, int y, int z, VoxelTypeID typeID)
        {
            voxels[Utils.Helpers.MultiIndexToFlat(x, y, z, dx, dxdy)] = typeID;
        }

        public VoxelTypeID[] ToArray()
        {
            return voxels;
        }
    }
}