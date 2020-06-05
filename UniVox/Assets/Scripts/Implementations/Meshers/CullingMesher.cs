using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TypeData = UniVox.Framework.VoxelTypeManager.VoxelTypeData;
using UniVox.Framework;
using UniVox.Implementations.ChunkData;
using UniVox.Implementations.Common;

namespace UniVox.Implementations.Meshers
{
    public class CullingMesher : AbstractMesherComponent<AbstractChunkData, VoxelData>
    {
        protected override bool IncludeFace(AbstractChunkData chunk, Vector3Int position, int direction)
        {
            if (chunk.TryGetVoxelAtLocalCoordinates(position + Directions.IntVectors[direction], out VoxelData adjacent))
            {
                if (adjacent.TypeID == VoxelTypeManager.AIR_ID)
                {
                    return true;
                }
                var adjacentData = voxelTypeManager.GetData(adjacent.TypeID);
                //Exclude this face if adjacent face is solid
                return !adjacentData.definition.meshDefinition.Faces[Directions.Oposite[direction]].isSolid;
            }
            return true;
        }
    }
}