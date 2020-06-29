using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using UniVox.Framework.Jobified;
using UniVox.Implementations.ChunkData;

namespace UniVox.Framework.ChunkPipeline
{
    public class ChunkPipelineManager : IDisposable
    {
        private List<PipelineStage> stages = new List<PipelineStage>();

        private Dictionary<Vector3Int, ChunkStageData> chunkStageMap = new Dictionary<Vector3Int, ChunkStageData>();

        private Func<Vector3Int, IChunkComponent> getChunkComponent;

        private Action<Vector3Int, int> createNewChunkWithTarget;

        //Possible target stages
        //This stage indicates the chunk has all the terrain data, but no "structures" like trees
        public int TerrainDataStage { get; private set; }
        //This stage indicates the chunk has been fully generated, including structures
        public int AllDataStage { get; private set; }
        public int RenderedStage { get; private set; }
        public int CompleteStage { get; private set; }

        public ChunkPipelineManager(IChunkProvider chunkProvider,
            IChunkMesher chunkMesher,
            Func<Vector3Int, IChunkComponent> getChunkComponent,
            Action<Vector3Int, int> createNewChunkWithTarget,
            Func<Vector3Int, float> getPriorityOfChunk,
            int maxDataPerUpdate, int maxMeshPerUpdate, int maxCollisionPerUpdate,
            bool structureGen = false, int maxStructurePerUpdate = 200)
        {
            this.getChunkComponent = getChunkComponent;
            this.createNewChunkWithTarget = createNewChunkWithTarget;

            int i = 0;

            var ScheduledForData = new PrioritizedStage("ScheduledForData", i++,
                maxDataPerUpdate,
                TargetStageGreaterThanCurrent,
                NextStageFreeForChunk,
                getPriorityOfChunk);
            stages.Add(ScheduledForData);

            var GeneratingData = new WaitForJobStage<IChunkData>("GeneratingData", i++,
                TargetStageGreaterThanCurrent,
                chunkProvider.GenerateTerrainData,
                (cId,dat)=> {
                    dat.FullyGenerated = !structureGen;//if structure generation is on, the chunk is not fully generated until after that
                    getChunkComponent(cId).Data = dat;
                },
                maxDataPerUpdate);
            stages.Add(GeneratingData);

            //Ensure that ScheduledForData never sends too many items per update
            ScheduledForData.UpdateMax = GeneratingData.MaxToEnter;

            TerrainDataStage = i;

            ///The function to be executed to determine whether a chunk is ready for meshing
            Func<Vector3Int, int, bool, bool> MeshDependencyFunction = NeighboursPassedStage;

            if (structureGen)
            {
                stages.Add(new PipelineStage("GotTerrainData", i++, ShouldScheduleForNext));

                ///Wait until all neighbours including diagonal ones have their data 
                var waitForNeighbourTerrain = new WaitingStage("WaitForNeighbourTerrain", i++,
                    ShouldScheduleForNext,
                    (cId, _) => NeighboursAndDiagonalsPassedStage(cId, TerrainDataStage)
                    );
                stages.Add(waitForNeighbourTerrain);

                var scheduledForStructures = new PrioritizedStage("ScheduledForStructures", i++, 1,
                    TargetStageGreaterThanCurrent,
                    NextStageFreeForChunk,
                    getPriorityOfChunk
                    );
                scheduledForStructures.Precondition = (cId) => NeighboursAndDiagonalsPassedStage(cId, TerrainDataStage);
                stages.Add(scheduledForStructures);

                var generatingStructures = new WaitForJobStage<ChunkNeighbourhood>("GeneratingStructures", i++,
                    TargetStageGreaterThanCurrent,
                    (id) => chunkProvider.GenerateStructuresForNeighbourhood(id,
                        new ChunkNeighbourhood(id,(neighId)=>getChunkComponent(neighId).Data,true)),
                    (a, b) => { return; },
                    maxStructurePerUpdate
                    );
                //TODO remove DEBUG                
                generatingStructures.PreconditionCheck = (cId) => NeighboursAndDiagonalsPassedStage(cId, TerrainDataStage);
                

                //Generating structures is not free for an ID if it contains that ID or any of its neighbours (to prevent multiple data access)
                generatingStructures.FreeFor = (id) => !Utils.Helpers.GetNeighboursIncludingDiagonal(id).Any((neigh)=>generatingStructures.Contains(neigh));
                stages.Add(generatingStructures);
                scheduledForStructures.UpdateMax = generatingStructures.MaxToEnter;


                var waitingForAllNeighboursToHaveStructures = new WaitingStage("WaitForNeighbourStructures", i++,
                    ShouldScheduleForNext,
                    (cId, _) => NeighboursAndDiagonalsPassedStage(cId, TerrainDataStage));
                waitingForAllNeighboursToHaveStructures.OnWaitEnded = (id) => {
                        Assert.IsFalse(getChunkComponent(id).Data.FullyGenerated,$"Chunk data for {id} was listed as fully generated before all its neighbours had structures");
                        getChunkComponent(id).Data.FullyGenerated = true;
                    };
                stages.Add(waitingForAllNeighboursToHaveStructures);

                MeshDependencyFunction = NeighboursAndDiagonalsPassedStage;
            }


            AllDataStage = i;
            stages.Add(new PipelineStage("GotAllData", i++));

            if (chunkMesher.IsMeshDependentOnNeighbourChunks)
            {
                /// If mesh is dependent on neighbour chunks, add a waiting stage
                /// to wait until the neighbours of a chunk have their data before
                /// moving that chunk onwards through the pipeline
                AllDataStage = i;
                stages.Add(new WaitingStage("WaitForNeighbourData", i++,
                    ShouldScheduleForNext,
                    (cId, _) => MeshDependencyFunction(cId, AllDataStage, true)));
            }
            else
            {
                stages[AllDataStage].NextStageCondition = ShouldScheduleForNext;
            }


            var ScheduledForMesh = new PrioritizedStage("ScheduledForMesh", i++,
                maxMeshPerUpdate,
                TargetStageGreaterThanCurrent,
                NextStageFreeForChunk,
                getPriorityOfChunk);
            if (chunkMesher.IsMeshDependentOnNeighbourChunks)
            {
                ///If mesh is dependent on neighbours, check the precondition has not changed
                ///before a chunk is allowed to go from the ScheduledForMesh stage to GeneratingMesh
                ScheduledForMesh.Precondition = (cId) => MeshDependencyFunction(cId, AllDataStage,false);
            }
            stages.Add(ScheduledForMesh);

            var GeneratingMesh = new WaitForJobStage<MeshDescriptor>("GeneratingMesh", i++,
                TargetStageGreaterThanCurrent,
                chunkMesher.CreateMeshJob,
                (cId, meshDescriptor) => getChunkComponent(cId).SetRenderMesh(meshDescriptor), 
                maxMeshPerUpdate);
            stages.Add(GeneratingMesh);
            ScheduledForMesh.UpdateMax = GeneratingMesh.MaxToEnter;

            //TODO remove DEBUG
            if (chunkMesher.IsMeshDependentOnNeighbourChunks)
            {
                GeneratingMesh.PreconditionCheck = (cId) => MeshDependencyFunction(cId, AllDataStage,false);
            }


            RenderedStage = i;
            stages.Add(new PipelineStage("GotMesh", i++, ShouldScheduleForNext));

            var ScheduledForCollisionMesh = new PrioritizedStage("ScheduledForCollisionMesh", i++, maxCollisionPerUpdate, TargetStageGreaterThanCurrent, NextStageFreeForChunk, getPriorityOfChunk);
            stages.Add(ScheduledForCollisionMesh);

            var ApplyingCollisionMesh = new WaitForJobStage<Mesh>("ApplyingCollisionMesh", i++, TargetStageGreaterThanCurrent, chunkMesher.ApplyCollisionMeshJob,
                (cId, mesh) => getChunkComponent(cId).SetCollisionMesh(mesh), maxCollisionPerUpdate);
            stages.Add(ApplyingCollisionMesh);
            CompleteStage = i;
            ScheduledForCollisionMesh.UpdateMax = ApplyingCollisionMesh.MaxToEnter;


            //Final stage "nextStageCondition" is always false
            stages.Add(new PipelineStage("Complete", i++, (a, b) => false));

        }

        /// <summary>
        /// Runs through the pipeline stages in order, moving chunks through the
        /// pipeline when necessary.
        /// </summary>
        public void Update()
        {
            Profiler.BeginSample("PipelineUpdate");
            for (int stageIndex = 0; stageIndex < stages.Count; stageIndex++)
            {
                var stage = stages[stageIndex];
                Profiler.BeginSample(stage.Name + "Stage");
                stage.Update();
                Profiler.EndSample();
                var movingOn = stage.MovingOnThisUpdate;

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

                var goingBackwards = stage.GoingBackwardsThisUpdate;
                int prevStageIndex = stageIndex - 1;
                foreach (var item in goingBackwards)
                {
                    //Send the item back one stage, as long as it's valid to do so
                    if (chunkStageMap.TryGetValue(item, out var stageData))
                    {
                        if (stageData.maxStage >= prevStageIndex)
                        {
                            ReenterAtStage(item, prevStageIndex, stageData);
                            if (stageData.maxStage == stageIndex)
                            {
                                ///This was the max stage, it has now been demoted
                                stageData.maxStage = prevStageIndex;
                            }
                        }
                        else
                        {
                            ///Discard the item, its state is not currently valid.
                            ///This can only be because the chunk id was removed from the pipeline
                            ///and re-added while this item was stuck in this stage, and it has 
                            ///only just been released. Being here implies that the id was re-added
                            ///with a lower priority.
                        }
                    }
                    else
                    {
                        ///Otherwise, the item has been removed from the pipeline and should be discarded, but
                        ///that should have happened earlier than this (it should have been added to the Terminating
                        ///list, not the GoingBackwards list)
                        throw new Exception($"{item} terminated in an unexpected way, it was part of the GoingBackwards list " +
                            $"when it should have been part of the terminating list");
                    }
                }
            }
            Profiler.EndSample();

        }

        public void Add(Vector3Int chunkId, int targetStage)
        {
            Assert.IsFalse(chunkStageMap.ContainsKey(chunkId));
            chunkStageMap.Add(chunkId, new ChunkStageData() { targetStage = targetStage });
            //Add to first stage
            if (!stages[0].Contains(chunkId))
            {
                ///Note that there is a possibility that the first stage could already
                ///contain the "new" chunk; if it had been added before, and then went out
                ///of range the first stage may not have gotten around to removing it yet.
                stages[0].Add(chunkId);
            }
        }

        /// <summary>
        /// Add a chunk to the pipeline that already has data (e.g it was loaded from storage)
        /// Therefore it skips the usual generation stages.
        /// </summary>
        /// <param name="chunkId"></param>
        public void AddWithData(Vector3Int chunkId, int targetStage)
        {
            Assert.IsFalse(chunkStageMap.ContainsKey(chunkId));

            var EnterAtStageId = AllDataStage;

            chunkStageMap.Add(chunkId, new ChunkStageData() { 
                targetStage = targetStage, 
                minStage = EnterAtStageId,
                maxStage = EnterAtStageId
            });

            //Add to AllData stage
            if (!stages[EnterAtStageId].Contains(chunkId))
            {
                ///Note that there is a possibility that the stage could already
                ///contain the "new" chunk; if it had been added before, and then went out
                ///of range the stage may not have gotten around to removing it yet.
                stages[EnterAtStageId].Add(chunkId);
            }
        }

        public void SetTarget(Vector3Int chunkId, int targetStage)
        {
            //TODO remove DEBUG
            if (targetStage != AllDataStage && targetStage != RenderedStage && targetStage != CompleteStage && targetStage != TerrainDataStage)
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
                    ReenterAtStage(chunkId, stageData.maxStage, stageData);
                }
            }
            else if (targetStage < prevTarget)
            {
                var prevMax = stageData.maxStage;

                if (targetStage < AllDataStage && prevMax >= AllDataStage)
                {
                    ///Do not allow a chunk to be downgraded to a stage lower than AllData if it has
                    ///already reached or surpassed that stage.
                    ///They may still be created targetting a lower stage, but cannot go backwards
                    targetStage = AllDataStage;
                    //Recursively try to set the target again
                    SetTarget(chunkId, targetStage);
                    return;
                }

                var newMax = Math.Min(targetStage, stageData.maxStage);
                stageData.maxStage = newMax;

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
                else
                {
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
        public void ReenterAtStage(Vector3Int chunkID, int stage)
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
                throw new ArgumentOutOfRangeException($"Cannot reenter chunk id {chunkID} at stage {stage} because it has " +
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

        public bool Contains(Vector3Int chunkID) 
        {
            var thinkWeContain = chunkStageMap.ContainsKey(chunkID);
            var actuallyContain = false;
            var containingName = "";
            foreach (var stage in stages)
            {
                if (stage.Contains(chunkID))
                {
                    actuallyContain = true;
                    containingName = stage.Name;
                    break;
                }
            }

            if (thinkWeContain != actuallyContain)
            {
                Debug.LogWarning($"Chunk stage map contains id {chunkID} = {thinkWeContain}, any of the stages contain the id = {actuallyContain}." +
                $" Containing stage name if applicable = {containingName}");
            }

            return thinkWeContain;
        }

        private bool ChunkDataReadable(ChunkStageData stageData)
        {
            return stageData.minStage >= AllDataStage;
        }

        /// <summary>
        /// Checks whether all neighbours of the given chunk ID are in at least the desired stage,
        /// and optionally requests generation of any that are not.
        /// </summary>
        /// <param name="chunkID"></param>
        /// <param name="generateMissing"></param>
        /// <returns></returns>
        private bool NeighboursPassedStage(Vector3Int chunkID, int targetStage, bool generateMissing = false)
        {
            bool allHaveData = true;
            foreach (var neighbourID in Utils.Helpers.GetNeighboursDirectOnly(chunkID))
            {
                ChunkPassedStage(neighbourID, targetStage, ref allHaveData, generateMissing);
                if (!generateMissing && !allHaveData)
                {
                    //Early stopping
                    break;
                }
            }

            return allHaveData;
        }

        private bool NeighboursAndDiagonalsPassedStage(Vector3Int chunkID, int targetStage, bool generateMissing = false)
        {
            bool allHaveData = true;

            foreach (var neighbourID in Utils.Helpers.GetNeighboursIncludingDiagonal(chunkID))
            {
                ChunkPassedStage(neighbourID, targetStage, ref allHaveData, generateMissing);
                if (!generateMissing&&!allHaveData)
                {
                    //Early stopping
                    break;
                }
            }

            return allHaveData;

        }

        private void ChunkPassedStage(Vector3Int id, int targetStage, ref bool allValid, bool generateMissing)
        {
            if (!chunkStageMap.TryGetValue(id, out var neighbourStageData))
            {
                if (generateMissing)
                {
                    //The chunk does not exist at all, generate it to the target stage
                    createNewChunkWithTarget(id, targetStage);
                }
                allValid = false;
            }
            else if (!(neighbourStageData.minStage >= targetStage))
            {                
                allValid = false;
            }
        }

        /// <summary>
        /// Wait function that causes a chunk to wait in a stage until the next stage
        /// is able to accept it.
        /// As with all wait functions, True indicates the wait is over
        /// </summary>
        /// <param name="chunkID"></param>
        /// <param name="currentStage"></param>
        /// <returns></returns>
        private bool NextStageFreeForChunk(Vector3Int chunkID, int currentStage)
        {
            if (currentStage >= stages.Count)
            {
                return false;
            }

            return stages[currentStage + 1].FreeFor(chunkID);
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

            return stages[currentStage + 1].Contains(chunkID);
        }


        private bool TargetStageGreaterThanCurrent(Vector3Int chunkID, int currentStage)
        {
            if (chunkStageMap.TryGetValue(chunkID, out var stageData))
            {
                return stageData.targetStage > currentStage;
            }
            return false;
        }

        public void Dispose()
        {
            Debug.Log(GetPipelineStatus());
            foreach (var stage in stages)
            {
                if (stage is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        public string GetPipelineStatus()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var stage in stages)
            {
                sb.AppendLine($"{stage.Name}:{stage.Count}");
            }
            return sb.ToString();
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