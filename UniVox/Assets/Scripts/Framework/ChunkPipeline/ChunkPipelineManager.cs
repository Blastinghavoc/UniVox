using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using UniVox.Framework.ChunkPipeline.WaitForNeighbours;
using UniVox.Framework.Jobified;
using UniVox.Implementations.ChunkData;
using Utils;

namespace UniVox.Framework.ChunkPipeline
{
    public class ChunkPipelineManager : IDisposable,IChunkPipeline
    {
        private List<IPipelineStage> stages = new List<IPipelineStage>();

        private Dictionary<Vector3Int, ChunkStageData> chunkStageMap = new Dictionary<Vector3Int, ChunkStageData>();

        public Func<Vector3Int, IChunkComponent> getChunkComponent { get; private set; }

        public event ChunkRemovedHandler OnChunkRemovedFromPipeline = delegate { };
        public event ChunkAddedHandler OnChunkAddedToPipeline = delegate { };
        public event ChunkMinStageDecreasedHandler OnChunkMinStageDecreased = delegate { };

        //Possible target stages
        //This stage indicates the chunk has all the terrain data, but no "structures" like trees
        public int TerrainDataStage { get; private set; }
        //Indicates chunk has generated its own structures, but may not have received all incoming structures from neighbours
        public int OwnStructuresStage { get; private set; }
        //This stage indicates the chunk has been fully generated, including structures from neighbours
        public int FullyGeneratedStage { get; private set; }
        public int RenderedStage { get; private set; }
        public int CompleteStage { get; private set; }

        public IChunkProvider chunkProvider { get; private set; }
        public IChunkMesher chunkMesher{ get; private set; }

        public bool StructureGen { get; private set; }

        /// <summary>
        /// Used to detect whether the pipeline is currently updating, and
        /// prevent new chunks being added to it while it is.
        /// </summary>
        private bool updateLock = false;

        public ChunkPipelineManager(IChunkProvider chunkProvider,
            IChunkMesher chunkMesher,
            Func<Vector3Int, IChunkComponent> getChunkComponent,
            Func<Vector3Int, float> getPriorityOfChunk,
            int maxDataPerUpdate,
            int maxMeshPerUpdate, 
            int maxCollisionPerUpdate, 
            bool structureGen = false,
            int maxStructurePerUpdate = 200)
        {
            this.getChunkComponent = getChunkComponent;
            this.chunkProvider = chunkProvider;
            this.chunkMesher = chunkMesher;
            this.StructureGen = structureGen;

            int i = 0;

            var ScheduledForData = new PrioritizedBufferStage("ScheduledForData", i++,
                this, getPriorityOfChunk);
            stages.Add(ScheduledForData);

            var GeneratingData = new GenerateTerrainStage(
                "GeneratingTerrain", i++, this, maxDataPerUpdate);
            stages.Add(GeneratingData);

            TerrainDataStage = i;
            OwnStructuresStage = i;

            if (structureGen)
            {
                ///Wait until all neighbours including diagonal ones have their terrain data 
                var waitForNeighbourTerrain = new WaitForNeighboursStage(
                    "WaitForNeighbourTerrain", i++, this, true);
                stages.Add(waitForNeighbourTerrain);

                var scheduledForStructures = new PrioritizedBufferStage(
                    "ScheduledForStructures", i++, this, getPriorityOfChunk);
                stages.Add(scheduledForStructures);

                var generatingStructures = new GenerateStructuresStage(
                    "GeneratingStructures", i++, this, maxStructurePerUpdate);                
                stages.Add(generatingStructures);

                ///At this point the chunk has generated all of its own structures.
                OwnStructuresStage = i;
                var waitingForAllNeighboursToHaveStructures = new WaitForNeighboursStage(
                    "WaitForNeighbourStructures", i++, this,true);
                waitingForAllNeighboursToHaveStructures.OnWaitEnded = (id) => {
                        Assert.IsFalse(getChunkComponent(id).Data.FullyGenerated,$"Chunk data for {id} was listed as fully generated before all its neighbours had structures");
                        getChunkComponent(id).Data.FullyGenerated = true;
                    };

                stages.Add(waitingForAllNeighboursToHaveStructures);

            }


            FullyGeneratedStage = i;

            if (chunkMesher.IsMeshDependentOnNeighbourChunks)
            {
                /// If mesh is dependent on neighbour chunks, add a waiting stage
                /// to wait until the neighbours of a chunk have their data before
                /// moving that chunk onwards through the pipeline. Diagonals are included
                /// in the neighbourhood if structureGen is on.
                stages.Add(new WaitForNeighboursStage("GotDataWaitingForNeighbours", i++,
                    this, includeDiagonals: structureGen));
            }
            else
            {
                ///Otherwise, the chunk can move onwards freely
                stages.Add(new PassThroughStage("GotAllData", i++,this));
            }


            var ScheduledForMesh = new PrioritizedBufferStage("ScheduledForMesh", i++,
                this, getPriorityOfChunk);
            stages.Add(ScheduledForMesh);

            var GeneratingMesh = new GenerateMeshStage("GeneratingMesh", i++,
                this, maxMeshPerUpdate);
            stages.Add(GeneratingMesh);


            RenderedStage = i;
            stages.Add(new PassThroughStage("GotMesh", i++, this));

            var ScheduledForCollisionMesh = new PrioritizedBufferStage(
                "ScheduledForCollisionMesh", i++, this, getPriorityOfChunk);
            stages.Add(ScheduledForCollisionMesh);

            var ApplyingCollisionMesh = new ApplyCollisionMeshStage(
                "ApplyingCollisionMesh", i++, this, maxCollisionPerUpdate);
            stages.Add(ApplyingCollisionMesh);

            //Final stage
            CompleteStage = i;
            stages.Add(new PassThroughStage("Complete", i++, this));


            foreach (var stage in stages)
            {
                stage.Initialise();
            }
        }

        /// <summary>
        /// Runs through the pipeline stages in order, moving chunks through the
        /// pipeline when necessary.
        /// </summary>
        public void Update()
        {
            Profiler.BeginSample("PipelineUpdate");
            updateLock = true;
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

                    foreach (var item in movingOn)
                    {
                        if (chunkStageMap.TryGetValue(item, out var stageData))
                        {
                            //If current stage is the min stage, increase the min stage by one
                            stageData.minStage = (stageIndex == stageData.minStage) ? nextStageIndex : stageData.minStage;
                            //Update max stage
                            stageData.maxStage = Math.Max(stageData.maxStage, nextStageIndex);

                            nextStage.Add(item, stageData);
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
                        ///that should have happened earlier than this (it should have terminated, not gone backwards)
                        throw new Exception($"{item} terminated in an unexpected way, it was part of the GoingBackwards list " +
                            $"when it should not have been");
                    }
                }

                stage.ClearLists();
            }
            updateLock = false;
            Profiler.EndSample();

        }

        private void CheckLockBeforeExternalOperation() 
        {
            if (updateLock)
            {
                throw new Exception("Cannot add, remove or set target of item in the pipeline while it is updating");
            }
        }

        public void Add(Vector3Int chunkId, int targetStage)
        {
            CheckLockBeforeExternalOperation();

            Assert.IsFalse(chunkStageMap.ContainsKey(chunkId));
            var stageData = new ChunkStageData() { targetStage = targetStage, minStage = 0, maxStage = 0 };
            chunkStageMap.Add(chunkId, stageData);
            //Add to first stage
            stages[0].Add(chunkId,stageData);

            Profiler.BeginSample("ChunkAddedToPipelineEvent");
            OnChunkAddedToPipeline(chunkId, 0);
            Profiler.EndSample();
        }

        /// <summary>
        /// Add a chunk to the pipeline that already has data (e.g it was loaded from storage)
        /// Therefore it skips the usual generation stages.
        /// </summary>
        /// <param name="chunkId"></param>
        public void AddWithData(Vector3Int chunkId, int targetStage)
        {
            CheckLockBeforeExternalOperation();

            Assert.IsFalse(chunkStageMap.ContainsKey(chunkId));

            var EnterAtStageId = FullyGeneratedStage;

            var stageData = new ChunkStageData()
            {
                targetStage = targetStage,
                minStage = EnterAtStageId,
                maxStage = EnterAtStageId
            };

            chunkStageMap.Add(chunkId, stageData);

            //Add to AllData stage
            stages[EnterAtStageId].Add(chunkId,stageData);

            Profiler.BeginSample("ChunkAddedToPipelineEvent");
            OnChunkAddedToPipeline(chunkId, EnterAtStageId);
            Profiler.EndSample();
        }

        public void SetTarget(Vector3Int chunkId, int targetStage)
        {
            CheckLockBeforeExternalOperation();

            //TODO remove DEBUG
            if (targetStage != FullyGeneratedStage && 
                targetStage != RenderedStage && 
                targetStage != CompleteStage && 
                targetStage != OwnStructuresStage &&
                targetStage != TerrainDataStage)
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

                if (targetStage < FullyGeneratedStage && prevMax >= FullyGeneratedStage)
                {
                    ///Do not allow a chunk to be downgraded to a stage lower than Fully Generated if it has
                    ///already reached or surpassed that stage.
                    ///They may still be created targetting a lower stage, but cannot go backwards
                    targetStage = FullyGeneratedStage;
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
                    SetNewMinimumIfSmallerThanCurrent(chunkId, stageData, stageData.maxStage);
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
            CheckLockBeforeExternalOperation();
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

            //Potentially update min
            SetNewMinimumIfSmallerThanCurrent(chunkID, stageData, stage);

            stageToEnter.Add(chunkID,stageData);
            
        }

        private void SetNewMinimumIfSmallerThanCurrent(Vector3Int chunkId,ChunkStageData stageData, int proposedMinimum) 
        {
            if (stageData.minStage <= proposedMinimum)
            {
                return;
            }
            //minimum stage is decreasing
            stageData.minStage = proposedMinimum;

            Profiler.BeginSample("ChunkMinStageDecreasedEvent");
            OnChunkMinStageDecreased(chunkId, stageData.minStage);
            Profiler.EndSample();
        }

        public void RemoveChunk(Vector3Int chunkId)
        {
            CheckLockBeforeExternalOperation();

            chunkStageMap.Remove(chunkId);
            Profiler.BeginSample("ChunkRemovedFromPipelineEvent");
            OnChunkRemovedFromPipeline(chunkId);
            Profiler.EndSample();
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
            IPipelineStage containingStage = null;
            foreach (var stage in stages)
            {
                if (stage.Contains(chunkID))
                {
                    actuallyContain = true;
                    containingStage = stage;
                    break;
                }
            }

            if (!thinkWeContain && actuallyContain)
            {
                ///Wait for job stages may still contain items when the stage map does not.
                ///No other stage is allowed to do this though.                
                Assert.IsTrue(containingStage is IWaitForJobStage, $"Chunk stage map contains id {chunkID} = {thinkWeContain}, any of the stages contain the id = {actuallyContain}." +
                $" Containing stage name if applicable = {containingStage.Name}");                   
                
            }

            return thinkWeContain;
        }

        private bool ChunkDataReadable(ChunkStageData stageData)
        {
            return stageData.minStage >= FullyGeneratedStage;
        }
            

        /// <summary>
        /// Checks that the minimum stage is greater than stageId for all neighbours of the
        /// given chunkId. Optionally includes diagonal neighbours.
        /// </summary>
        /// <param name="chunkId"></param>
        /// <param name="stageId"></param>
        /// <param name="includeDiagonals"></param>
        /// <returns></returns>
        public bool AllNeighboursMinStageGreaterThan(Vector3Int chunkId, int stageId,bool includeDiagonals = false) 
        {
            if (includeDiagonals)
            {
                for (int i = 0; i < DiagonalDirectionExtensions.numDirections; i++)
                {
                    var neighbourId = chunkId + DiagonalDirectionExtensions.Vectors[i];
                    if (!ChunkMinStageGreaterThan(neighbourId, stageId))
                    {
                        return false;
                    }
                }
            }
            else
            {
                for (int i = 0; i < DirectionExtensions.numDirections; i++)
                {
                    var neighbourId = chunkId + DirectionExtensions.Vectors[i];
                    if (!ChunkMinStageGreaterThan(neighbourId, stageId))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Returns true if the min stage of the given chunk id is strictly greater than
        /// the given stageId. False otherwise, including when the id is not in the pipeline.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="stageId"></param>
        /// <returns></returns>
        public bool ChunkMinStageGreaterThan(Vector3Int id, int stageId) 
        {
            if (!chunkStageMap.TryGetValue(id, out var neighbourStageData))
            {               
                return false;
            }
            else if (!(neighbourStageData.minStage > stageId))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Wait function that causes a chunk to wait in a stage until the next stage
        /// is able to accept it.
        /// As with all wait functions, True indicates the wait is over
        /// </summary>
        /// <param name="chunkID"></param>
        /// <param name="currentStage"></param>
        /// <returns></returns>
        public bool NextStageFreeForChunk(Vector3Int chunkID, int currentStage)
        {
            if (currentStage >= stages.Count)
            {
                return false;
            }

            return stages[currentStage + 1].FreeFor(chunkID);
        }

        public bool NextStageContainsChunk(Vector3Int chunkID, int currentStage)
        {
            if (currentStage >= stages.Count)
            {
                return false;
            }

            return stages[currentStage + 1].Contains(chunkID);
        }


        public bool TargetStageGreaterThanCurrent(Vector3Int chunkID, int currentStage)
        {
            if (chunkStageMap.TryGetValue(chunkID, out var stageData))
            {
                return stageData.targetStage > currentStage;
            }
            return false;
        }

        public bool TargetStageGreaterThanCurrent(int currentStage, ChunkStageData stageData) 
        {
            return stageData.targetStage > currentStage;
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

        public IPipelineStage NextStage(int currentStage)
        {
            return stages[currentStage + 1];            
        }

    }
}