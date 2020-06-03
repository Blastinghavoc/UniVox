using UnityEngine;
using System.Collections;

public class BasicChunkManager : AbstractChunkManager<AbstractChunkData,VoxelData>
{
    protected override void Start()
    {
        base.Start();
        GenerateChunkWithID(new Vector3Int(0, 0, 0));
    }
}
