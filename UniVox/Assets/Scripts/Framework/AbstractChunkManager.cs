using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UniVox.Framework;
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

    [SerializeField] protected Transform Player;

    [Range(0, 100)]
    [SerializeField] protected int ChunkRadius;

    /// <summary>
    /// Controls how many chunks can be generated and meshed per update
    /// </summary>
    [Range(1, 100)]
    [SerializeField] protected ushort MaxChunksPerUpdate = 1;
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
    }

    /// <summary>
    /// Load chunks around the player, unload those that are too far.
    /// By Default only loading chunks in two dimensions.
    /// </summary>
    protected void UpdatePlayerArea()
    {
        playerChunkID = WorldToChunkPosition(Player.position);

        //Find all chunks outside the radius
        var outside = loadedChunks.Where(pair => !InsideChunkRadius(pair.Key))
            .Select(pair => pair)
            .ToList();

        //Deactivate all chunks outside the radius
        outside.ForEach(pair => DeactivateChunk(pair.Value, pair.Key));

        for (int z = -ChunkRadius; z < ChunkRadius; z++)
        {
            //for (int y = -ChunkRadius; y < ChunkRadius; y++)
            //{
            for (int x = -ChunkRadius; x < ChunkRadius; x++)
            {
                var chunkID = playerChunkID + new Vector3Int(x, 0, z);
                RequestRegenerationOfChunk(chunkID);//Request that this chunk should exist
            }
            //}
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
        while (gameObject.activeSelf)
        {
            for (int i = 0; i < MaxChunksPerUpdate && NeedDataQueue.Count > 0; i++)
            {
                var ChunkComponent = NeedDataQueue.Dequeue();

                if (ChunkComponent.status == ChunkStatus.Deactivated)
                {
                    continue;
                }

                GenerateData(ChunkComponent);
                ChunkComponent.status = ChunkStatus.ReadyForMesh;
                NextStep(ChunkComponent);
            }

            for (int i = 0; i < MaxChunksPerUpdate && NeedMeshingQueue.Count > 0; i++)
            {
                var ChunkComponent = NeedMeshingQueue.Dequeue();

                if (ChunkComponent.status == ChunkStatus.Deactivated)
                {
                    continue;
                }

                GenerateMesh(ChunkComponent);
                ChunkComponent.status = ChunkStatus.Complete;
            }

            yield return null;
        }
    }

    protected void DeactivateChunk(AbstractChunkComponent<ChunkDataType, VoxelDataType> chunkComponent, Vector3Int chunkID)
    {
        loadedChunks.Remove(chunkID);

        //Return modified data to the provider
        if (chunkComponent.Data != null && chunkComponent.Data.ModifiedSinceGeneration)
        {
            chunkProvider.AddModifiedChunkData(chunkID, chunkComponent.Data);
        }

        chunkComponent.status = ChunkStatus.Deactivated;

        //Return chunk gameobject to pool
        chunkPool.ReturnToPool(chunkComponent.gameObject);
    }

    /// <summary>
    /// Starts the process of (re)generating and meshing a chunk
    /// if it doesn't already exist.
    /// </summary>
    /// <param name="chunkID"></param>
    protected void RequestRegenerationOfChunk(Vector3Int chunkID, AbstractChunkComponent<ChunkDataType, VoxelDataType> ChunkComponent = null)
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
            }
        }

        NextStep(ChunkComponent);

    }

    /// <summary>
    /// Sets up the next step in the chunk creation process
    /// </summary>
    /// <param name="ChunkComponent"></param>
    private void NextStep(AbstractChunkComponent<ChunkDataType, VoxelDataType> ChunkComponent) {
        switch (ChunkComponent.status)
        {
            case ChunkStatus.ReadyForData:
                {
                    //This is a newly created chunk, schedule it 
                    ChunkComponent.status = ChunkStatus.ScheduledForData;
                    NeedDataQueue.Enqueue(ChunkComponent);
                }
                break;
            case ChunkStatus.ScheduledForData://Already scheduled, and will be processed in due course
                break;
            case ChunkStatus.ReadyForMesh:
                {
                    //Has data, needs mesh
                    ChunkComponent.status = ChunkStatus.ScheduledForMesh;
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

    private void GenerateMesh(AbstractChunkComponent<ChunkDataType, VoxelDataType> ChunkComponent) {
        ChunkComponent.SetMesh(chunkMesher.CreateMesh(ChunkComponent.Data));
    }

    private void GenerateData(AbstractChunkComponent<ChunkDataType, VoxelDataType> ChunkComponent) {
        ChunkComponent.Data = chunkProvider.ProvideChunkData(ChunkComponent.ChunkID, ChunkDimensions);
    }

    /// <summary>
    /// Manhattan distance query returning true 
    /// </summary>
    /// <param name="chunkID"></param>
    /// <returns></returns>
    protected bool InsideChunkRadius(Vector3Int chunkID)
    {
        var displacement = playerChunkID - chunkID;
        var absDisplacement = new Vector3Int(
            Mathf.Abs(displacement.x),
            Mathf.Abs(displacement.y),
            Mathf.Abs(displacement.z));
        //Inside if all elements of the absolute displacement are less than or equal to the chunk radius
        return absDisplacement.All(_ => _ <= ChunkRadius);
    }

    public bool TrySetVoxel(Vector3 worldPos, ushort voxelTypeID,bool overrideExisting)
    {
        Vector3Int localVoxelIndex;
        var chunkID = WorldToChunkPosition(worldPos,out localVoxelIndex);

        if (loadedChunks.TryGetValue(chunkID,out var chunkComponent))
        {
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

            chunkComponent.status = ChunkStatus.ReadyForMesh;
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
