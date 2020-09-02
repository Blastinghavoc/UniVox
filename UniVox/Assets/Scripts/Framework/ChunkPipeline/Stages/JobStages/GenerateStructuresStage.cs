using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Framework.ChunkPipeline
{
    public class GenerateStructuresStage : WaitForJobStage<ChunkNeighbourhood>
    {
        public GenerateStructuresStage(string name, int order, IChunkPipeline pipeline, int maxInStage) : base(name, order, pipeline, maxInStage)
        {
        }

        protected override AbstractPipelineJob<ChunkNeighbourhood> MakeJob(Vector3Int chunkId)
        {
            return pipeline.chunkProvider.GenerateStructuresForNeighbourhood(chunkId,
                        new ChunkNeighbourhood(chunkId, (neighId) => pipeline.getChunkComponent(neighId).Data));
        }

        protected override void OnJobDone(Vector3Int chunkId, ChunkNeighbourhood result)
        {
            //Result is already applied, no extra work to be done.
        }

        /// <summary>
        /// Generating structures is only free for an id if none (! any) of that id's neighbours
        /// are in the stage. This prevents two jobs trying to access the same neighbourhood data
        /// at the same time.
        /// </summary>
        /// <param name="chunkId"></param>
        /// <returns></returns>
        public override bool FreeFor(Vector3Int chunkId, HashSet<Vector3Int> pendingEntry)
        {
            bool baseCondition = base.FreeFor(chunkId, pendingEntry);

            bool doesNotAndWillNotContainNeighbour = !Utils.Helpers.GetNeighboursDirectOnly(chunkId).Any((neigh) => Contains(neigh) || pendingEntry.Contains(neigh));

            return baseCondition && doesNotAndWillNotContainNeighbour;
        }
    }
}