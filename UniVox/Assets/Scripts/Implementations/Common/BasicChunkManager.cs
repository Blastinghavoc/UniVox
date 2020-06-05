using UnityEngine;
using System.Collections;
using UniVox.Implementations.ChunkData;

namespace UniVox.Implementations.Common
{
    public class BasicChunkManager : AbstractChunkManager<AbstractChunkData, VoxelData>
    {
        protected override void Start()
        {
            base.Start();            
        }
    }
}