using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Utils;

namespace UniVox.Framework.ChunkPipeline.WaitForNeighbours
{
    /// <summary>
    /// Stage in which chunks wait for their neighbours to be in this stage or greater before
    /// continuing. Supports both definitions of neighbours; included or excluding diagonals
    /// </summary>
    public class WaitForNeighboursStage : AbstractPipelineStage, IDisposable
    {

        private Dictionary<Vector3Int, INeighbourStatus> neighbourStatuses = new Dictionary<Vector3Int, INeighbourStatus>();

        public override int Count => neighbourStatuses.Count;
        //No entry limit
        public override int EntryLimit => int.MaxValue;

        /// <summary>
        /// Optional action to perform when the wait for an item has finished
        /// </summary>
        public Action<Vector3Int> OnWaitEnded = (_) => { return; };

        private bool includeDiagonals;

        private int prevStageId;

        private HashSet<Vector3Int> waitEndedSet;

        public WaitForNeighboursStage(string name, int stageId, IChunkPipeline pipeline, bool includeDiagonals) : base(name, stageId, pipeline)
        {
            this.includeDiagonals = includeDiagonals;

            prevStageId = stageId - 1;

            waitEndedSet = new HashSet<Vector3Int>();

            pipeline.OnChunkAddedToPipeline += WhenChunkAddedToPipeline;
            pipeline.OnChunkRemovedFromPipeline += WhenChunkRemovedFromPipeline;
            pipeline.OnChunkMinStageDecreased += WhenChunkMinStageDecreased;
        }

        public void Dispose()
        {
            pipeline.OnChunkAddedToPipeline -= WhenChunkAddedToPipeline;
            pipeline.OnChunkRemovedFromPipeline -= WhenChunkRemovedFromPipeline;
            pipeline.OnChunkMinStageDecreased -= WhenChunkMinStageDecreased;
        }

        /// <summary>
        /// When a chunk is added to the pipeline, check if it satisfies any of the waiting conditions
        /// </summary>
        /// <param name="chunkId"></param>
        /// <param name="addedAtStage"></param>
        private void WhenChunkAddedToPipeline(Vector3Int chunkId, int addedAtStage)
        {
            if (addedAtStage == StageID)
            {
                return;//Will already be dealt with by the Add method
            }

            if (StageID > prevStageId)
            {
                UpdateStatusOfNeighbours(chunkId, true);
            }
        }

        /// <summary>
        /// When a chunk is removed from the pipeline, discard it if it is tracked by this stage,
        /// and update the statuses of it neighbours.
        /// </summary>
        /// <param name="chunkId"></param>
        private void WhenChunkRemovedFromPipeline(Vector3Int chunkId)
        {
            neighbourStatuses.Remove(chunkId);
            waitEndedSet.Remove(chunkId);
            UpdateStatusOfNeighbours(chunkId, false);
        }

        /// <summary>
        /// When a chunk's minimum stage decreases, if it has decreased to a point
        /// before this stage then it is no longer valid for its neighbours.
        /// </summary>
        /// <param name="chunkId"></param>
        /// <param name="newMinStage"></param>
        private void WhenChunkMinStageDecreased(Vector3Int chunkId, int newMinStage) 
        {
            if (newMinStage < StageID)
            {                
                UpdateStatusOfNeighbours(chunkId, false);
            }
        }

        public override void Add(Vector3Int incoming,ChunkStageData stageData)
        {

            if (!TerminateHereCondition(stageData))
            {
                
                if (!neighbourStatuses.ContainsKey(incoming))
                {
                    //Add if not already present
                    var incomingStatus = ComputeStatus(incoming);
                    neighbourStatuses.Add(incoming, incomingStatus);

                    CheckIfWaitOver(incoming, incomingStatus); 
                }

                if (stageData.minStage > prevStageId)
                {
                    ///If the minimum stage of the incoming chunk is greater than the previous stage,
                    ///update the statuses of the neighbours.
                    ///Otherwise, there is another instance of the incoming chunk ID earlier in the pipeline
                    ///that must be waited for instead.
                    UpdateStatusOfNeighbours(incoming, true);
                }
            }
        }
        

        private void UpdateStatusOfNeighbours(Vector3Int chunkId, bool operationIsAdd)
        {
            if (includeDiagonals)
            {
                for (int i = 0; i < DiagonalDirectionExtensions.numDirections; i++)
                {
                    var neighbourId = chunkId + DiagonalDirectionExtensions.Vectors[i];
                    if (neighbourStatuses.TryGetValue(neighbourId, out var neighbourStatus))
                    {
                        if (operationIsAdd)
                        {
                            neighbourStatus.AddNeighbour(i);
                            CheckIfWaitOver(neighbourId, neighbourStatus);
                        }
                        else
                        {
                            neighbourStatus.RemoveNeighbour(i);
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < DirectionExtensions.numDirections; i++)
                {
                    var neighbourId = chunkId + DirectionExtensions.Vectors[i];
                    if (neighbourStatuses.TryGetValue(neighbourId, out var neighbourStatus))
                    {
                        if (operationIsAdd)
                        {
                            neighbourStatus.AddNeighbour(i);
                            CheckIfWaitOver(neighbourId, neighbourStatus);
                        }
                        else
                        {
                            neighbourStatus.RemoveNeighbour(i);
                        }
                    }
                }
            }
        }

        private INeighbourStatus ComputeStatus(Vector3Int chunkId)
        {
            INeighbourStatus status;
            if (includeDiagonals)
            {
                status = new DiagonalNeighbourStatus();
                for (int i = 0; i < DiagonalDirectionExtensions.numDirections; i++)
                {
                    var neighbourId = chunkId + DiagonalDirectionExtensions.Vectors[i];
                    if (pipeline.ChunkMinStageGreaterThan(neighbourId, prevStageId))
                    {
                        status.AddNeighbour(i);
                    }
                }
            }
            else
            {
                status = new NeighbourStatus();
                for (int i = 0; i < DirectionExtensions.numDirections; i++)
                {
                    var neighbourId = chunkId + DirectionExtensions.Vectors[i];
                    if (pipeline.ChunkMinStageGreaterThan(neighbourId, prevStageId))
                    {
                        status.AddNeighbour(i);
                    }
                }
            }
            return status;
        }

        private void CheckIfWaitOver(Vector3Int id, INeighbourStatus status)
        {
            if (status.AllValid)
            {
                waitEndedSet.Add(id);
            }
        }

        public override bool Contains(Vector3Int chunkID)
        {
            return neighbourStatuses.ContainsKey(chunkID);
        }

        public override void Update()
        {
            base.Update();

            foreach (var item in waitEndedSet)
            {
                if (neighbourStatuses.TryGetValue(item,out var status))
                {
                    if (status.AllValid) 
                    {
                        if (CheckAndResolvePreconditionsBeforeExit(item))
                        {
                            MovingOnThisUpdate.Add(item);
                            neighbourStatuses.Remove(item);
                            OnWaitEnded(item);
                        }
                    }
                    else
                    {
                        ///Status changed since it was added to the waitEndedSet,
                        ///it cannot be moved on yet.
                    }
                }
            }

            waitEndedSet.Clear();
        }

        /// <summary>
        /// Checks the preconditions assumed for the chunkId before it can exit the stage.
        /// Also handles resolution of these precondtions.
        /// </summary>
        /// <param name="chunkId"></param>
        /// <returns></returns>
        private bool CheckAndResolvePreconditionsBeforeExit(Vector3Int chunkId)
        {
            if (TerminateHereCondition(chunkId))
            {
                ///This must be false on entry, and therefore should still be false on exit.
                ///This can be violated if the target stage of a chunkId changes, in which case the
                ///id should terminate here.
                neighbourStatuses.Remove(chunkId);//Stop tracking this id
                return false;
            }
            return true;
        }

        /// <summary>
        /// Terminate if the target stage is not greater than the current stage.
        /// </summary>
        /// <param name="chunkId"></param>
        /// <returns></returns>
        protected override bool TerminateHereCondition(Vector3Int chunkId)
        {
            return !pipeline.TargetStageGreaterThanCurrent(chunkId, StageID);
        }

        
    }

}