using UnityEngine;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Framework.ChunkPipeline
{
    public class ApplyCollisionMeshStage : WaitForJobStage<Mesh>
    {
        public ApplyCollisionMeshStage(string name, int order, IChunkPipeline pipeline, int maxInStage) : base(name, order, pipeline, maxInStage)
        {
        }

        protected override AbstractPipelineJob<Mesh> MakeJob(Vector3Int chunkId)
        {
            return pipeline.chunkMesher.ApplyCollisionMeshJob(chunkId);
        }

        protected override void OnJobDone(Vector3Int chunkId, Mesh result)
        {
            pipeline.getChunkComponent(chunkId).SetCollisionMesh(result);
        }
    }
}