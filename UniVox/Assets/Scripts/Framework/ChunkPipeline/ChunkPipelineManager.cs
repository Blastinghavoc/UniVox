using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Framework.ChunkPipeline
{
    public class ChunkPipelineManager<ChunkDataType, VoxelDataType> 
        where ChunkDataType : IChunkData<VoxelDataType>
        where VoxelDataType : IVoxelData
    {
        private List<PipelineStage> stages = new List<PipelineStage>();
        private AbstractChunkManager<ChunkDataType,VoxelDataType> chunkManager;

        private Dictionary<Vector3Int, ChunkStageData> chunkStageMap = new Dictionary<Vector3Int, ChunkStageData>();

        private Func<Vector3Int, AbstractChunkComponent<ChunkDataType, VoxelDataType>> getChunkComponent;

        //Possible target stages
        public int DataStage { get; private set; }
        public int RenderedStage { get; private set; }
        public int CompleteStage { get; private set; }

        public ChunkPipelineManager(AbstractChunkManager<ChunkDataType, VoxelDataType> chunkManager,
            Func<Vector3Int,AbstractChunkComponent<ChunkDataType,VoxelDataType>> getChunkComponent
            ,int maxDataPerUpdate,int maxMeshPerUpdate,int maxCollisionPerUpdate) 
        {
            this.chunkManager = chunkManager;
            this.getChunkComponent = getChunkComponent;

            int i = 0;

            stages.Add(new RateLimitedPipelineStage("ScheduledForData",i++,maxDataPerUpdate));
            stages.Add(new WaitForJobStage<ChunkDataType>("GeneratingData", i++, makeDataGenJob,
                (cId, dat) => getChunkComponent(cId).Data = dat)) ;
            DataStage = i;
            stages.Add(new PipelineStage("GotData",i++));

            if (chunkManager.chunkMesher.IsMeshDependentOnNeighbourChunks)
            {
                DataStage = i;
                stages.Add(new WaitingPipelineStage("WaitingForNeighbourData",i++, ShouldScheduleForNext,(cId,_)=>NeighboursHaveData(cId)));
            }
            else
            {
                stages[DataStage].NextStageCondition = ShouldScheduleForNext;
            }

            stages.Add(new RateLimitedPipelineStage("ScheduledForMesh",i++,maxMeshPerUpdate));
            stages.Add(new WaitForJobStage<Mesh>("GeneratingMesh",i++,makeMeshingJob,
                (cId,mesh)=>getChunkComponent(cId).SetRenderMesh(mesh)));
            RenderedStage = i;
            stages.Add(new PipelineStage("GotMesh",i++, ShouldScheduleForNext));

            stages.Add(new RateLimitedPipelineStage("ScheduledForCollisionMesh",i++,maxCollisionPerUpdate));
            stages.Add(new WaitForJobStage<Mesh>("ApplyingCollisionMesh",i++,makeCollisionMeshingJob,
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
            //TODO add chunk to first stage
        }

        public void SetTarget(Vector3Int chunkId, int targetStage) 
        {
            if(chunkStageMap.TryGetValue(chunkId,out var stageData))                
            {
                stageData.targetStage = targetStage;
            }
        }

        public void RemoveChunk(Vector3Int chunkId) 
        {
            chunkStageMap.Remove(chunkId);
        }

        private bool ChunkDataReadable(ChunkStageData stageData) 
        {
            return stageData.minStage >= DataStage;
        }

        private AbstractPipelineJob<ChunkDataType> makeDataGenJob(Vector3Int chunkID) 
        {
            return new BasicDataGenerationJob<ChunkDataType, VoxelDataType>(chunkManager.chunkProvider, chunkID);
        }

        private AbstractPipelineJob<Mesh> makeMeshingJob(Vector3Int chunkID) 
        {
            return new BasicMeshGenerationJob<ChunkDataType, VoxelDataType>(chunkManager.chunkMesher, getChunkComponent(chunkID).Data);
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
                if (!chunkStageMap.TryGetValue(neighbourID, out var neighbourStageData) || !ChunkDataReadable(neighbourStageData))
                {
                    //The data for this neighbour is not available

                    allHaveData = false;

                    //TODO generate the missing chunk
                    
                }

            }
            return allHaveData;
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

            return stages[currentStage].Contains(chunkID);
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
        }

    }
}