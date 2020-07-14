using System.Collections;
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
        public Vector3Int CollidableChunksRadii { get => collidableChunksRadii;protected set => collidableChunksRadii = value; }
        public Vector3Int RenderedChunksRadii { get => renderedChunksRadii;protected set => renderedChunksRadii = value; }
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
        protected IEnumerator IncrementalProcessIterator;

        #endregion        

        public IVoxelPlayer Player { get; protected set; }

        /// <summary>
        /// Controls how many chunks are processed per update 
        /// when the play area changes.
        /// </summary>
        [Range(1, 1000)]
        [SerializeField] protected ushort updateRate = 1;
        public ushort UpdateRate { get => updateRate; set => updateRate = value; }

        public int WaitingForPlayAreaUpdate { get; private set; }//DEBUG

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

        public void Initialise(IChunkManager chunkManager, IChunkPipeline pipeline,IVoxelPlayer player)
        {
            this.chunkManager = chunkManager;
            this.Player = player;
            worldLimits = chunkManager.WorldLimits;

            IncrementalDone = true;

            Assert.IsTrue(RenderedChunksRadii.All((a, b) => a >= b, CollidableChunksRadii),
                "The rendering radii must be at least as large as the collidable radii");
            Assert.IsTrue(CollidableChunksRadii.All(a => a > 0),"Play area manager does not support collidable radii with any" +
                $" dimensions less than 1. Given collidable radii was {CollidableChunksRadii}");

            //Calculate chunk radii

            //Chunks can exist as just data one chunk further away than the rendered chunks
            FullyGeneratedRadii = RenderedChunksRadii + new Vector3Int(1, 1, 1);
            MaximumActiveRadii = FullyGeneratedRadii;
            if (chunkManager.GenerateStructures)
            {
                StructureChunksRadii = FullyGeneratedRadii + new Vector3Int(1, 1, 1);
                //Extra radius for just terrain data.
                MaximumActiveRadii = StructureChunksRadii + new Vector3Int(1, 1, 1);
            }

            //Initialise radii sequence
            radiiSequence = new ChunkStage[5] {
            new ChunkStage(CollidableChunksRadii,pipeline.CompleteStage),
            new ChunkStage(RenderedChunksRadii,pipeline.RenderedStage),
            new ChunkStage(FullyGeneratedRadii,pipeline.FullyGeneratedStage),
            new ChunkStage(StructureChunksRadii,pipeline.OwnStructuresStage),
            new ChunkStage(MaximumActiveRadii,pipeline.TerrainDataStage),
            };            

            playerChunkID = chunkManager.WorldToChunkPosition(Player.Position);

            //Immediately request generation of the chunk the player is in
            chunkManager.SetTargetStageOfChunk(playerChunkID, pipeline.CompleteStage);

            UpdateWholePlayArea();
        }


        public void Update()
        {
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
        }

        protected void RestartIncrementalProcessing() 
        {
            var chunkDifference = playerChunkID - prevPlayerChunkID;
            var absChunkDifference = chunkDifference.ElementWise(Mathf.Abs);

            ///If the absolute chunk difference is greater than 1 in any direction, 
            ///just recalculate all the targets.
            if (absChunkDifference.Any((_)=> _ > 1))
            {
                IncrementalProcessIterator = SetAllTargetsProcess();
            }
            else
            {
                ///Otherwise, a smarter approach is employed that just updates the chunks for which
                ///the target should change.
                IncrementalProcessIterator = UpdatePlayerAreaIncrementallyDifferenceOnly();
            }

            var numChunksX = MaximumActiveRadii.x * 2 + 1;
            var numChunksY = MaximumActiveRadii.y * 2 + 1;
            var numChunksZ = MaximumActiveRadii.z * 2 + 1;

            WaitingForPlayAreaUpdate = numChunksX * numChunksY * numChunksZ;

            IncrementalDone = false;
        }

        protected void ProcessChunksIncrementally() 
        {
            int processedThisUpdate = 0;

            while (IncrementalProcessIterator.MoveNext())
            {
                ++processedThisUpdate;
                --WaitingForPlayAreaUpdate;
                if (processedThisUpdate >= updateRate)
                {
                    return;
                }
            }
            //Done processing
            IncrementalDone = true;
            WaitingForPlayAreaUpdate = 0;
        }

        /// <summary>
        /// Set the target stages of all chunks around the player
        /// </summary>
        protected void UpdateWholePlayArea()
        {
            Profiler.BeginSample("UpdateWholePlayArea");

            //Do the whole update at once
            var iterator = SetAllTargetsProcess();
            while (iterator.MoveNext())
            {
            }

            Profiler.EndSample();
        }

        /// <summary>
        /// Sets the targets of ALL chunks in the radii. This is good when the world loads and the 
        /// chunks don't have a target yet, but inefficient when the player chunk changes, 
        /// as not every chunk will need to change target.
        /// </summary>
        protected IEnumerator SetAllTargetsProcess()
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

                foreach (var chunkId in CuboidalArea(playerChunkID, endRadii,startRadii))
                {
                    chunkManager.SetTargetStageOfChunk(chunkId, stage.pipelineStage);
                    yield return null;
                }
            }
        }

        /// <summary>
        /// Only sets the targets of chunks that for which the target has changed.
        /// Also deactivates chunks as necessary. This is designed to be an efficient
        /// way to keep the world up to date as the player moves.
        /// </summary>
        /// <returns></returns>
        protected IEnumerator UpdatePlayerAreaIncrementallyDifferenceOnly()
        {

            //TODO WIP
            var chunkDifference = playerChunkID - prevPlayerChunkID;
            var negativeChunkDifference = -1 * chunkDifference;
            var absChunkDifference = chunkDifference.ElementWise(Mathf.Abs);

            Assert.IsTrue(absChunkDifference.All(_ => _ <= 1),$"Difference-based incremental update" +
                $"does not support chunk differences greater than 1 in any dimension. " +
                $"Absoloute difference was {absChunkDifference}");

            //For each stage

            //Set all chunks in the negative chunk difference area around the prev chunk to the next stage
            //Set all chunks in the positive chunk difference area around the current chunk to the current stage.

            for (int i = 0; i < radiiSequence.Length; i++)
            {
                var stage = radiiSequence[i];
                ChunkStage nextStage = i + 1 < radiiSequence.Length ? radiiSequence[i + 1] : null;

                var endRadii = stage.radii;

                Vector3Int startRadii = Vector3Int.zero;

                if (i > 0)
                {
                    var prevStage = radiiSequence[i - 1];
                    startRadii = prevStage.radii + Vector3Int.one;
                }

                //No point having a radius much greater than the vertical chunk limit, as nothing can be out there
                if (worldLimits.IsWorldHeightLimited)
                {
                    endRadii.y = Mathf.Min(endRadii.y, worldLimits.HeightLimit + 1);
                }

                var adjustedStartRadii = (endRadii - absChunkDifference).ElementWise((_)=>Mathf.Max(_,0)) +Vector3Int.one;
                //adjustedStartRadii.Clamp(Vector3Int.zero, endRadii);
                //Only inspect the area that is different
                startRadii = startRadii.ElementWise((a, b) => Mathf.Max(a, b), adjustedStartRadii);

                foreach (var offset in CuboidalArea(Vector3Int.zero, endRadii, startRadii))
                {

                    if (offset == Vector3Int.zero)
                    {
                        continue;//Skip
                    }

                    ////TODO remove DEBUG
                    //var dbgChunkId = offset + playerChunkID;
                    //if (dbgChunkId.x == 1 && dbgChunkId.y == 1 && dbgChunkId.z == -1)
                    //{
                    //    Debug.Log("ShouldAdd");
                    //}

                    ////TODO remove DEBUG
                    //var dbgChunkIdPrv = offset;
                    //if (dbgChunkIdPrv.x == 1 && dbgChunkIdPrv.y == 1 && dbgChunkIdPrv.z == -1)
                    //{
                    //    Debug.Log("ShouldRemove");
                    //}

                    var dotProd = offset.Dot(chunkDifference);
                    var products = offset * chunkDifference;

                    var chunkId = offset + playerChunkID;
                    var displacementFromCurrent = offset;
                    var displacementFromPrevious = chunkId - prevPlayerChunkID;

                    bool add = false;

                    if (displacementFromCurrent.Dot(chunkDifference) >= 0)
                    {
                        add = true;
                    }

                    bool remove = false;

                    //WIP

                    //Note that both of these conditions can be true at the same time

                    if (dotProd >= 0)//This offset represents a chunk id that just entered this stage radii                    
                    {
                        Assert.IsTrue(add,"Add disagrees");
                        chunkManager.SetTargetStageOfChunk(offset + playerChunkID, stage.pipelineStage);
                    }

                    //if (dotProd <= 0)//The chunk should now be in the next stage outwards from the player
                    if (products.Any(a=>a<=0))//The chunk should now be in the next stage outwards from the player
                    {                       

                        if (nextStage != null)
                        {
                            chunkManager.SetTargetStageOfChunk(offset + prevPlayerChunkID, nextStage.pipelineStage);
                        }
                        else
                        {
                            //The next stage out from here does not exist -> deactivate the chunk
                            var chunkId = offset + prevPlayerChunkID;
                            chunkManager.TryDeactivateChunk(chunkId);
                        }
                    }

                    yield return null;
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