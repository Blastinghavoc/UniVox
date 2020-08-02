using UnityEngine;
using UniVox.Framework.ChunkPipeline;

namespace UniVox.Framework
{
    public interface IChunkComponent
    {
        Vector3Int ChunkID { get; }
        IChunkData Data { get; set; }
        MeshDescriptor meshDescriptor { get; }

        Mesh GetRenderMesh();

        void Initialise(Vector3Int id, Vector3 position);
        void RemoveCollisionMesh();
        void RemoveRenderMesh();
        void SetCollisionMesh(Mesh mesh);
        void SetRenderMesh(MeshDescriptor meshDesc);

        //TODO remove DEBUG
        void SetPipelineStagesDebug(ChunkStageData chunkStageData);
    }
}