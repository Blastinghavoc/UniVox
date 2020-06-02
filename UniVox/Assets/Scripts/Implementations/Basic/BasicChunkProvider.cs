using UnityEngine;
using System.Collections;

public class BasicChunkProvider : IChunkProvider<BasicChunkData,BasicVoxelData>
{
    public BasicChunkData ProvideChunkData(Vector3Int chunkID, Vector3Int chunkDimensions)
    {
        var ChunkData = new BasicChunkData(chunkID, chunkDimensions);
        ChunkData.SetVoxelAtLocalCoordinates(new Vector3Int(0, 0, 0), new BasicVoxelData(1));
        return ChunkData;
    }
}
