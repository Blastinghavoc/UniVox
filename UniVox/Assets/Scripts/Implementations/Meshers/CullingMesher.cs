using UnityEngine;
using UniVox.Framework;
using UniVox.Implementations.ChunkData;
using UniVox.Implementations.Common;

namespace UniVox.Implementations.Meshers
{
    public class CullingMesher : AbstractMesherComponent<AbstractChunkData, VoxelData>
    {
        public override void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager) 
        {
            base.Initialise(voxelTypeManager, chunkManager);
            //Culling mesher depends on neighbouring chunks for meshing
            IsMeshDependentOnNeighbourChunks = true;
        }

        protected override bool IncludeFace(AbstractChunkData chunk, Vector3Int position, int direction)
        {
            var adjacentVoxelIndex = position + Directions.IntVectors[direction];            

            if (chunk.TryGetVoxelAtLocalCoordinates(adjacentVoxelIndex, out VoxelData adjacentVoxel))
            {//If adjacent voxel is in the chunk

                return IncludeFaceOfAdjacentWithID(adjacentVoxel.TypeID, direction);
            }
            else
            {
                //If adjacent voxel is in the neighbouring chunk

                var neighbourChunkIndex = chunk.ChunkID + Directions.IntVectors[direction];
                var localIndexOfAdjacentVoxelInNeighbour = chunkManager.LocalVoxelIndexOfPosition(adjacentVoxelIndex);

                if (chunkManager.TryGetVoxel(neighbourChunkIndex, localIndexOfAdjacentVoxelInNeighbour, out var adjacentVoxelTypeID))
                {
                    return IncludeFaceOfAdjacentWithID(adjacentVoxelTypeID, direction);
                }

            }
            //Adjacent voxel cannot be found, assume the face must be included
            return true;
        }

        private bool IncludeFaceOfAdjacentWithID(ushort voxelTypeID,int direction) 
        {
            if (voxelTypeID == VoxelTypeManager.AIR_ID)
            {
                //Include the face if the adjacent voxel is air
                return true;
            }
            var adjacentData = voxelTypeManager.GetData(voxelTypeID);

            //Exclude this face if adjacent face is solid
            return !adjacentData.definition.meshDefinition.Faces[Directions.Oposite[direction]].isSolid;
        }
    }
}