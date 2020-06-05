using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using Utils.Pooling;

public abstract class AbstractChunkManager<ChunkDataType, VoxelDataType> : MonoBehaviour, IChunkManager 
    where ChunkDataType : IChunkData<VoxelDataType>
    where VoxelDataType : IVoxelData
{
    [SerializeField]
    private Vector3Int CHUNK_DIMENSIONS = new Vector3Int(32, 32, 32);

    [SerializeField]
    private int VOXEL_SIZE = 1;

    public AbstractProviderComponent<ChunkDataType, VoxelDataType> chunkProvider;
    public AbstractMesherComponent<ChunkDataType, VoxelDataType> chunkMesher;

    public VoxelTypeManager VoxelTypeManager;

    public PrefabPool chunkPool;

    public Transform Player;
    [Range(0, 100)]
    public int ChunkRadius;

    protected Dictionary<Vector3Int, AbstractChunkComponent<ChunkDataType, VoxelDataType>> loadedChunks = new Dictionary<Vector3Int, AbstractChunkComponent<ChunkDataType, VoxelDataType>>();

    //Current chunkID occupied by the Player transform
    protected Vector3Int playerChunkID = Vector3Int.zero;

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
                if (!loadedChunks.ContainsKey(chunkID))
                {
                    GenerateChunkWithID(chunkID);
                }
            }
            //}
        }
    }

    protected void DeactivateChunk(AbstractChunkComponent<ChunkDataType, VoxelDataType> chunkComponent, Vector3Int chunkID)
    {
        loadedChunks.Remove(chunkID);
        chunkPool.ReturnToPool(chunkComponent.gameObject);
    }

    protected void GenerateChunkWithID(Vector3Int chunkID)
    {

        //Get a new Chunk GameObject to house the generated Chunk data.
        var ChunkObject = chunkPool.Next(transform);
        var ChunkComponent = ChunkObject.GetComponent<AbstractChunkComponent<ChunkDataType, VoxelDataType>>();

        Assert.IsNotNull(ChunkComponent);

        ChunkComponent.name = $"Chunk ({chunkID.x},{chunkID.y},{chunkID.z})";
        ChunkComponent.transform.position = ChunkToWorldPosition(chunkID);

        //Add to set of loaded chunks
        loadedChunks[chunkID] = ChunkComponent;

        ChunkComponent.Data = chunkProvider.ProvideChunkData(chunkID, CHUNK_DIMENSIONS);

        GenerateMesh(ChunkComponent);
    }

    protected void GenerateMesh(AbstractChunkComponent<ChunkDataType, VoxelDataType> ChunkComponent) {
        ChunkComponent.SetMesh(chunkMesher.CreateMesh(ChunkComponent.Data));
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
            var tmp1 = chunkComponent.Data[localVoxelIndex];
            chunkComponent.Data[localVoxelIndex] = newVox;
            var tmp = chunkComponent.Data[localVoxelIndex];
            GenerateMesh(chunkComponent);
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
        var result = floor.ElementWise((a, b) => Mathf.FloorToInt(a / (float)b), CHUNK_DIMENSIONS);

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
        var result = floor.ElementWise((a, b) => Mathf.FloorToInt(a / (float)b), CHUNK_DIMENSIONS);

        var remainder = floor.ElementWise((a, b) => a % b, CHUNK_DIMENSIONS);
        //Local block index is the remainder, with negatives adjusted
        localVoxelIndex = remainder.ElementWise((a, b) => a < 0 ? b + a : a, CHUNK_DIMENSIONS);

        return result;
    }

    public Vector3 ChunkToWorldPosition(Vector3Int chunkID)
    {
        //ChunkManager must be located at world origin
        Assert.AreEqual(Vector3.zero, transform.position);
        Assert.AreEqual(Quaternion.Euler(0, 0, 0), transform.rotation);

        Vector3 result = chunkID * CHUNK_DIMENSIONS;
        return result;
    }

    public Vector3 SnapToVoxelCenter(Vector3 pos)
    {
        var result = pos.ElementWise(_ => Mathf.Floor(_) + VOXEL_SIZE/2.0f);
        return result;
    }

    #endregion
}
