using System.Linq;
using UnityEngine;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using UniVox.Framework.Lighting;

namespace UniVox.Framework.ChunkPipeline
{
    public class GenerateLightsStage : WaitForJobStage<LightmapGenerationJobResult>
    {
        private ILightManager lightManager;
        public GenerateLightsStage(string name, int order, IChunkPipeline pipeline, int maxInStage, ILightManager lightManager) : base(name, order, pipeline, maxInStage)
        {
            this.lightManager = lightManager;
        }

        protected override AbstractPipelineJob<LightmapGenerationJobResult> MakeJob(Vector3Int chunkId)
        {
            return lightManager.CreateGenerationJob(chunkId);
        }

        protected override void OnJobDone(Vector3Int chunkId, LightmapGenerationJobResult result)
        {
            lightManager.ApplyGenerationResult(chunkId, result);
        }

        /// <summary>
        /// Generating lights for a chunk is only allowed if none of that chunks direct
        /// neighbours are also generating lights (due to neighbour dependencies).
        /// </summary>
        /// <param name="chunkId"></param>
        /// <returns></returns>
        public override bool FreeFor(Vector3Int chunkId)
        {
            return !Utils.Helpers.GetNeighboursDirectOnly(chunkId).Any((neigh) => Contains(neigh));
        }
    }
}