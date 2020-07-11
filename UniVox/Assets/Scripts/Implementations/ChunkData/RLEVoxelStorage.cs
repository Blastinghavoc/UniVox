using UnityEngine;
using UniVox.Framework;

namespace UniVox.Implementations.ChunkData
{
    public class RLEVoxelStorage : RLEArray<VoxelTypeID>, IVoxelStorageImplementation
    {
        private int dx;
        private int dxdy;
        public override void InitialiseEmpty(Vector3Int dimensions)
        {
            InitialiseSelf(dimensions);
            base.InitialiseEmpty(dimensions);
        }

        public override void InitialiseWithData(Vector3Int dimensions, VoxelTypeID[] initialData)
        {
            InitialiseSelf(dimensions);
            base.InitialiseWithData(dimensions, initialData);
        }

        private void InitialiseSelf(Vector3Int dimensions) 
        {
            dx = dimensions.x;
            dxdy = dimensions.x * dimensions.y;
        }

        public VoxelTypeID Get(int x, int y, int z)
        {
            return Get(Utils.Helpers.MultiIndexToFlat(x, y, z, dx, dxdy));
        }

        public void Set(int x, int y, int z, VoxelTypeID typeID)
        {
            Set(Utils.Helpers.MultiIndexToFlat(x, y, z, dx, dxdy), typeID);
        }
    }
}