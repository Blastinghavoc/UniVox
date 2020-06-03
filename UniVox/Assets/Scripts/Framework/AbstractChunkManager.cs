using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Utilities.Pooling;

public abstract class AbstractChunkManager<ChunkDataType,VoxelDataType> : MonoBehaviour 
    where ChunkDataType: IChunkData<VoxelDataType>
    where VoxelDataType : IVoxelData
{
    [SerializeField]
    private Vector3Int CHUNK_DIMENSIONS = new Vector3Int(32,32,32);

    public AbstractProviderComponent<ChunkDataType, VoxelDataType> chunkProvider;
    public AbstractMesherComponent<ChunkDataType, VoxelDataType> chunkMesher;

    public PrefabPool chunkPool;

    public Transform Player;
    [Range(0,100)]
    public int ChunkRadius;

    protected Dictionary<Vector3Int, AbstractChunkComponent<ChunkDataType, VoxelDataType>> loadedChunks = new Dictionary<Vector3Int, AbstractChunkComponent<ChunkDataType, VoxelDataType>>();

    protected virtual void Start()
    {
        //Enforce positioning of ChunkManager at the world origin
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.Euler(0, 0, 0);

        chunkProvider = GetComponent<AbstractProviderComponent<ChunkDataType, VoxelDataType>>();
        chunkMesher = GetComponent<AbstractMesherComponent<ChunkDataType, VoxelDataType>>();
    }

    protected virtual void Update() {
        UpdatePlayerArea();
    }

    /// <summary>
    /// Load chunks around the player, unload those that are too far.
    /// By Default only loading chunks in two dimensions.
    /// </summary>
    protected void UpdatePlayerArea() {
        var playerChunkID = WorldToChunkPosition(Player.position);

        for (int z = -ChunkRadius; z < ChunkRadius; z++)
        {
            //for (int y = -ChunkRadius; y < ChunkRadius; y++)
           //{
                for (int x = -ChunkRadius; x < ChunkRadius; x++)
                {
                    var chunkID = playerChunkID + new Vector3Int(x,0,z);
                    if (!loadedChunks.ContainsKey(chunkID))
                    {
                        GenerateChunkWithID(chunkID);
                    }
                }
            //}
        }
    }

    protected void GenerateChunkWithID(Vector3Int chunkID) 
    {
        
        //Get a new Chunk GameObject to house the generated Chunk data.
        var ChunkObject = chunkPool.Next(transform);
        var ChunkComponent = ChunkObject.GetComponent<AbstractChunkComponent<ChunkDataType,VoxelDataType>>();

        Assert.IsNotNull(ChunkComponent);

        ChunkComponent.transform.position = ChunkToWorldPosition(chunkID);

        //Add to set of loaded chunks
        loadedChunks[chunkID] = ChunkComponent;

        ChunkComponent.Data = chunkProvider.ProvideChunkData(chunkID, CHUNK_DIMENSIONS);

        ChunkComponent.SetMesh(chunkMesher.CreateMesh(ChunkComponent.Data));
    }

    public Vector3Int WorldToChunkPosition(Vector3 pos) 
    {
        //ChunkManager must be located at world origin
        Assert.AreEqual(Vector3.zero, transform.position);
        Assert.AreEqual(Quaternion.Euler(0, 0, 0), transform.rotation);

        Vector3Int result = new Vector3Int();
        result.x = Mathf.FloorToInt(pos.x) / CHUNK_DIMENSIONS.x;
        result.y = Mathf.FloorToInt(pos.y) / CHUNK_DIMENSIONS.y;
        result.z = Mathf.FloorToInt(pos.z) / CHUNK_DIMENSIONS.z;
        return result;
    }

    public Vector3 ChunkToWorldPosition(Vector3Int pos) 
    {
        //ChunkManager must be located at world origin
        Assert.AreEqual(Vector3.zero, transform.position);
        Assert.AreEqual(Quaternion.Euler(0, 0, 0), transform.rotation);

        Vector3 result = pos * CHUNK_DIMENSIONS; ;
        return result;
    }
}
