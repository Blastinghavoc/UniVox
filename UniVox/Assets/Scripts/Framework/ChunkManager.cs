using PerformanceTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UniVox.Framework;
using UniVox.Framework.ChunkPipeline;
using UniVox.Framework.Common;
using Utils.Pooling;
using static Utils.Helpers;

public class ChunkManager : MonoBehaviour, IChunkManager, ITestableChunkManager
{
    #region Shown in inspector

    #region Runtime Constants
    [SerializeField] private Vector3Int chunkDimensions = new Vector3Int(32, 32, 32);
    public Vector3Int ChunkDimensions { get => chunkDimensions; }

    [SerializeField] private int voxelSize = 1;
    protected int VoxelSize { get => voxelSize; }

    [SerializeField] protected Vector3Int collidableChunksRadii;
    [SerializeField] protected Vector3Int renderedChunksRadii;
    protected Vector3Int fullyGeneratedRadii;
    protected Vector3Int structureChunksRadii;
    public Vector3Int MaximumActiveRadii { get; private set; }

    //Should the world height be limited (like minecraft)
    [SerializeField] protected bool limitWorldHeight;
    //Vertical chunk limit of 8 -> max chunkid Y coordinate is 7, min -8
    [SerializeField] protected int verticalChunkLimit;
    public bool IsWorldHeightLimited { get => limitWorldHeight; }
    public int MaxChunkY { get { return (limitWorldHeight) ? verticalChunkLimit - 1 : int.MaxValue; } }
    public int MinChunkY { get { return (limitWorldHeight) ? -verticalChunkLimit : int.MinValue; } }

    #endregion


    [SerializeField] protected GameObject chunkPrefab;
    protected PrefabPool chunkPool;

    [SerializeField] protected Rigidbody Player;


    /// <summary>
    /// Controls how many chunks can be generated per update
    /// </summary>
    [Range(1, 100)]
    [SerializeField] protected ushort MaxGeneratedPerUpdate = 1;

    [SerializeField] protected bool GenerateStructures = false;
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

    #endregion


    protected VoxelTypeManager VoxelTypeManager;
    protected IChunkProvider chunkProvider { get; set; }
    protected IChunkMesher chunkMesher { get; set; }

    protected Dictionary<Vector3Int, ChunkComponent> loadedChunks;

    //Current chunkID occupied by the Player
    protected Vector3Int playerChunkID;
    protected Vector3Int prevPlayerChunkID;

    private ChunkPipelineManager pipeline;

    private FrameworkEventManager eventManager;

    public virtual void Initialise()
    {
        Assert.IsTrue(renderedChunksRadii.All((a, b) => a >= b, collidableChunksRadii),
            "The rendering radii must be at least as large as the collidable radii");

        //Chunks can exist as just data one chunk further away than the rendered chunks
        fullyGeneratedRadii = renderedChunksRadii + new Vector3Int(1, 1, 1);
        MaximumActiveRadii = fullyGeneratedRadii;
        if (GenerateStructures)
        {
            structureChunksRadii = fullyGeneratedRadii + new Vector3Int(1, 1, 1);
            //Extra radius for just terrain data.
            MaximumActiveRadii = structureChunksRadii + new Vector3Int(1, 1, 1);
        }

        eventManager = new FrameworkEventManager();

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
        Assert.IsNotNull(chunkProvider, "Chunk Manager must have a chunk provider component");
        Assert.IsNotNull(chunkMesher, "Chunk Manager must have a chunk mesher component");

        chunkProvider.Initialise(VoxelTypeManager, this,eventManager);
        chunkMesher.Initialise(VoxelTypeManager, this,eventManager);

        pipeline = new ChunkPipelineManager(
            chunkProvider,
            chunkMesher,
            GetChunkComponent,
            GetPriorityOfChunk,
            MaxGeneratedPerUpdate,
            MaxMeshedPerUpdate,
            MaxMeshedPerUpdate,
            GenerateStructures,
            MaxStructurePerUpdate);

        Player.position = new Vector3(5, 17, 5);
        Player.velocity = Vector3.zero;

        playerChunkID = WorldToChunkPosition(Player.position);
        //Immediately request generation of the chunk the player is in
        SetTargetStageOfChunk(playerChunkID, pipeline.CompleteStage);
        UpdatePlayerArea();
    }

    protected virtual void Update()
    {
        prevPlayerChunkID = playerChunkID;
        playerChunkID = WorldToChunkPosition(Player.position);

        if (playerChunkID != prevPlayerChunkID)
        {
            UpdatePlayerArea();
        }

        //TODO remove DEBUG
        pipeline.DebugMode = DebugPipeline;
        if (DebugPipeline)
        {
            //Activates the pipeline debug for one frame only.
            DebugPipeline = false;
        }

        pipeline.Update();

        if (!loadedChunks.TryGetValue(playerChunkID, out var chunkComponent) ||
            !pipeline.GetMaxStage(playerChunkID).Equals(pipeline.CompleteStage))
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

    }

    /// <summary>
    /// Load chunks around the player, unload those that are too far.
    /// </summary>
    protected void UpdatePlayerArea()
    {
        Profiler.BeginSample("UpdatePlayArea");

        //Deactivate any chunks that are outside the maximum radius
        Profiler.BeginSample("DetectAndDeactivateChunks");
        var deactivate = loadedChunks.Select((pair) => pair.Key)
            .Where((id) => !InsideChunkRadius(id, MaximumActiveRadii))
            .ToList();

        deactivate.ForEach(DeactivateChunk);
        Profiler.EndSample();

        //for (int x = -MaximumActiveRadii.x; x <= MaximumActiveRadii.x; x++)
        //{
        //    for (int y = -MaximumActiveRadii.y; y <= MaximumActiveRadii.y; y++)
        //    {
        //        for (int z = -MaximumActiveRadii.z; z <= MaximumActiveRadii.z; z++)
        //        {
        //            var chunkID = playerChunkID + new Vector3Int(x, y, z);

        //            if (InsideChunkRadius(chunkID, collidableChunksRadii))
        //            {
        //                SetTargetStageOfChunk(chunkID, pipeline.CompleteStage);//Request that this chunk should be complete
        //            }
        //            else if (InsideChunkRadius(chunkID, renderedChunksRadii))
        //            {
        //                SetTargetStageOfChunk(chunkID, pipeline.RenderedStage);//Request that this chunk should be rendered
        //            }
        //            else if (InsideChunkRadius(chunkID, fullyGeneratedRadii))
        //            {
        //                //This chunk should be fully generated including structures
        //                SetTargetStageOfChunk(chunkID, pipeline.FullyGeneratedStage);
        //            }
        //            else if(InsideChunkRadius(chunkID,structureChunksRadii))
        //            {
        //                SetTargetStageOfChunk(chunkID, pipeline.OwnStructuresStage);
        //            }
        //            else
        //            {
        //                //Request that this chunk should be just terrain data, no structures
        //                SetTargetStageOfChunk(chunkID, pipeline.TerrainDataStage);
        //            }

        //        }
        //    }
        //}
        UpdatePlayerAreaIncrementally();
        Profiler.EndSample();
    }

    /// <summary>
    /// Rather than doing all the chunk target changes in one frame, this spreads the load out over multiple frames
    /// </summary>
    protected void UpdatePlayerAreaIncrementally() 
    {
        //TODO deactivate chunks
        //Update chunks nearest to farthest
        //Start with collidable chunks
        foreach (var chunkId in CuboidalArea(playerChunkID,collidableChunksRadii))
        {
            SetTargetStageOfChunk(chunkId, pipeline.CompleteStage);//Request that this chunk should be complete
        }

        //Then rendered chunks
        foreach (var chunkId in CuboidalArea(playerChunkID, renderedChunksRadii, collidableChunksRadii + Vector3Int.one))
        {
            SetTargetStageOfChunk(chunkId, pipeline.RenderedStage);//Request that this chunk should be rendered
        }

        //Then fully generated
        foreach (var chunkId in CuboidalArea(playerChunkID, fullyGeneratedRadii, renderedChunksRadii + Vector3Int.one))        
        {
            //These chunks should be fully generated including structures from other chunks
            SetTargetStageOfChunk(chunkId, pipeline.FullyGeneratedStage);
        }

        //Then own structures
        foreach (var chunkId in CuboidalArea(playerChunkID, structureChunksRadii, fullyGeneratedRadii + Vector3Int.one))
        {
            //These chunks should have generated their own structures.
            SetTargetStageOfChunk(chunkId, pipeline.OwnStructuresStage);
        }

        //Then just terrain data, no structures
        foreach (var chunkId in CuboidalArea(playerChunkID, MaximumActiveRadii, structureChunksRadii + Vector3Int.one))
        {
            //Request that this chunk should be just terrain data, no structures
            SetTargetStageOfChunk(chunkId, pipeline.TerrainDataStage);
        }
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

    protected void DeactivateChunk(Vector3Int chunkID)
    {
        Profiler.BeginSample("DeactivateChunk");
        if (loadedChunks.TryGetValue(chunkID, out var chunkComponent))
        {
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

            eventManager.FireChunkDeactivated(chunkID, playerChunkID, MaximumActiveRadii);
        }
        else
        {
            throw new ArgumentException($"Cannot deactivate chunk {chunkID} as it is already inactive or nonexistent");
        }
        Profiler.EndSample();
    }

    /// <summary>
    /// Sets the target stage of a chunk, creating it if it did not exist
    /// </summary>
    /// <param name="chunkID"></param>
    /// <param name="targetStage"></param>
    protected void SetTargetStageOfChunk(Vector3Int chunkID, int targetStage)
    {
        Profiler.BeginSample("SetTargetStageOfChunk");
        bool outOfWorld = false;
        if (chunkID.y > MaxChunkY || chunkID.y < MinChunkY)
        {
            outOfWorld = true;
            var absDistanceOutisedWorld = (chunkID.y > MaxChunkY) ? chunkID.y - MaxChunkY : MinChunkY - chunkID.y;

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
            else if (chunkProvider.TryGetStoredDataForChunk(chunkID,out var data))
            {
                //Chunks with saved data bypass the generation process.
                ChunkComponent.Data = data;
                pipeline.AddWithData(chunkID, targetStage);
            }
            else
            {
                //Add the new chunk to the pipeline for data generation
                pipeline.Add(chunkID, targetStage);
            }

            Profiler.EndSample();
            Profiler.EndSample();
            return;
        }

        pipeline.SetTarget(chunkID, targetStage);
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
        var displacement = playerChunkID - chunkID;
        var absDisplacement = displacement.ElementWise(Mathf.Abs);

        //Inside if all elements of the absolute displacement are less than or equal to the chunk radius
        return absDisplacement.All((a, b) => a <= b, Radii);
    }

    /// <summary>
    /// Get a priority for a chunk, that is equal to the manhattan distance 
    /// from the player to the chunk
    /// </summary>
    /// <param name="chunkID"></param>
    /// <returns></returns>
    private float GetPriorityOfChunk(Vector3Int chunkID)
    {
        var absDisplacement = (playerChunkID - chunkID).ElementWise(Mathf.Abs);
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

    public ReadOnlyChunkData GetReadOnlyChunkData(Vector3Int chunkID)
    {
        if (loadedChunks.TryGetValue(chunkID, out var chunkComponent))
        {
            if (chunkComponent.Data == null)
            {
                throw new ArgumentException($"It is not valid to get read-only data for chunk ID {chunkID}, as its data is null");
            }
            return new ReadOnlyChunkData(chunkComponent.Data);
        }
        throw new ArgumentException($"It is not valid to get read-only data for chunk ID {chunkID}, as it is not in the loaded chunks." +
            $" Did the pipeline contain the chunk? {pipeline.Contains(chunkID)}");
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
                if (prevID != (VoxelTypeID)VoxelTypeManager.AIR_ID)
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
        voxelTypeID = (VoxelTypeID)VoxelTypeManager.AIR_ID;

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

        Vector3Int floor = new Vector3Int();
        floor.x = Mathf.FloorToInt(pos.x);
        floor.y = Mathf.FloorToInt(pos.y);
        floor.z = Mathf.FloorToInt(pos.z);

        //Result is elementwise integer division by the Chunk dimensions
        var result = floor.ElementWise((a, b) => Mathf.FloorToInt(a / (float)b), ChunkDimensions);

        localVoxelIndex = LocalVoxelIndexOfPosition(floor);

        return result;
    }

    /// <summary>
    /// Returns the local voxel index of some position, but not the chunk ID
    /// of the chunk containing that position. Intended to be used when you 
    /// already know the chunkID containing the position.
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public Vector3Int LocalVoxelIndexOfPosition(Vector3Int position)
    {
        var remainder = position.ElementWise((a, b) => a % b, ChunkDimensions);
        //Local voxel index is the remainder, with negatives adjusted
        return remainder.ElementWise((a, b) => a < 0 ? b + a : a, ChunkDimensions);
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

    public Rigidbody GetPlayer()
    {
        return Player;
    }

    public string GetPipelineStatus()
    {
        return pipeline.GetPipelineStatus();
    }

    public Tuple<bool, bool> ContainsChunkID(Vector3Int chunkID)
    {
        return new Tuple<bool, bool>(loadedChunks.ContainsKey(chunkID), pipeline.Contains(chunkID));
    }

    #endregion
}
