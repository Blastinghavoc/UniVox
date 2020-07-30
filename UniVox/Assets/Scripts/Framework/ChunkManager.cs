using PerformanceTesting;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UniVox.Framework.ChunkPipeline;
using UniVox.Framework.Common;
using UniVox.Framework.Lighting;
using UniVox.Framework.PlayAreaManagement;
using Utils.Pooling;

namespace UniVox.Framework
{
    public class ChunkManager : MonoBehaviour, IChunkManager, ITestableChunkManager
    {
        #region Shown in inspector

        #region Runtime Constants
        [SerializeField] private Vector3Int chunkDimensions = new Vector3Int(32, 32, 32);
        public Vector3Int ChunkDimensions { get => chunkDimensions; }

        [SerializeField] private int voxelSize = 1;
        protected int VoxelSize { get => voxelSize; }

        [SerializeField] protected PlayAreaManager playArea;
        public PlayAreaManager PlayArea { get => playArea; }
        public Vector3Int MaximumActiveRadii => playArea.MaximumActiveRadii;

        [SerializeField] protected WorldSizeLimits worldSizeLimits;
        public WorldSizeLimits WorldLimits { get => worldSizeLimits; }

        [SerializeField] protected bool IncludeLighting = true;
        #endregion

        public GameObject player;

        [SerializeField] protected GameObject chunkPrefab;
        protected PrefabPool chunkPool;

        /// <summary>
        /// Controls how many chunks can be generated per update
        /// </summary>
        [Range(1, 100)]
        [SerializeField] protected ushort MaxGeneratedPerUpdate = 1;

        [SerializeField] protected bool generateStructures = false;
        public bool GenerateStructures { get => generateStructures; }
        /// <summary>
        /// Controls how many chunks can have their structures generated per update
        /// </summary>
        [Range(1, 100)]
        [SerializeField] protected ushort MaxStructurePerUpdate = 1;

        /// <summary>
        /// Controls how many chunks can be meshed per update
        /// </summary>
        [Range(1, 100)]
        [SerializeField] protected ushort MaxMeshedPerUpdate = 1;

        //TODO remove DEBUG
        [SerializeField] protected bool DebugPipeline = false;

        [SerializeField] protected GameObject LightManagerGO = null;

        #endregion        

        protected VoxelTypeManager VoxelTypeManager;
        protected IChunkProvider chunkProvider;
        protected IChunkMesher chunkMesher;
        protected ILightManager lightManager;

        protected Dictionary<Vector3Int, ChunkComponent> loadedChunks;

        private ChunkPipelineManager pipeline;

        private FrameworkEventManager eventManager;

        public virtual void Initialise()
        {
            eventManager = new FrameworkEventManager();

            worldSizeLimits.Initalise();

            //Enforce positioning of ChunkManager at the world origin
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.Euler(0, 0, 0);

            loadedChunks = new Dictionary<Vector3Int, ChunkComponent>();

            chunkPool = new PrefabPool() { prefab = chunkPrefab };

            VoxelTypeManager = FindObjectOfType<VoxelTypeManager>();
            Assert.IsNotNull(VoxelTypeManager, "Chunk Manager must have a reference to a Voxel Type Manager");
            VoxelTypeManager.Initialise();

            //Initialise VoxelWorldInterface
            var worldInterface = FindObjectOfType<VoxelWorldInterface>();
            worldInterface.Intialise(this, VoxelTypeManager);

            chunkProvider = GetComponent<IChunkProvider>();
            chunkMesher = GetComponent<IChunkMesher>();
            lightManager = LightManagerGO.GetComponent<ILightManager>();
            Assert.IsNotNull(chunkProvider, "Chunk Manager must have a chunk provider component");
            Assert.IsNotNull(chunkMesher, "Chunk Manager must have a chunk mesher component");
            Assert.IsNotNull(lightManager, "Chunk Manager must have a reference to a gameobject with a light manager component");

            chunkProvider.Initialise(VoxelTypeManager, this, eventManager);
            chunkMesher.Initialise(VoxelTypeManager, this, eventManager);
            lightManager.Initialise(VoxelTypeManager, this, chunkProvider);

            var lm = IncludeLighting ? lightManager : null;

            pipeline = new ChunkPipelineManager(
                chunkProvider,
                chunkMesher,
                GetChunkComponent,
                GetPriorityOfChunk,
                MaxGeneratedPerUpdate,
                MaxMeshedPerUpdate,
                MaxMeshedPerUpdate,
                generateStructures,
                MaxStructurePerUpdate,
                lm);

            //Initialise play area manager
            var voxelPlayer = player.GetComponent<IVoxelPlayer>();
            Assert.IsNotNull(voxelPlayer, "Chunk manager must have a reference to a player with a component" +
                " that implements IVoxelPlayer");
            playArea.Initialise(this, pipeline, voxelPlayer);

        }

        protected virtual void Update()
        {

            //TODO remove DEBUG
            pipeline.DebugMode = DebugPipeline;
            if (DebugPipeline)
            {
                //Activates the pipeline debug for one frame only.
                DebugPipeline = false;
            }

            playArea.Update();
            var chunksTouchedByLightingUpdate = lightManager.Update();

            foreach (var id in chunksTouchedByLightingUpdate)
            {

                if (pipeline.GetTargetStage(id) >= pipeline.RenderedStage)
                {
                    RedoChunkFromStage(id, pipeline.FullyGeneratedStage);
                }
            }


            pipeline.Update();
        }

        /// <summary>
        /// Dispose of anything that needs to be disposed of
        /// </summary>
        private void OnDestroy()
        {
            pipeline.Dispose();
            VoxelTypeManager.Dispose();
            if (chunkMesher is IDisposable disposableChunkMesher)
            {
                disposableChunkMesher.Dispose();
            }

            if (chunkProvider is IDisposable disposableChunkProvider)
            {
                disposableChunkProvider.Dispose();
            }

            lightManager.Dispose();
        }

        public bool IsChunkComplete(Vector3Int chunkId)
        {
            return loadedChunks.ContainsKey(chunkId) && pipeline.GetMaxStage(chunkId).Equals(pipeline.CompleteStage);
        }

        public bool IsChunkFullyGenerated(Vector3Int chunkId)
        {
            return loadedChunks.ContainsKey(chunkId) && pipeline.GetMaxStage(chunkId) >= (pipeline.FullyGeneratedStage);
        }

        public Vector3Int[] GetAllLoadedChunkIds()
        {
            Vector3Int[] keys = new Vector3Int[loadedChunks.Keys.Count];
            loadedChunks.Keys.CopyTo(keys, 0);
            return keys;
        }

        private ChunkComponent GetChunkComponent(Vector3Int chunkID)
        {
            if (loadedChunks.TryGetValue(chunkID, out var chunkComponent))
            {
                return chunkComponent;
            }
            throw new Exception($"Tried to get a chunk component that for chunk ID {chunkID} that is not loaded");
        }

        public MeshDescriptor GetMeshDescriptor(Vector3Int chunkID)
        {
            var desc = GetChunkComponent(chunkID).meshDescriptor;
            if (desc == null)
            {
                throw new Exception($"Chunk with id {chunkID} does not have a valid mesh descriptor");
            }
            return desc;
        }

        /// <summary>
        /// Attempts to deactivate the chunk with given ID.
        /// Returns true if successful, false if otherwise (i.e, if the chunk was already deactivated)
        /// </summary>
        /// <param name="chunkID"></param>
        /// <returns></returns>
        public bool TryDeactivateChunk(Vector3Int chunkID)
        {
            Profiler.BeginSample("DeactivateChunk");
            bool wasPresent = false;
            if (loadedChunks.TryGetValue(chunkID, out var chunkComponent))
            {
                wasPresent = true;

                pipeline.RemoveChunk(chunkID);

                //Deal with the component's data
                if (chunkComponent.Data != null)
                {
                    if (chunkComponent.Data.ModifiedSinceGeneration)
                    {
                        //If it was modified, don't dispose of it yet, give it back to the provider
                        chunkProvider.StoreModifiedChunkData(chunkComponent.ChunkID, chunkComponent.Data);
                    }
                }

                loadedChunks.Remove(chunkID);

                //Return chunk gameobject to pool
                chunkPool.ReturnToPool(chunkComponent.gameObject);

                eventManager.FireChunkDeactivated(chunkID, playArea.playerChunkID, MaximumActiveRadii);
            }
            Profiler.EndSample();
            return wasPresent;
        }

        /// <summary>
        /// Sets the target stage of a chunk, creating it if it did not exist
        /// </summary>
        /// <param name="chunkID"></param>
        /// <param name="targetStage"></param>
        public void SetTargetStageOfChunk(Vector3Int chunkID, int targetStage, TargetUpdateMode updateMode = TargetUpdateMode.any)
        {
            Profiler.BeginSample("SetTargetStageOfChunk");
            bool outOfWorld = false;
            if (chunkID.y > WorldLimits.MaxChunkY || chunkID.y < WorldLimits.MinChunkY)
            {
                outOfWorld = true;
                var absDistanceOutisedWorld = (chunkID.y > WorldLimits.MaxChunkY) ? chunkID.y - WorldLimits.MaxChunkY
                    : WorldLimits.MinChunkY - chunkID.y;

                Assert.IsTrue(absDistanceOutisedWorld > 0);

                if (absDistanceOutisedWorld == 1)
                {
                    ///Chunks 1 chunk outside the vertical range can only be data chunks at maximum,
                    ///and will be forcibly created at this stage without the usual neighbour constraints
                    targetStage = Mathf.Min(targetStage, pipeline.FullyGeneratedStage);
                }
                else
                {
                    ///Chunks further outside the vertical range may not exist
                    Profiler.EndSample();
                    return;
                }
            }


            if (!loadedChunks.TryGetValue(chunkID, out var ChunkComponent))
            {
                Profiler.BeginSample("CreatingChunkComponent");

                //Get a new Chunk GameObject to house the generated Chunk data.
                var ChunkObject = chunkPool.Next(transform);
                ChunkComponent = ChunkObject.GetComponent<ChunkComponent>();
                ChunkComponent.Initialise(chunkID, ChunkToWorldPosition(chunkID));
                //Add to set of loaded chunks
                loadedChunks[chunkID] = ChunkComponent;

                if (outOfWorld)
                {
                    //Out of world chunks get initialised empty, always.
                    ChunkComponent.Data = new BoundaryChunkData(chunkID, chunkDimensions);
                    pipeline.AddWithData(chunkID, targetStage);
                }
                else if (chunkProvider.TryGetStoredDataForChunk(chunkID, out var data))
                {
                    //Chunks with saved data bypass the generation process.
                    ChunkComponent.Data = data;
                    pipeline.AddWithData(chunkID, targetStage, IncludeLighting);//Add with voxel data, but re-generate light data
                }
                else
                {
                    //Add the new chunk to the pipeline for data generation
                    pipeline.Add(chunkID, targetStage);
                }

                eventManager.FireChunkActivated(chunkID);

                Profiler.EndSample();
                Profiler.EndSample();
                return;
            }

            pipeline.SetTarget(chunkID, targetStage, updateMode);
            Profiler.EndSample();
        }

        /// <summary>
        /// Cause the chunk with the given ID to be re-enterred into the pipeline at an
        /// earlier stage.
        /// </summary>
        /// <param name="chunkID"></param>
        /// <param name="stage"></param>
        protected void RedoChunkFromStage(Vector3Int chunkID, int stage)
        {
            pipeline.ReenterAtStage(chunkID, stage);
        }

        /// <summary>
        /// Manhattan distance query returning true of the chunk ID is 
        /// within the given radii of the player chunk in each axis
        /// </summary>
        /// <param name="chunkID"></param>
        /// <returns></returns>
        public bool InsideChunkRadius(Vector3Int chunkID, Vector3Int Radii)
        {
            return playArea.InsideChunkRadius(chunkID, Radii);
        }

        /// <summary>
        /// Get a priority for a chunk, that is equal to the manhattan distance 
        /// from the player to the chunk
        /// </summary>
        /// <param name="chunkID"></param>
        /// <returns></returns>
        private float GetPriorityOfChunk(Vector3Int chunkID)
        {
            var absDisplacement = (playArea.playerChunkID - chunkID).ElementWise(Mathf.Abs);
            return absDisplacement.x + absDisplacement.y + absDisplacement.z;
        }

        #region Get/Set voxels

        /// <summary>
        /// What to do when a voxel value is set
        /// </summary>
        /// <param name="previousTypeID"></param>
        /// <param name="newTypeID"></param>
        /// <param name="chunkComponent"></param>
        /// <param name="localVoxelIndex"></param>
        protected void OnVoxelSet(VoxelTypeID previousTypeID, VoxelTypeID newTypeID, ChunkComponent chunkComponent, Vector3Int localVoxelIndex)
        {
            if (previousTypeID == newTypeID)
            {
                return;//Nothing has changed
            }

            var chunkID = chunkComponent.ChunkID;

            //The neighbouring chunk(s) may need remeshing
            if (chunkMesher.IsMeshDependentOnNeighbourChunks)
            {
                foreach (var dir in GetBordersVoxelIsOn(localVoxelIndex))
                {
                    var neighbourChunkID = chunkID + dir;
                    if (loadedChunks.TryGetValue(neighbourChunkID, out var neighbourComponent))
                    {

                        if (pipeline.GetTargetStage(neighbourChunkID) >= pipeline.RenderedStage)
                        {
                            //The neighbour chunk will need remeshing
                            RedoChunkFromStage(neighbourChunkID, pipeline.FullyGeneratedStage);
                        }
                    }
                }
            }

            if (IncludeLighting)
            {
                var chunksTouchedByLightingUpdate = lightManager.UpdateLightOnVoxelSet(new ChunkNeighbourhood(chunkID, GetChunkData),
                    localVoxelIndex, newTypeID, previousTypeID);

                foreach (var id in chunksTouchedByLightingUpdate)
                {
                    if (pipeline.GetTargetStage(id) >= pipeline.RenderedStage)
                    {
                        RedoChunkFromStage(id, pipeline.FullyGeneratedStage);
                    }
                }
            }

            if (pipeline.GetTargetStage(chunkID) >= pipeline.RenderedStage)
            {
                //The chunk that changed will need remeshing if its target stage has a mesh
                RedoChunkFromStage(chunkID, pipeline.FullyGeneratedStage);
            }
        }

        /// <summary>
        /// Returns a list of directions such that the given voxel index 
        /// shares a face with another chunk in that direction.
        /// List is empty for indices that are not on the edge of the chunk
        /// </summary>
        /// <param name="localVoxelIndex"></param>
        /// <returns></returns>
        private List<Vector3Int> GetBordersVoxelIsOn(Vector3Int localVoxelIndex)
        {
            List<Vector3Int> borderDirections = new List<Vector3Int>();

            //If voxel is an interior voxel, it is not on any borders
            if (localVoxelIndex.All((a, b) => a > 0 && a < b - 1, ChunkDimensions))
            {
                return borderDirections;
            }

            for (int i = 0; i < DirectionExtensions.numDirections; i++)
            {
                var dir = DirectionExtensions.Vectors[i];
                var adjacentIndex = localVoxelIndex + dir;
                if (adjacentIndex.Any((a, b) => a < 0 || a >= b, ChunkDimensions))
                {
                    /* If any of the adjacent index components are outside the chunk dimensions,
                     * then the voxel is on the border in the current direction
                    */
                    borderDirections.Add(dir);
                }
            }

            return borderDirections;
        }

        public RestrictedChunkData GetReadOnlyChunkData(Vector3Int chunkID)
        {
            if (loadedChunks.TryGetValue(chunkID, out var chunkComponent))
            {
                if (chunkComponent.Data == null)
                {
                    throw new ArgumentException($"It is not valid to get read-only data for chunk ID {chunkID}, as its data is null");
                }
                return new RestrictedChunkData(chunkComponent.Data);
            }
            throw new ArgumentException($"It is not valid to get read-only data for chunk ID {chunkID}, as it is not in the loaded chunks." +
                $" Did the pipeline contain the chunk? {pipeline.Contains(chunkID)}");
        }

        public IChunkData GetChunkData(Vector3Int chunkId)
        {
            if (loadedChunks.TryGetValue(chunkId, out var chunkComponent))
            {
                if (chunkComponent.Data == null)
                {
                    throw new ArgumentException($"It is not valid to get data for chunk ID {chunkId}, as its data is null");
                }
                return chunkComponent.Data;
            }
            throw new ArgumentException($"It is not valid to get data for chunk ID {chunkId}, as it is not in the loaded chunks." +
                $" Did the pipeline contain the chunk? {pipeline.Contains(chunkId)}");
        }

        public bool TrySetVoxel(Vector3 worldPos, VoxelTypeID voxelTypeID, VoxelRotation voxelRotation = default, bool overrideExisting = false)
        {
            Vector3Int localVoxelIndex;
            var chunkID = WorldToChunkPosition(worldPos, out localVoxelIndex);

            if (loadedChunks.TryGetValue(chunkID, out var chunkComponent))
            {
                if (pipeline.GetMaxStage(chunkID) < pipeline.CompleteStage)
                {
                    //Disallow edits to incomplete chunks (those without collision meshes)
                    return false;
                }

                var newVoxID = new VoxelTypeID();
                newVoxID = voxelTypeID;

                VoxelTypeID prevID = chunkComponent.Data[localVoxelIndex];

                if (!overrideExisting)
                {
                    //Disallow setting voxel if one already exists
                    if (prevID != (VoxelTypeID)VoxelTypeID.AIR_ID)
                    {
                        return false;
                    }
                }

                chunkComponent.Data[localVoxelIndex] = newVoxID;
                chunkComponent.Data.SetRotation(localVoxelIndex, voxelRotation);

                OnVoxelSet(prevID, voxelTypeID, chunkComponent, localVoxelIndex);

                return true;
            }
            return false;
        }

        public bool TryGetVoxel(Vector3 worldPos, out VoxelTypeID voxelTypeID)
        {
            Vector3Int localVoxelIndex;
            var chunkID = WorldToChunkPosition(worldPos, out localVoxelIndex);

            return TryGetVoxel(chunkID, localVoxelIndex, out voxelTypeID);
        }

        public bool TryGetVoxel(Vector3Int chunkID, Vector3Int localVoxelIndex, out VoxelTypeID voxelTypeID)
        {
            voxelTypeID = (VoxelTypeID)VoxelTypeID.AIR_ID;

            if (loadedChunks.TryGetValue(chunkID, out var chunkComponent))
            {
                if (!pipeline.ChunkDataReadable(chunkID))
                {
                    //Data is not valid to be read
                    return false;
                }

                voxelTypeID = chunkComponent.Data[localVoxelIndex];

                return true;
            }
            return false;
        }

        public bool TryGetLightLevel(Vector3 worldPos, out LightValue lightValue)
        {
            Vector3Int localVoxelIndex;
            var chunkID = WorldToChunkPosition(worldPos, out localVoxelIndex);
            lightValue = default;

            if (loadedChunks.TryGetValue(chunkID, out var chunkComponent))
            {
                if (!pipeline.ChunkDataReadable(chunkID))
                {
                    //Data is not valid to be read
                    return false;
                }

                lightValue = chunkComponent.Data.GetLight(localVoxelIndex);

                return true;
            }
            return false;
        }

        #endregion

        #region position conversion methods
        public Vector3Int WorldToChunkPosition(Vector3 pos)
        {
            //ChunkManager must be located at world origin
            Assert.AreEqual(Vector3.zero, transform.position);
            Assert.AreEqual(Quaternion.Euler(0, 0, 0), transform.rotation);

            Vector3Int floor = new Vector3Int();
            floor.x = Mathf.FloorToInt(pos.x);
            floor.y = Mathf.FloorToInt(pos.y);
            floor.z = Mathf.FloorToInt(pos.z);

            //Result is elementwise integer division by the Chunk dimensions
            var result = floor.ElementWise((a, b) => Mathf.FloorToInt(a / (float)b), ChunkDimensions);

            return result;
        }

        protected Vector3Int WorldToChunkPosition(Vector3 pos, out Vector3Int localVoxelIndex)
        {
            //ChunkManager must be located at world origin
            Assert.AreEqual(Vector3.zero, transform.position);
            Assert.AreEqual(Quaternion.Euler(0, 0, 0), transform.rotation);

            //TODO replace body with call to Utils.Helpers.AdjustForBounds ?

            Vector3Int floor = new Vector3Int();
            floor.x = Mathf.FloorToInt(pos.x);
            floor.y = Mathf.FloorToInt(pos.y);
            floor.z = Mathf.FloorToInt(pos.z);

            //Result is elementwise integer division by the Chunk dimensions
            var result = floor.ElementWise((a, b) => Mathf.FloorToInt(a / (float)b), ChunkDimensions);

            localVoxelIndex = Utils.Helpers.ModuloChunkDimensions(floor, chunkDimensions);

            return result;
        }

        public Vector3 ChunkToWorldPosition(Vector3Int chunkID)
        {
            //ChunkManager must be located at world origin
            Assert.AreEqual(Vector3.zero, transform.position);
            Assert.AreEqual(Quaternion.Euler(0, 0, 0), transform.rotation);

            Vector3 result = chunkID * ChunkDimensions;
            return result;
        }

        public Vector3 SnapToVoxelCenter(Vector3 pos)
        {
            var result = pos.ElementWise(_ => Mathf.Floor(_) + VoxelSize / 2.0f);
            return result;
        }
        #endregion

        #region Test and debug facilitating methods


        public bool PipelineIsSettled()
        {
            return pipeline.IsSettled();
        }

        public IVoxelPlayer GetPlayer()
        {
            return playArea.Player;
        }

        public string GetPipelineStatus()
        {
            return pipeline.GetPipelineStatus();
        }

        public Tuple<bool, bool> ContainsChunkID(Vector3Int chunkID)
        {
            return new Tuple<bool, bool>(loadedChunks.ContainsKey(chunkID), pipeline.Contains(chunkID));
        }

        public int GetMinPipelineStageOfChunk(Vector3Int chunkId)
        {
            return pipeline.GetMinStage(chunkId);
        }

        public string GetMinPipelineStageOfChunkByName(Vector3Int chunkId)
        {
            return pipeline.GetStage(pipeline.GetMinStage(chunkId)).Name;
        }
        #endregion
    }
}