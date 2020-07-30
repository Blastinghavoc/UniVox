using System;
using UnityEngine;
using static UniVox.Framework.ChunkPipeline.ChunkPipelineManager;
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

        public PassThroughStage(string name, int stageId,IChunkPipeline pipeline):base(name,stageId,pipeline)
        {
        }

        /// <summary>
        /// In a passthrough stage, the incoming chunk either moves on
        /// to the next stage, or immediately terminates here.
        /// </summary>
        /// <param name="incoming"></param>
        public override void Add(Vector3Int incoming, ChunkStageData stageData)
        {
            if (!TerminateHereCondition(stageData))
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

    public class PassThroughApplyFunctionStage : PassThroughStage
    {
        Action<Vector3Int> func;
        public PassThroughApplyFunctionStage(string name, int stageId, IChunkPipeline pipeline,Action<Vector3Int> func) : base(name, stageId, pipeline)
        {
            this.func = func;
        }

        public override void Add(Vector3Int incoming, ChunkStageData stageData)
        {
            base.Add(incoming, stageData);
            func(incoming);//Apply the function to anything entering this stage, even if it terminates here.
        }
    }
}