using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UniVox.Framework;
using Utils.FSM;
using Utils.Pooling;

public abstract class AbstractChunkManager<ChunkDataType, VoxelDataType> : MonoBehaviour, IChunkManager 
    where ChunkDataType : IChunkData<VoxelDataType>
    where VoxelDataType : IVoxelData
{
    #region Shown in inspector

    #region Runtime Constants
    [SerializeField] private Vector3Int chunkDimensions = new Vector3Int(32, 32, 32);
    public Vector3Int ChunkDimensions { get => chunkDimensions; }

    [SerializeField] private int voxelSize = 1;
    protected int VoxelSize { get => voxelSize; }
    #endregion

    [SerializeField] protected VoxelTypeManager VoxelTypeManager;

    [SerializeField] protected PrefabPool chunkPool = null;

    [SerializeField] protected Rigidbody Player;

    [Range(0, 100)]
    [SerializeField] private int chunkRadiusX = 1;
    [Range(0, 100)]
    [SerializeField] private int chunkRadiusY = 1;
    [Range(0, 100)]
    [SerializeField] private int chunkRadiusZ = 1;
    protected Vector3Int meshedChunksRadii;
    protected Vector3Int dataChunksRadii;

    /// <summary>
    /// Controls how many chunks can be generated and meshed per update
    /// </summary>
    [Range(1, 100)]
    [SerializeField] protected ushort MaxGeneratedPerUpdate = 1;

    /// <summary>
    /// Controls how many chunks can be generated and meshed per update
    /// </summary>
    [Range(1, 100)]
    [SerializeField] protected ushort MaxMeshedPerUpdate = 1;
    #endregion


    protected AbstractProviderComponent<ChunkDataType, VoxelDataType> chunkProvider;
    protected AbstractMesherComponent<ChunkDataType, VoxelDataType> chunkMesher;

    protected Dictionary<Vector3Int, AbstractChunkComponent<ChunkDataType, VoxelDataType>> loadedChunks = new Dictionary<Vector3Int, AbstractChunkComponent<ChunkDataType, VoxelDataType>>();

    //Current chunkID occupied by the Player transform
    protected Vector3Int playerChunkID = Vector3Int.zero;

    private Queue<AbstractChunkComponent<ChunkDataType, VoxelDataType>> NeedDataQueue = new Queue<AbstractChunkComponent<ChunkDataType, VoxelDataType>>();

    private Queue<AbstractChunkComponent<ChunkDataType, VoxelDataType>> NeedMeshingQueue = new Queue<AbstractChunkComponent<ChunkDataType, VoxelDataType>>();

    protected virtual void Start()
    {
        meshedChunksRadii = new Vector3Int(chunkRadiusX,chunkRadiusY,chunkRadiusZ);
        //Chunks can exist as just data one chunk further away than the meshed chunks
        dataChunksRadii = meshedChunksRadii + new Vector3Int(1, 1, 1);

        //SetupChunkStatePipeline();

        //Enforce positioning of ChunkManager at the world origin
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.Euler(0, 0, 0);

        Assert.IsNotNull(VoxelTypeManager,"Chunk Manager must have a reference to a Voxel Type Manager");
        VoxelTypeManager.Initialise();

        //Create VoxelWorldInterface
        var worldInterface = gameObject.AddComponent<VoxelWorldInterface>();
        worldInterface.Intialise(this,VoxelTypeManager);

        chunkProvider = GetComponent<AbstractProviderComponent<ChunkDataType, VoxelDataType>>();
        chunkMesher = GetComponent<AbstractMesherComponent<ChunkDataType, VoxelDataType>>();
        Assert.IsNotNull(chunkProvider,"Chunk Manager must have a chunk provider component");
        Assert.IsNotNull(chunkMesher, "Chunk Manager must have a chunk mesher component");

        chunkProvider.Initialise(VoxelTypeManager);
        chunkMesher.Initialise(VoxelTypeManager);

        //Immediately request generation of the chunk the player is in
        RequestRegenerationOfChunk(WorldToChunkPosition(Player.position));

        StartCoroutine(ProcessQueues());
    }

    protected virtual void Update()
    {
        UpdatePlayerArea();
        if (!loadedChunks.TryGetValue(playerChunkID,out var chunkComponent) || chunkComponent.Status != ChunkStatus.Complete)
        {
            //Freeze player if the chunk isn't ready for them
            Player.velocity = Vector3.zero;
        }
    }

    /// <summary>
    /// Load chunks around the player, unload those that are too far.
    /// </summary>
    protected void UpdatePlayerArea()
    {
        playerChunkID = WorldToChunkPosition(Player.position);

        //Find all chunks outside the radius
        var outside = loadedChunks.Where(pair => !InsideChunkRadius(pair.Key,dataChunksRadii))
            .Select(pair => pair.Value)
            .ToList();

        //Deactivate all chunks outside the radius
        outside.ForEach(_ => TryToDeactivate(_));

        for (int z = -meshedChunksRadii.z; z <= meshedChunksRadii.z; z++)
        {
            for (int y = -meshedChunksRadii.y; y <= meshedChunksRadii.y; y++)
            {
                for (int x = -meshedChunksRadii.x; x <= meshedChunksRadii.x; x++)
                {
                    var chunkID = playerChunkID + new Vector3Int(x, y, z);

                    RequestRegenerationOfChunk(chunkID);//Request that this chunk should exist
                }
            }
        }
    }

    /// <summary>
    /// Each frame generates data and/or meshes for any Chunk Components 
    /// that need it, up to a limit of MaxChunksPerUpdate.
    /// Always generates data first, then does meshing, so that a single
    /// chunk can receive data and a mesh in the same update.
    /// </summary>
    /// <returns></returns>
    private IEnumerator ProcessQueues() 
    {
        yield return new WaitForEndOfFrame();
        while (gameObject.activeSelf)
        {
            for (int i = 0; i < MaxGeneratedPerUpdate && NeedDataQueue.Count > 0; i++)
            {
                var ChunkComponent = NeedDataQueue.Dequeue();

                Assert.AreEqual(ChunkStatus.ScheduledForData, ChunkComponent.Status,
                    $"Chunk {ChunkComponent.ChunkID} was in the Need Data Queue without being Scheduled For Data");

                //Generate data
                ChunkComponent.Data = chunkProvider.ProvideChunkData(ChunkComponent.ChunkID, ChunkDimensions);

                ChunkComponent.Status = ChunkStatus.WaitingForNeighbourData;
                NextStep(ChunkComponent);
            }

            for (int i = 0; i < MaxMeshedPerUpdate && NeedMeshingQueue.Count > 0; i++)
            {
                var ChunkComponent = NeedMeshingQueue.Dequeue();

                Assert.IsTrue(InsideChunkRadius(ChunkComponent.ChunkID, meshedChunksRadii),
                    $"Chunk {ChunkComponent.ChunkID} was in the mesh queue but is outside the mesh radius");

                //Generate mesh
                ChunkComponent.SetMesh(chunkMesher.CreateMesh(ChunkComponent.Data));

                ChunkComponent.Status = ChunkStatus.Complete;
            }

            yield return null;
        }
    }

    protected void TryToDeactivate(AbstractChunkComponent<ChunkDataType, VoxelDataType> chunkComponent)
    {
        if (chunkComponent.Status == ChunkStatus.ScheduledForData ||
            chunkComponent.Status == ChunkStatus.ScheduledForData)
        {
            //Cannot be deactivated right now, as it is currently queued for processing.
            //Set the target status so that it can be deactivated as soon as possible
            chunkComponent.TargetStatus = ChunkStatus.Deactivated;
            return;
            //Whatever requested the deactivation can do so again later
        }

        loadedChunks.Remove(chunkComponent.ChunkID);

        //Return modified data to the provider
        if (chunkComponent.Data != null && chunkComponent.Data.ModifiedSinceGeneration)
        {
            chunkProvider.AddModifiedChunkData(chunkComponent.ChunkID, chunkComponent.Data);
        }

        //Return chunk gameobject to pool
        chunkPool.ReturnToPool(chunkComponent.gameObject);
    }

    /// <summary>
    /// Starts the process of (re)generating and meshing a chunk
    /// if it doesn't already exist.
    /// </summary>
    /// <param name="chunkID"></param>
    protected void RequestRegenerationOfChunk(Vector3Int chunkID, AbstractChunkComponent<ChunkDataType, VoxelDataType> ChunkComponent = null,ChunkStatus targetStatus = ChunkStatus.Complete)
    {
        if (ChunkComponent == null)
        {
            if (!loadedChunks.TryGetValue(chunkID, out ChunkComponent))
            {
                //Get a new Chunk GameObject to house the generated Chunk data.
                var ChunkObject = chunkPool.Next(transform);
                ChunkComponent = ChunkObject.GetComponent<AbstractChunkComponent<ChunkDataType, VoxelDataType>>();
                ChunkComponent.Initialise(chunkID, ChunkToWorldPosition(chunkID));
                //Add to set of loaded chunks
                loadedChunks[chunkID] = ChunkComponent;

                Assert.AreEqual(ChunkStatus.ReadyForData, ChunkComponent.Status);
                Assert.AreEqual(ChunkStatus.ReadyForData, ChunkComponent.TargetStatus);
            }
        }

        //Update the target status if it is a later state than the chunks current target.
        ChunkComponent.TargetStatus = (targetStatus > ChunkComponent.TargetStatus) ? targetStatus: ChunkComponent.TargetStatus;

        //DEBUG
        ChunkComponent.inMeshRadius = InsideChunkRadius(ChunkComponent.ChunkID, meshedChunksRadii);

        NextStep(ChunkComponent);

    }

    /// <summary>
    /// Sets up the next step in the chunk creation process
    /// </summary>
    /// <param name="ChunkComponent"></param>
    private void NextStep(AbstractChunkComponent<ChunkDataType, VoxelDataType> ChunkComponent) {
        if (ChunkComponent.Status == ChunkComponent.TargetStatus)
        {
            return;//The chunk does not want to take any further steps at this time
        }

        //If the chunk is supposed to be deactivated, do so
        if (ChunkComponent.TargetStatus == ChunkStatus.Deactivated)
        {
            TryToDeactivate(ChunkComponent);
        }

        switch (ChunkComponent.Status)
        {
            case ChunkStatus.ReadyForData:
                {
                    //This is a newly created chunk, schedule it 
                    ChunkComponent.Status = ChunkStatus.ScheduledForData;
                    NeedDataQueue.Enqueue(ChunkComponent);
                }
                break;
            case ChunkStatus.ScheduledForData://Already scheduled, and will be processed in due course
                break;
            case ChunkStatus.WaitingForNeighbourData:
                {                    
                    if (DoNeighboursHaveData(ChunkComponent.ChunkID,ChunkComponent.TargetStatus==ChunkStatus.Complete))
                    {
                        //All neighbours have data, so chunk is ready for mesh
                        ChunkComponent.Status = ChunkStatus.ReadyForMesh;
                        //Move on to further steps if desired
                        NextStep(ChunkComponent);
                    }
                    //If neighbours do not have data, keep waiting
                }
                break;
            case ChunkStatus.ReadyForMesh:
                {
                    //Has data, needs mesh
                    ChunkComponent.Status = ChunkStatus.ScheduledForMesh;
                    NeedMeshingQueue.Enqueue(ChunkComponent);
                }
                break;
            case ChunkStatus.ScheduledForMesh://Already scheduled, and will be processed in due course
                break;
            case ChunkStatus.Complete:
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Returns true iff all 6 direct neighbours of the chunk
    /// have data ready, optionally requests the generation of data
    /// for them if not.
    /// </summary>
    /// <param name="ChunkID"></param>
    /// <returns></returns>
    private bool DoNeighboursHaveData(Vector3Int ChunkID,bool RequestGenerationIfMissing = false) 
    {
        bool allHaveData = true;
        foreach (var dir in Directions.IntVectors)
        {
            var neighbour = ChunkID + dir;
            if (!loadedChunks.TryGetValue(neighbour, out var chunkComponent) || !chunkComponent.DataValid)
            {
                allHaveData = false;
                if (RequestGenerationIfMissing)
                {
                    RequestRegenerationOfChunk(neighbour, chunkComponent, ChunkStatus.WaitingForNeighbourData);
                }
            }

        }
        return allHaveData;
    }

    private void GenerateData(AbstractChunkComponent<ChunkDataType, VoxelDataType> ChunkComponent) {
        ChunkComponent.Data = chunkProvider.ProvideChunkData(ChunkComponent.ChunkID, ChunkDimensions);
    }

    /// <summary>
    /// Manhattan distance query returning true of the chunk ID is 
    /// within the given radii of the player chunk in each axis
    /// </summary>
    /// <param name="chunkID"></param>
    /// <returns></returns>
    protected bool InsideChunkRadius(Vector3Int chunkID,Vector3Int Radii)
    {
        var displacement = playerChunkID - chunkID;
        var absDisplacement = displacement.ElementWise(Mathf.Abs);

        //Inside if all elements of the absolute displacement are less than or equal to the chunk radius
        return absDisplacement.All((a, b) => a <= b, Radii);
    }

    public bool TrySetVoxel(Vector3 worldPos, ushort voxelTypeID,bool overrideExisting)
    {
        Vector3Int localVoxelIndex;
        var chunkID = WorldToChunkPosition(worldPos,out localVoxelIndex);

        if (loadedChunks.TryGetValue(chunkID,out var chunkComponent))
        {
            if (chunkComponent.Status != ChunkStatus.Complete)
            {
                //Disallow edits to incomplete chunks
                return false;
            }

            var newVox = default(VoxelDataType);
            newVox.TypeID = voxelTypeID;
            if (!overrideExisting)
            {
                //Disallow setting voxel if one already exists
                if (chunkComponent.Data[localVoxelIndex].TypeID != 0)
                {
                    return false;
                }
            }
            chunkComponent.Data[localVoxelIndex] = newVox;

            chunkComponent.Status = ChunkStatus.ReadyForMesh;
            RequestRegenerationOfChunk(chunkID, chunkComponent);//regenerate the chunk mesh

            return true;
        }
        return false;
    }

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

        var remainder = floor.ElementWise((a, b) => a % b, ChunkDimensions);
        //Local block index is the remainder, with negatives adjusted
        localVoxelIndex = remainder.ElementWise((a, b) => a < 0 ? b + a : a, ChunkDimensions);

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
        var result = pos.ElementWise(_ => Mathf.Floor(_) + VoxelSize/2.0f);
        return result;
    }

    #endregion
}
