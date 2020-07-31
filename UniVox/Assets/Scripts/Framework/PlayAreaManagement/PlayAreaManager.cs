using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UniVox.Framework.ChunkPipeline;
using static Utils.Helpers;

namespace UniVox.Framework.PlayAreaManagement
{

    /// <summary>
    /// Class responsible for managing the state of the play area as the 
    /// player moves
    /// </summary>
    [System.Serializable]
    public class PlayAreaManager
    {
        [SerializeField] private Vector3Int collidableChunksRadii;
        [SerializeField] private Vector3Int renderedChunksRadii;
        public Vector3Int CollidableChunksRadii { get => collidableChunksRadii; protected set => collidableChunksRadii = value; }
        public Vector3Int RenderedChunksRadii { get => renderedChunksRadii; protected set => renderedChunksRadii = value; }
        public Vector3Int FullyGeneratedRadii { get; protected set; }
        public Vector3Int StructureChunksRadii { get; protected set; }
        public Vector3Int MaximumActiveRadii { get; protected set; }

        protected class ChunkStage
        {
            public Vector3Int radii;
            public int pipelineStage;

            public ChunkStage(Vector3Int radii, int pipelineStage)
            {
                this.radii = radii;
                this.pipelineStage = pipelineStage;
            }
        }
        //The sequence of chunk stages radiating out from the player position.
        protected ChunkStage[] radiiSequence;

        #region variables for incremental processing

        protected bool IncrementalDone = true;
        protected Queue<IEnumerator>[] processQueuesByRadii;
        private int incrementalProcessCount;//TODO remove DEBUG
        #endregion        

        public IVoxelPlayer Player { get; protected set; }

        /// <summary>
        /// Controls how many chunks are processed per update 
        /// when the play area changes.
        /// </summary>
        [Range(1, 10000)]
        [SerializeField] protected ushort updateRate = 1;
        public ushort UpdateRate { get => updateRate; set => updateRate = value; }

        public int ProcessesQueued { get; private set; }//DEBUG

        //Current chunkID occupied by the Player
        public Vector3Int playerChunkID { get; protected set; }
        public Vector3Int prevPlayerChunkID { get; protected set; }

        protected IChunkManager chunkManager;

        protected WorldSizeLimits worldLimits;

        public PlayAreaManager(Vector3Int collidableChunksRadii, Vector3Int renderedChunksRadii)
        {
            this.collidableChunksRadii = collidableChunksRadii;
            this.renderedChunksRadii = renderedChunksRadii;
        }

        public void Initialise(IChunkManager chunkManager, IChunkPipeline pipeline, IVoxelPlayer player)
        {
            this.chunkManager = chunkManager;
            this.Player = player;
            worldLimits = chunkManager.WorldLimits;            

            IncrementalDone = true;

            Assert.IsTrue(RenderedChunksRadii.All((a, b) => a >= b, CollidableChunksRadii),
                "The rendering radii must be at least as large as the collidable radii");
            Assert.IsTrue(CollidableChunksRadii.All(a => a > 0), "Play area manager does not support collidable radii with any" +
                $" dimensions less than 1. Given collidable radii was {CollidableChunksRadii}");

            //Calculate chunk radii

            //Chunks can exist as just data one chunk further away than the rendered chunks
            FullyGeneratedRadii = RenderedChunksRadii + new Vector3Int(1, 1, 1);
            MaximumActiveRadii = FullyGeneratedRadii;
            var lightBufferRadii = Vector3Int.zero;
            int numRadii = 3;
            if (chunkManager.GenerateStructures)
            {
                numRadii = 5;
                StructureChunksRadii = FullyGeneratedRadii + new Vector3Int(1, 1, 1);
                //Extra radius for just terrain data.
                MaximumActiveRadii = StructureChunksRadii + new Vector3Int(1, 1, 1);
            }
            else if (chunkManager.IncludeLighting)
            {
                numRadii = 4;
                lightBufferRadii = FullyGeneratedRadii + Vector3Int.one;
                MaximumActiveRadii = lightBufferRadii;
            }

            processQueuesByRadii = new Queue<IEnumerator>[numRadii];
            for (int i = 0; i < processQueuesByRadii.Length; i++)
            {
                processQueuesByRadii[i] = new Queue<IEnumerator>();
            }

            //Initialise radii sequence
            radiiSequence = new ChunkStage[numRadii];
            radiiSequence[0] = new ChunkStage(CollidableChunksRadii, pipeline.CompleteStage);
            radiiSequence[1] = new ChunkStage(RenderedChunksRadii, pipeline.RenderedStage);
            radiiSequence[2] = new ChunkStage(FullyGeneratedRadii, pipeline.FullyGeneratedStage);

            //extra radii in the sequence if generating structures
            if (chunkManager.GenerateStructures)
            {
                radiiSequence[3] = new ChunkStage(StructureChunksRadii, pipeline.OwnStructuresStage);
                radiiSequence[4] = new ChunkStage(MaximumActiveRadii, pipeline.TerrainDataStage);
            }
            else if (chunkManager.IncludeLighting)//Extra radii if lighting is included
            {
                //Lighting is dependent on neighbour chunk data.
                radiiSequence[3] = new ChunkStage(lightBufferRadii, pipeline.AllVoxelsNeedLightGenStage);
            }

            playerChunkID = chunkManager.WorldToChunkPosition(Player.Position);

            //Immediately request generation of the chunk the player is in
            chunkManager.SetTargetStageOfChunk(playerChunkID, pipeline.CompleteStage);

            UpdateWholePlayArea();
        }


        public void Update()
        {
            Profiler.BeginSample("PlayAreaManagerUpdate");
            prevPlayerChunkID = playerChunkID;
            playerChunkID = chunkManager.WorldToChunkPosition(Player.Position);

            if (playerChunkID != prevPlayerChunkID)
            {
                RestartIncrementalProcessing();
            }

            if (!IncrementalDone)
            {
                ProcessChunksIncrementally();
            }

            //Freeze the player if the chunk they are in is not complete yet.
            if (!chunkManager.IsChunkComplete(playerChunkID))
            {
                if (playerChunkID.y > worldLimits.MinChunkY && playerChunkID.y < worldLimits.MaxChunkY)
                {
                    //Freeze player if the chunk isn't ready for them (doesn't exist or doesn't have collision mesh)
                    //But only if the chunk is within the world limits
                    Player.AllowMove(false);
                }
            }
            else
            {
                //Remove constraints
                Player.AllowMove(true);
            }
            Profiler.EndSample();
        }

        protected void RestartIncrementalProcessing()
        {
            var chunkDifference = playerChunkID - prevPlayerChunkID;
            var absChunkDifference = chunkDifference.ElementWise(Mathf.Abs);

            ///If the absolute chunk difference is greater than 1 in any direction, 
            ///just recalculate all the targets.
            ///
            //IEnumerator incrementalProcessIterator;
            if (absChunkDifference.Any((_) => _ > 1))
            {
                ///The player most likely teleported, an incremental approach is not guaranteed
                ///to be possible.
                Debug.LogWarning($"Player teleported (moved more than 1 chunk per update) with absolute chunk difference {absChunkDifference}" +
                    $", brute force play area update used");
                ResolveTeleport();
                
                //throw new NotImplementedException();
                //This does not deactivate chunks at the moment!
                //incrementalProcessIterator = SetAllTargetsProcess(playerChunkID, prevPlayerChunkID);
            }
            else
            {
                ///Otherwise, a smarter approach is employed that just updates the chunks for which
                ///the target should change.
                //incrementalProcessIterator = UpdatePlayerAreaIncrementallyDifferenceOnly(playerChunkID,prevPlayerChunkID);
                for (int i = 0; i < radiiSequence.Length; i++)
                {
                    processQueuesByRadii[i].Enqueue(DifferenceBasedIncrementalUpdater(playerChunkID, prevPlayerChunkID, i));
                    ProcessesQueued += 1;
                }
            }


            IncrementalDone = false;
            incrementalProcessCount = 0;
        }

        protected void ProcessChunksIncrementally()
        {
            int processedThisUpdate = 0;

            for (int i = 0; i < processQueuesByRadii.Length; i++)
            {
                var processQueue = processQueuesByRadii[i];
                while (processQueue.Count > 0)
                {
                    var processIterator = processQueue.Peek();

                    if (processedThisUpdate >= updateRate)
                    {
                        //Check before we start iterating too, to prevent updating anything if update rate is 0
                        return;
                    }

                    while (processIterator.MoveNext())
                    {
                        ++processedThisUpdate;
                        ++incrementalProcessCount;
                        if (processedThisUpdate >= updateRate)
                        {
                            return;
                        }
                    }
                    //This process is done
                    --ProcessesQueued;
                    processQueue.Dequeue();
                }
            }

            //Done all processes
            IncrementalDone = true;
            ProcessesQueued = 0;

            Debug.Log($"ProcessChunksIncrementally did {incrementalProcessCount} updates");
        }

        /// <summary>
        /// Set the target stages of all chunks around the player
        /// </summary>
        protected void UpdateWholePlayArea()
        {
            Profiler.BeginSample("UpdateWholePlayArea");

            //Do the whole update at once
            var iterator = SetAllTargetsProcess(playerChunkID, prevPlayerChunkID);

            //DEBUG
            int count = 0;

            while (iterator.MoveNext())
            {
                count++;
            }

            Debug.Log($"UpdateWholePlayArea did {count} updates");

            Profiler.EndSample();
        }

        protected void ResolveTeleport() 
        {
            Profiler.BeginSample("UpdateWholePlayArea");

            //Do the whole update at once
            var iterator = SetAllTargetsProcess(playerChunkID, prevPlayerChunkID);

            var outsideRangeSet = new HashSet<Vector3Int>(chunkManager.GetAllLoadedChunkIds());

            var countProcessed = 0;

            while (iterator.MoveNext())
            {
                outsideRangeSet.Remove((Vector3Int)iterator.Current);
                countProcessed++;
            }

            var countRemoved = outsideRangeSet.Count;

            //remove any chunks that were not processed, as these must be outside the play area
            foreach (var chunkId in outsideRangeSet)
            {
                chunkManager.TryDeactivateChunk(chunkId);
            }

            Debug.Log($"ResolveTeleport did {countProcessed} chunk updates and {countRemoved} deactivations");

            //Clear the process queues, as we've just done the whole update by brute force
            for (int i = 0; i < processQueuesByRadii.Length; i++)
            {
                processQueuesByRadii[i].Clear();
            }

            Profiler.EndSample();
        }

        /// <summary>
        /// Sets the targets of ALL chunks in the radii. This is good when the world loads and the 
        /// chunks don't have a target yet, but inefficient when the player chunk changes, 
        /// as not every chunk will need to change target.
        /// Note that this method does not deactivate any chunks
        /// </summary>
        protected IEnumerator SetAllTargetsProcess(Vector3Int newChunkId, Vector3Int oldChunkId)
        {

            for (int i = 0; i < radiiSequence.Length; i++)
            {
                var stage = radiiSequence[i];

                var endRadii = stage.radii;
                Vector3Int startRadii = Vector3Int.zero;
                if (i > 0)
                {
                    var prevStage = radiiSequence[i - 1];
                    startRadii = prevStage.radii + Vector3Int.one;
                }

                foreach (var chunkId in CuboidalArea(newChunkId, endRadii, startRadii))
                {
                    chunkManager.SetTargetStageOfChunk(chunkId, stage.pipelineStage);
                    yield return chunkId;
                }
            }
        }

        /// <summary>
        /// Incremental updater for a single set of radii in the sequence
        /// </summary>
        /// <param name="newChunkId"></param>
        /// <param name="oldChunkId"></param>
        /// <param name="sequenceIndex"></param>
        /// <returns></returns>
        protected IEnumerator DifferenceBasedIncrementalUpdater(Vector3Int newChunkId, Vector3Int oldChunkId, int sequenceIndex)
        {
            var chunkDifference = newChunkId - oldChunkId;
            var absChunkDifference = chunkDifference.ElementWise(Mathf.Abs);

            Assert.IsTrue(absChunkDifference.All(_ => _ <= 1), $"Difference-based incremental update" +
                $" does not support chunk differences greater than 1 in any dimension. " +
                $"Absoloute difference was {absChunkDifference}");

            var stage = radiiSequence[sequenceIndex];
            ChunkStage nextStage = sequenceIndex + 1 < radiiSequence.Length ? radiiSequence[sequenceIndex + 1] : null;

            var endRadii = stage.radii;

            Vector3Int startRadii = Vector3Int.zero;

            ///Used to prevent later stages overwriting stuff done by previous stages
            Vector3Int prevStageRadii = Vector3Int.zero;
            if (sequenceIndex > 0)
            {
                var prevStage = radiiSequence[sequenceIndex - 1];
                prevStageRadii = prevStage.radii;
                startRadii = prevStage.radii + Vector3Int.one;
            }

            var adjustedStartRadii = (endRadii - absChunkDifference).ElementWise((_) => Mathf.Max(_, 0)) + Vector3Int.one;
            //adjustedStartRadii.Clamp(Vector3Int.zero, endRadii);
            //Only inspect the area that is different
            startRadii = startRadii.ElementWise((a, b) => Mathf.Max(a, b), adjustedStartRadii);

            ///IMPORTANT: this is NOT always the same as newChunkId, because
            ///this process may be executing late as part of a queue.
            Vector3Int currentPlayerChunkIdAtTimeOfExecution = playerChunkID;
            bool thereIsPreviousWork = currentPlayerChunkIdAtTimeOfExecution != newChunkId;
            ///Ensures that the above two variables always remain correct,
            ///irrespective of when the iterator yields (and external values change)
            Action afterYield = () => {
                currentPlayerChunkIdAtTimeOfExecution = playerChunkID;
                thereIsPreviousWork = (sequenceIndex > 0)? currentPlayerChunkIdAtTimeOfExecution != newChunkId
                    : false;//There cannot be any previous work if this is the first stage
            };

            foreach (var offset in CuboidalArea(Vector3Int.zero, endRadii, startRadii))
            {           

                var chunkIdFromOldPos = oldChunkId + offset;
                var chunkIdFromNewPos = newChunkId + offset;                

                ///By definition, the chunk id according to the current pos is in the new area
                ///Check if it is also in the old area, because if it is not then it is a 
                ///chunk that has just been moved into, so needs to upgrade its stage
                if (!InsideCuboid(chunkIdFromNewPos, oldChunkId, endRadii))
                {
                    ///Prevent overwriting previous work
                    if (!thereIsPreviousWork || !InsideCuboid(chunkIdFromNewPos, currentPlayerChunkIdAtTimeOfExecution, prevStageRadii))
                    {
                        //Upgrade the target stage of the chunk, making sure it is an upgrade
                        chunkManager.SetTargetStageOfChunk(chunkIdFromNewPos, stage.pipelineStage, TargetUpdateMode.upgradeOnly);
                        yield return null;
                        afterYield();
                    }
                }

                ///Must check if the id according to the prev pos is in the new area.
                ///If it is, nothing needs to be done, but if it is not, that chunk Id
                ///needs to be downgraded one stage.
                if (!InsideCuboid(chunkIdFromOldPos, newChunkId, endRadii))
                {                  

                    ///Prevent overwriting previous work
                    if (!thereIsPreviousWork || !InsideCuboid(chunkIdFromOldPos, currentPlayerChunkIdAtTimeOfExecution, prevStageRadii))
                    {
                        if (nextStage != null)
                        {
                            //Downgrade the target stage of the chunk, making sure it is a downgrade
                            chunkManager.SetTargetStageOfChunk(chunkIdFromOldPos, nextStage.pipelineStage, TargetUpdateMode.downgradeOnly);
                        }
                        else
                        {
                            //The next stage out from here does not exist -> deactivate the chunk
                            chunkManager.TryDeactivateChunk(chunkIdFromOldPos);
                        }
                        yield return null;
                        afterYield();
                    }

                }

            }
        }

        /// <summary>
        /// Manhattan distance query returning true of the chunk ID is 
        /// within the given radii of the player chunk in each axis
        /// </summary>
        /// <param name="chunkID"></param>
        /// <returns></returns>
        public bool InsideChunkRadius(Vector3Int chunkID, Vector3Int Radii)
        {
            var displacement = playerChunkID - chunkID;
            var absDisplacement = displacement.ElementWise(Mathf.Abs);

            //Inside if all elements of the absolute displacement are less than or equal to the chunk radius
            return absDisplacement.All((a, b) => a <= b, Radii);
        }
    }
}