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
    public IChunkProvider<ChunkDataType, VoxelDataType> chunkProvider;
    public IChunkMesher<ChunkDataType, VoxelDataType> chunkMesher;

    public PrefabPool chunkPool;

    public void GenerateChunkWithID(Vector3Int chunkID) {
        
        //Get a new Chunk GameObject to house the generated Chunk data.
        var ChunkObject = chunkPool.Next(transform);
        var ChunkComponent = ChunkObject.GetComponent<AbstractChunkComponent<ChunkDataType,VoxelDataType>>();

        Assert.IsNotNull(ChunkComponent);

        ChunkComponent.Data = chunkProvider.ProvideChunkData(chunkID, CHUNK_DIMENSIONS);

        ChunkComponent.SetMesh(chunkMesher.CreateMesh(ChunkComponent.Data));
    }
}
