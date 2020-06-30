using System;
using UnityEngine;
using static UniVox.Framework.ChunkPipeline.IPipelineStage;

namespace UniVox.Framework.ChunkPipeline
{
    /// <summary>
    /// Basic pipeline stage that immediately passes chunks through to the next stage,
    /// provided that their target stage is greater than this stage, and the next stage
    /// is free for them. If either condition is violated, the chunk terminates here.
    /// </summary>
    public class PassThroughStage : AbstractPipelineStage
    {       
        /// <summary>
        /// Passthrough stage never contains anything
        /// </summary>
        public override int Count { get => 0; }

        /// <summary>
        /// Passthrough stage does not limit the number of items that can be added to it.
        /// </summary>
        public override int EntryLimit { get => int.MaxValue; }        

        /// <summary>
        /// Condition under which a chunkId should terminate at this stage
        /// rather than passing through.
        /// For the passthrough stage, an id terminates if its target stage is not after this one,
        /// or the next stage cannot accept it. (Note that the passthrough stage has no facility for
        /// waiting until the next stage can accept it)
        /// </summary>
        /// <param name="chunkId"></param>
        /// <param name="currentStageId"></param>
        /// <returns></returns>
        protected override bool TerminateHereCondition(Vector3Int chunkId) 
        {
            return !pipeline.TargetStageGreaterThanCurrent(chunkId, StageID) ||
                !pipeline.NextStageFreeForChunk(chunkId,StageID);
        }

        public PassThroughStage(string name, int stageId,IChunkPipeline pipeline):base(name,stageId,pipeline)
        {
        }            

        /// <summary>
        /// In a passthrough stage, the incoming chunk either moves on
        /// to the next stage, or immediately terminates here.
        /// </summary>
        /// <param name="incoming"></param>
        public override void Add(Vector3Int incoming)
        {
            if (!TerminateHereCondition(incoming))
            {
                MovingOnThisUpdate.Add(incoming);
            }
            //Otherwise, discard
        }

        /// <summary>
        /// A Passthrough stage never contains anything,
        /// all things put in to it immediately move on or terminate, they
        /// never wait inside the stage.
        /// </summary>
        /// <param name="chunkID"></param>
        /// <returns></returns>
        public override bool Contains(Vector3Int chunkID)
        {
            return false;
        }

    }
}