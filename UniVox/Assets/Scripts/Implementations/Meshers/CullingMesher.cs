using System.Collections.Generic;
using UnityEngine;
using UniVox.Framework;
using UniVox.Implementations.ChunkData;

namespace UniVox.Implementations.Meshers
{
    public class CullingMesher : AbstractMesherComponent
    {
        public override void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager) 
        {
            base.Initialise(voxelTypeManager, chunkManager);
            //Culling mesher depends on neighbouring chunks for meshing
            IsMeshDependentOnNeighbourChunks = true;
        }
    }
}