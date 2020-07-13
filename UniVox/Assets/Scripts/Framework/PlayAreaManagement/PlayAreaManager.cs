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

        protected bool IncrementalDone;
        protected IEnumerator IncrementalProcessIterator;

        #endregion

        //Should the world height be limited (like minecraft)
        [SerializeField] protected bool limitWorldHeight;
        //Vertical chunk limit of 8 -> max chunkid Y coordinate is 7, min -8
        [SerializeField] protected int verticalChunkLimit;
        public bool IsWorldHeightLimited { get => limitWorldHeight; }
        public int MaxChunkY { get; protected set; }
        public int MinChunkY { get; protected set; }

        [SerializeField] public Rigidbody Player;

        /// <summary>
        /// Controls how many chunks are processed per update 
        /// when the play area changes.
        /// </summary>
        [Range(1, 1000)]
        [SerializeField] protected ushort UpdateRate = 1;
        public int WaitingForPlayAreaUpdate { get; private set; }//DEBUG

        //Current chunkID occupied by the Player
        public Vector3Int playerChunkID { get; protected set; }
        public Vector3Int prevPlayerChunkID { get; protected set; }        

        protected ChunkManager chunkManager;
        protected ChunkPipelineManager pipeline;

        public void Initialise(ChunkManager chunkManager, ChunkPipelineManager pipeline)
        {
            this.chunkManager = chunkManager;
            this.pipeline = pipeline;

            Assert.IsTrue(RenderedChunksRadii.All((a, b) => a >= b, CollidableChunksRadii),
                "The rendering radii must be at least as large as the collidable radii");

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

            //Set up world height limits
            MaxChunkY = limitWorldHeight ? verticalChunkLimit - 1 : int.MaxValue;
            MinChunkY = limitWorldHeight ? -verticalChunkLimit : int.MinValue;

            //Set up player
            Player.position = new Vector3(5, 17, 5);
            Player.velocity = Vector3.zero;

            playerChunkID = chunkManager.WorldToChunkPosition(Player.position);

            //Immediately request generation of the chunk the player is in
            chunkManager.SetTargetStageOfChunk(playerChunkID, pipeline.CompleteStage);

            UpdateWholePlayArea();
        }


        public void Update()
        {
            prevPlayerChunkID = playerChunkID;
            playerChunkID = chunkManager.WorldToChunkPosition(Player.position);

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
                if (playerChunkID.y > MinChunkY && playerChunkID.y < MaxChunkY)
                {
                    //Freeze player if the chunk isn't ready for them (doesn't exist or doesn't have collision mesh)
                    //But only if the chunk is within the world limits
                    Player.constraints |= RigidbodyConstraints.FreezePosition;
                }
            }
            else
            {
                //Remove constraints
                Player.constraints &= ~RigidbodyConstraints.FreezePosition;
            }
        }

        protected void RestartIncrementalProcessing() 
        {
            IncrementalProcessIterator = UpdatePlayerAreaIncrementallyDifferenceOnly();

            var numChunksX = MaximumActiveRadii.x * 2 + 1;
            var numChunksY = MaximumActiveRadii.y * 2 + 1;
            var numChunksZ = MaximumActiveRadii.z * 2 + 1;

            WaitingForPlayAreaUpdate = numChunksX * numChunksY * numChunksZ;
        }

        protected void ProcessChunksIncrementally() 
        {
            int processedThisUpdate = 0;

            while (IncrementalProcessIterator.MoveNext())
            {
                ++processedThisUpdate;
                --WaitingForPlayAreaUpdate;
                if (processedThisUpdate >= UpdateRate)
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

            //Start with collidable chunks
            foreach (var chunkId in CuboidalArea(playerChunkID, CollidableChunksRadii))
            {
                chunkManager.SetTargetStageOfChunk(chunkId, pipeline.CompleteStage);//Request that this chunk should be complete
                yield return null;
            }

            //Then rendered chunks
            foreach (var chunkId in CuboidalArea(playerChunkID, RenderedChunksRadii, CollidableChunksRadii + Vector3Int.one))
            {
                chunkManager.SetTargetStageOfChunk(chunkId, pipeline.RenderedStage);//Request that this chunk should be rendered
                yield return null;
            }

            //Then fully generated
            foreach (var chunkId in CuboidalArea(playerChunkID, FullyGeneratedRadii, RenderedChunksRadii + Vector3Int.one))
            {
                //These chunks should be fully generated including structures from other chunks
                chunkManager.SetTargetStageOfChunk(chunkId, pipeline.FullyGeneratedStage);
                yield return null;
            }

            //Then own structures
            foreach (var chunkId in CuboidalArea(playerChunkID, StructureChunksRadii, FullyGeneratedRadii + Vector3Int.one))
            {
                //These chunks should have generated their own structures.
                chunkManager.SetTargetStageOfChunk(chunkId, pipeline.OwnStructuresStage);
                yield return null;
            }

            //Then just terrain data, no structures
            foreach (var chunkId in CuboidalArea(playerChunkID, MaximumActiveRadii, StructureChunksRadii + Vector3Int.one))
            {
                //Request that this chunk should be just terrain data, no structures
                chunkManager.SetTargetStageOfChunk(chunkId, pipeline.TerrainDataStage);
                yield return null;
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
                if (limitWorldHeight)
                {
                    endRadii.y = Mathf.Min(endRadii.y, verticalChunkLimit + 1);
                }

                //Only inspect the area that is different
                startRadii = startRadii.ElementWise((a, b) => Mathf.Max(a, b), endRadii - absChunkDifference);

                foreach (var offset in CuboidalArea(Vector3Int.zero, endRadii, startRadii))
                {

                    if (offset.All((a, b) => SameSign(a, b), chunkDifference))
                    {
                        //This offset represents a chunk id that just entered this stage radii
                        chunkManager.SetTargetStageOfChunk(offset + playerChunkID, stage.pipelineStage);
                    }
                    else
                    {
                        //The chunk should now be in the next stage outwards from the player

                        if (nextStage != null)
                        {
                            chunkManager.SetTargetStageOfChunk(offset + prevPlayerChunkID, nextStage.pipelineStage);
                        }
                        else
                        {
                            //The next stage out from here does not exist -> deactivate the chunk
                            var chunkId = offset + prevPlayerChunkID;                            
                            chunkManager.TryDeactivateChunk(offset + prevPlayerChunkID);                            
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