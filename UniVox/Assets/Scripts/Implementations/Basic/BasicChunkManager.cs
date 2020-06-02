using UnityEngine;
using System.Collections;

public class BasicChunkManager : AbstractChunkManager<BasicChunkData,BasicVoxelData>
{
    private void Start()
    {
        chunkMesher = new BasicChunkMesher();
        chunkProvider = new BasicChunkProvider();
        GenerateChunkWithID(new Vector3Int(0, 0, 0));
    }
}
