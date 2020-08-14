using UnityEngine;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Framework.ChunkPipeline
{
    public class GenerateTerrainStage : WaitForJobStage<IChunkData>
    {
        public GenerateTerrainStage(string name, int order, IChunkPipeline pipeline, int maxInStage) : base(name, order, pipeline, maxInStage)
        {
        }

        protected override AbstractPipelineJob<IChunkData> MakeJob(Vector3Int chunkId)
        {
            return pipeline.chunkProvider.GenerateTerrainData(chunkId);
        }

        protected override void OnJobDone(Vector3Int chunkId, IChunkData result)
        {
            pipeline.getChunkComponent(chunkId).Data = result;
        }
    }
}