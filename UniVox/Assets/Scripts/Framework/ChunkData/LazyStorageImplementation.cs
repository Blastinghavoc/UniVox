using UnityEngine;

namespace UniVox.Framework
{

    /// <summary>
    /// This storage implementation efficiently represents empty storage, and
    /// lazily initialises real storage only if an attempt is made to write to the data.
    /// </summary>
    public class LazyStorageImplementation<OwnerType, StoredDataType> : IChunkStorageImplementation<StoredDataType>
        where OwnerType : IChunkStorageOwner<StoredDataType>
    {
        OwnerType owner;
        public LazyStorageImplementation(OwnerType owner)
        {
            this.owner = owner;
        }

        public StoredDataType Get(int x, int y, int z)
        {
            return default;
        }

        public void InitialiseEmpty(Vector3Int dimensions)
        {
            throw new System.NotImplementedException();
        }

        public void InitialiseWithData(Vector3Int dimensions, StoredDataType[] initialData)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Setting a value causes the real storage to be initialised.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <param name="value"></param>
        public void Set(int x, int y, int z, StoredDataType value)
        {
            owner.InitialiseEmptyStorage();
            owner.Set(x, y, z, value);
        }


        public StoredDataType[] ToArray()
        {
            return new StoredDataType[owner.Dimensions.x * owner.Dimensions.y * owner.Dimensions.z];
        }
    }

    public interface IChunkStorageOwner<StoredDataType>
    {
        void InitialiseEmptyStorage();
        void Set(int x, int y, int z, StoredDataType item);

        Vector3Int Dimensions { get; }
    }

}