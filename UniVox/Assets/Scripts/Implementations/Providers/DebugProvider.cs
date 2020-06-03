using UnityEngine;
using System.Collections;

public class DebugProvider : AbstractProviderComponent<AbstractChunkData,VoxelData>
{
    public override AbstractChunkData ProvideChunkData(Vector3Int chunkID, Vector3Int chunkDimensions)
    {
        var ChunkData = new ArrayChunkData(chunkID, chunkDimensions);
        for (int z = 0; z < chunkDimensions.z; z++)
        {
            for (int x = 0; x < chunkDimensions.x; x++)
            {
                ChunkData[x, 0, z] = new VoxelData(1);
            }
        }
        return ChunkData;
    }
}
