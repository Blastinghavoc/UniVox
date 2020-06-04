using UnityEngine;
using System.Collections;

public class DebugProvider : AbstractProviderComponent<AbstractChunkData,VoxelData>
{
    public override AbstractChunkData ProvideChunkData(Vector3Int chunkID, Vector3Int chunkDimensions)
    {
        return HalfHeight(chunkID, chunkDimensions);
    }

    private AbstractChunkData HalfHeight(Vector3Int chunkID, Vector3Int chunkDimensions) 
    {
        var ChunkData = new ArrayChunkData(chunkID, chunkDimensions);
        for (int z = 0; z < chunkDimensions.z; z++)
        {
            for (int y = 0; y < chunkDimensions.y / 2; y++)
            {
                for (int x = 0; x < chunkDimensions.x; x++)
                {
                    ChunkData[x, y, z] = new VoxelData(1);
                }
            }
        }
        return ChunkData;
    }

    private AbstractChunkData HalfLattice(Vector3Int chunkID, Vector3Int chunkDimensions) {
        bool b = true;
        var ChunkData = new ArrayChunkData(chunkID, chunkDimensions);
        for (int z = 0; z < chunkDimensions.z; z++)
        {
            b = !b;
            for (int y = 0; y < chunkDimensions.y / 2; y++)
            {
                b = !b;
                for (int x = 0; x < chunkDimensions.x; x++)
                {
                    b = !b;
                    if (b)
                    {
                        continue;
                    }
                    ChunkData[x, y, z] = new VoxelData(1);
                }
            }
        }
        return ChunkData;
    }
}
