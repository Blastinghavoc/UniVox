using UnityEngine;

namespace UniVox.Framework
{
    public interface IChunkComponent<VoxelDataType>      
        where VoxelDataType : struct,IVoxelData
    {
        Vector3Int ChunkID { get; }
        IChunkData<VoxelDataType> Data { get; set; }

        Mesh GetRenderMesh();

        void Initialise(Vector3Int id, Vector3 position);
        void RemoveCollisionMesh();
        void RemoveRenderMesh();
        void SetCollisionMesh(Mesh mesh);
        void SetRenderMesh(Mesh mesh);
    }
}