using UnityEngine;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Framework.ChunkPipeline
{
    public class GenerateMeshStage : WaitForJobStage<MeshDescriptor>
    {
        public GenerateMeshStage(string name, int order, IChunkPipeline pipeline, int maxInStage) : base(name, order, pipeline, maxInStage)
        {
        }

        protected override AbstractPipelineJob<MeshDescriptor> MakeJob(Vector3Int chunkId)
        {
            return pipeline.chunkMesher.CreateMeshJob(chunkId);
        }

        protected override void OnJobDone(Vector3Int chunkId, MeshDescriptor result)
        {
            pipeline.getChunkComponent(chunkId).SetRenderMesh(result);
        }
    }
}