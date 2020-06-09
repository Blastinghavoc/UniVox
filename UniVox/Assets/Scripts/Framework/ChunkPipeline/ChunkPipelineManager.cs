﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using UnityEngine.Assertions;

namespace UniVox.Framework.ChunkPipeline
{
    public class ChunkPipelineManager<ChunkDataType, VoxelDataType> 
        where ChunkDataType : IChunkData<VoxelDataType>
        where VoxelDataType : IVoxelData
    {
        private List<PipelineStage> stages = new List<PipelineStage>();
        private IChunkProvider<ChunkDataType, VoxelDataType> chunkProvider;
        private IChunkMesher<ChunkDataType, VoxelDataType> chunkMesher;

        private Dictionary<Vector3Int, ChunkStageData> chunkStageMap = new Dictionary<Vector3Int, ChunkStageData>();

        private Func<Vector3Int, IChunkComponent<ChunkDataType, VoxelDataType>> getChunkComponent;

        private Action<Vector3Int, int> createNewChunkWithTarget;

        //Possible target stages
        public int DataStage { get; private set; }
        public int RenderedStage { get; private set; }
        public int CompleteStage { get; private set; }

        public ChunkPipelineManager(IChunkProvider<ChunkDataType, VoxelDataType> chunkProvider,
            IChunkMesher<ChunkDataType, VoxelDataType> chunkMesher,
            Func<Vector3Int,IChunkComponent<ChunkDataType,VoxelDataType>> getChunkComponent,
            Action<Vector3Int, int> createNewChunkWithTarget,
            int maxDataPerUpdate,int maxMeshPerUpdate,int maxCollisionPerUpdate) 
        {
            this.chunkProvider = chunkProvider;
            this.chunkMesher = chunkMesher;
            this.getChunkComponent = getChunkComponent;
            this.createNewChunkWithTarget = createNewChunkWithTarget;

            int i = 0;

            stages.Add(new RateLimitedPipelineStage("ScheduledForData",i++,maxDataPerUpdate,TargetStageGreaterThanCurrent,NextStageFreeForChunk));
            stages.Add(new WaitForJobStage<ChunkDataType>("GeneratingData", i++, TargetStageGreaterThanCurrent, makeDataGenJob,
                (cId, dat) => getChunkComponent(cId).Data = dat)) ;
            DataStage = i;
            stages.Add(new PipelineStage("GotData",i++));

            if (chunkMesher.IsMeshDependentOnNeighbourChunks)
            {
                DataStage = i;
                stages.Add(new WaitingPipelineStage("WaitingForNeighbourData",i++, ShouldScheduleForNext,(cId,_)=>NeighboursHaveData(cId)));
            }
            else
            {
                stages[DataStage].NextStageCondition = ShouldScheduleForNext;
            }

            stages.Add(new RateLimitedPipelineStage("ScheduledForMesh",i++,maxMeshPerUpdate, TargetStageGreaterThanCurrent, NextStageFreeForChunk));
            stages.Add(new WaitForJobStage<Mesh>("GeneratingMesh",i++, TargetStageGreaterThanCurrent, makeMeshingJob,
                (cId,mesh)=>getChunkComponent(cId).SetRenderMesh(mesh)));
            RenderedStage = i;
            stages.Add(new PipelineStage("GotMesh",i++, ShouldScheduleForNext));

            stages.Add(new RateLimitedPipelineStage("ScheduledForCollisionMesh",i++,maxCollisionPerUpdate, TargetStageGreaterThanCurrent, NextStageFreeForChunk));
            stages.Add(new WaitForJobStage<Mesh>("ApplyingCollisionMesh",i++, TargetStageGreaterThanCurrent, makeCollisionMeshingJob,
                (cId,mesh)=>getChunkComponent(cId).SetCollisionMesh(mesh)));
            CompleteStage = i;
            //Final stage "nextStageCondition" is always false
            stages.Add(new PipelineStage("Complete",i++, (a,b)=>false));
        
        }

        /// <summary>
        /// Runs through the pipeline stages in order, moving chunks through the
        /// pipeline when necessary.
        /// </summary>
        public void Update() 
        {
            for (int stageIndex = 0; stageIndex < stages.Count; stageIndex++)
            {
                var stage = stages[stageIndex];
                stage.Update(out var movingOn, out var terminatingHere);

                if (movingOn.Count > 0)
                {
                    var nextStageIndex = stageIndex + 1;
                    var nextStage = stages[nextStageIndex];
                    nextStage.AddAll(movingOn);
                    foreach (var item in movingOn)
                    {
                        if (chunkStageMap.TryGetValue(item, out var stageData))
                        {
                            //If current stage is the min stage, increase the min stage by one
                            stageData.minStage = (stageIndex == stageData.minStage) ? nextStageIndex : stageData.minStage;
                            //Update max stage
                            stageData.maxStage = Math.Max(stageData.maxStage, nextStageIndex);
                        }
                        else 
                        {
                            throw new Exception("Tried to move a chunk id that does not exist to the next pipeline stage");
                        }
                    }
                }
            }

        }

        public void AddChunk(Vector3Int chunkId, int targetStage) 
        {
            chunkStageMap.Add(chunkId, new ChunkStageData() { targetStage = targetStage});
            //Add to first stage
            stages[0].Add(chunkId);
        }

        public void SetTarget(Vector3Int chunkId, int targetStage) 
        {
            if (targetStage != DataStage && targetStage != RenderedStage && targetStage != CompleteStage)
            {
                throw new ArgumentOutOfRangeException("Target stage was not one of the valid target stages");
            }

            var stageData = GetStageData(chunkId);

            var prevTarget = stageData.targetStage;

            if (prevTarget == targetStage)
            {
                //If targets equal, no work to be done
                return;
            }

            //Upgrade
            if (targetStage > prevTarget)
            {
                if (stageData.WorkInProgress)
                {
                    //The existing work will reach the new target
                }
                else
                {
                    //Must restart work from previous max
                    ReenterAtStage(chunkId, stageData.maxStage,stageData);
                }
            }
            else if (targetStage < prevTarget)
            {
                var prevMax = stageData.maxStage;
                stageData.maxStage = Math.Min(targetStage, stageData.maxStage);

                if (stageData.maxStage < prevMax)
                {
                    //Downgrading the maximum state of the chunk

                    var chunkComponent = getChunkComponent(chunkId);
                    if (stageData.maxStage < CompleteStage)
                    {
                        chunkComponent.RemoveCollisionMesh();
                    }
                    if (stageData.maxStage < RenderedStage)
                    {
                        chunkComponent.RemoveRenderMesh();
                    }

                    //Ensure min stage isn't greater than max
                    stageData.minStage = Math.Min(stageData.minStage, stageData.maxStage);
                }
                else {
                    /* target has decreased, but the chunk never reached a higher stage than that,
                     * so nothing extra has to be done. Work in progress will stop at the new target
                    */
                }

            }

            stageData.targetStage = targetStage;

        }

        /// <summary>
        /// Re-enter the chunk id at an earlier stage
        /// </summary>
        /// <param name="chunkID"></param>
        /// <param name="stage"></param>
        public void ReenterAtStage(Vector3Int chunkID,int stage) 
        {
            ReenterAtStage(chunkID, stage, GetStageData(chunkID));
        }

        /// <summary>
        /// Private version of above
        /// </summary>
        /// <param name="chunkID"></param>
        /// <param name="stage"></param>
        /// <param name="stageData"></param>
        private void ReenterAtStage(Vector3Int chunkID, int stage, ChunkStageData stageData) 
        {
            if (stage > stageData.maxStage)
            {
                throw new ArgumentOutOfRangeException($"Cannot reenter chunk id {chunkID} at stage {stage} because it has" +
                    $"never previously reached that stage. The current max stage is {stageData.maxStage}");
            }

            var stageToEnter = stages[stage];
            if (!stageToEnter.Contains(chunkID))
            {
                //Potentially update min
                stageData.minStage = Math.Min(stage, stageData.minStage);

                stageToEnter.Add(chunkID);
            }
        }

        public void RemoveChunk(Vector3Int chunkId) 
        {
            chunkStageMap.Remove(chunkId);
        }

        public int GetMaxStage(Vector3Int chunkId) 
        {
            var stageData = GetStageData(chunkId);
            return stageData.maxStage;
        }

        public int GetMinStage(Vector3Int chunkId)
        {
            var stageData = GetStageData(chunkId);
            return stageData.minStage;
        }

        public int GetTargetStage(Vector3Int chunkId) 
        {
            var stageData = GetStageData(chunkId);
            return stageData.targetStage;
        }

        public bool ChunkDataReadable(Vector3Int chunkId) 
        {
            var stageData = GetStageData(chunkId);
            return ChunkDataReadable(stageData);
        }

        /// <summary>
        /// Get the stage data for a chunk, under the assumption that it exists.
        /// An exception is thrown if it does not exist.
        /// </summary>
        /// <param name="chunkID"></param>
        /// <returns></returns>
        private ChunkStageData GetStageData(Vector3Int chunkID) 
        {
            if (chunkStageMap.TryGetValue(chunkID, out var stageData))
            {
                return stageData;
            }
            else
            {
                throw new ArgumentOutOfRangeException($"Chunk id {chunkID} is not present in the pipeline");
            }
        }

        private bool ChunkDataReadable(ChunkStageData stageData) 
        {
            return stageData.minStage >= DataStage;
        }

        private AbstractPipelineJob<ChunkDataType> makeDataGenJob(Vector3Int chunkID) 
        {
            return new BasicDataGenerationJob<ChunkDataType, VoxelDataType>(chunkProvider, chunkID);
        }

        private AbstractPipelineJob<Mesh> makeMeshingJob(Vector3Int chunkID) 
        {
            return new BasicMeshGenerationJob<ChunkDataType, VoxelDataType>(chunkMesher, getChunkComponent(chunkID).Data);
        }

        private AbstractPipelineJob<Mesh> makeCollisionMeshingJob(Vector3Int chunkID) 
        {
            return new BasicCollisionMeshingJob<ChunkDataType, VoxelDataType>(getChunkComponent(chunkID));
        }

        /// <summary>
        /// Checks whether all neighbours of the given chunk ID have data,
        /// and requests generation of the data for any that do not.
        /// </summary>
        /// <param name="chunkID"></param>
        /// <returns></returns>
        private bool NeighboursHaveData(Vector3Int chunkID) 
        {
            bool allHaveData = true;
            foreach (var dir in Directions.IntVectors)
            {
                var neighbourID = chunkID + dir;

                if (!chunkStageMap.TryGetValue(neighbourID, out var neighbourStageData))
                {
                    //The chunk does not exist at all, generate it to the data stage
                    createNewChunkWithTarget(neighbourID, DataStage);
                    allHaveData = false;
                }
                else if (!ChunkDataReadable(neighbourStageData))
                {
                    allHaveData = false;
                    /* data is not currently readable, but the existence of the chunk in the stage map
                     * implies that it is just waiting to generate its data, so it does not need 
                     * to be created or have its target changed
                     */
                    Assert.IsTrue(neighbourStageData.targetStage >= DataStage);
                }               

            }
            return allHaveData;
        }

        /// <summary>
        /// Wait function that causes a chunk to wait in a stage until the next stage
        /// does not contain it.
        /// As with all wait functions, True indicates the wait is over
        /// </summary>
        /// <param name="chunkID"></param>
        /// <param name="currentStage"></param>
        /// <returns></returns>
        private bool NextStageFreeForChunk(Vector3Int chunkID, int currentStage) 
        {
            return !NextStageContainsChunk(chunkID, currentStage);
        }

        private bool ShouldScheduleForNext(Vector3Int chunkID, int currentStage) 
        {
            return TargetStageGreaterThanCurrent(chunkID, currentStage) && !NextStageContainsChunk(chunkID, currentStage);
        }

        private bool NextStageContainsChunk(Vector3Int chunkID, int currentStage) 
        {
            if (currentStage >= stages.Count)
            {
                return false;
            }

            return stages[currentStage+1].Contains(chunkID);
        }


        private bool TargetStageGreaterThanCurrent(Vector3Int chunkID, int currentStage) 
        {
            if (chunkStageMap.TryGetValue(chunkID,out var stageData))
            {
                return stageData.targetStage > currentStage;
            }
            return false;
        }

        private class ChunkStageData 
        {
            public int maxStage = 0;
            public int minStage = 0;
            public int targetStage;

            public bool WorkInProgress { get => minStage < targetStage || minStage < maxStage; }
        }

    }
}