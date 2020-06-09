using UnityEngine;

namespace UniVox.Framework
{
    public interface IChunkComponent<ChunkDataType, VoxelDataType>
        where ChunkDataType : IChunkData<VoxelDataType>
        where VoxelDataType : IVoxelData
    {
        Vector3Int ChunkID { get; }
        ChunkDataType Data { get; set; }

        Mesh GetRenderMesh();

        void Initialise(Vector3Int id, Vector3 position);
        void RemoveCollisionMesh();
        void RemoveRenderMesh();
        void SetCollisionMesh(Mesh mesh);
        void SetRenderMesh(Mesh mesh);
    }
}