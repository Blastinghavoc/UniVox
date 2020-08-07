using UnityEngine;
using UniVox.Framework;

namespace UniVox.Implementations.ChunkData
{
    public class FlatArrayStorage<StoredDataType> : IChunkStorageImplementation<StoredDataType>
    {
        private StoredDataType[] array;
        private int dx;
        private int dxdy;

        public StoredDataType Get(int x, int y, int z)
        {
            return array[Utils.Helpers.MultiIndexToFlat(x, y, z, dx, dxdy)];
        }

        public void InitialiseEmpty(Vector3Int dimensions)
        {
            dx = dimensions.x;
            dxdy = dimensions.x * dimensions.y;
            array = new StoredDataType[dxdy * dimensions.z];
        }

        public void InitialiseWithData(Vector3Int dimensions, StoredDataType[] initialData)
        {
            dx = dimensions.x;
            dxdy = dimensions.x * dimensions.y;
            array = initialData;
        }

        public void Set(int x, int y, int z, StoredDataType typeID)
        {
            array[Utils.Helpers.MultiIndexToFlat(x, y, z, dx, dxdy)] = typeID;
        }

        public StoredDataType[] ToArray()
        {
            return array;
        }
    }
}