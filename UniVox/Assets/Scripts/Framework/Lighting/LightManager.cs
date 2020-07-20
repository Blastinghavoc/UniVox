using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniVox.Framework.Common;

namespace UniVox.Framework.Lighting
{
    public class LightManager
    {
        private IVoxelTypeManager voxelTypeManager;
        public void Initialise(IVoxelTypeManager voxelTypeManager)
        {
            this.voxelTypeManager = voxelTypeManager;
        }

        public void OnChunkGenerated(IChunkData chunkData)
        {
            //TODO compute sunlight
            //TODO compute point lights, for now assuming none are generated
        }

        public List<Vector3Int> UpdateLightOnVoxelSet(ChunkNeighbourhood neighbourhood, Vector3Int localCoords, VoxelTypeID voxelType, VoxelTypeID previousType)
        {
            var lightEmissionCurrent = voxelTypeManager.GetLightEmission(voxelType);
            var lightEmissionPrevious = voxelTypeManager.GetLightEmission(voxelType);

            var (x, y, z) = localCoords;

            if (lightEmissionPrevious > 1)
            {
                //if previous type emitted light, undo light 

            }


            if (lightEmissionCurrent > 1)
            {
                //if voxel type emits light, bfs set light values.
                var lv = neighbourhood.GetLightValue(x,y,z);
                lv.Dynamic = lightEmissionCurrent;
                neighbourhood.SetLightValue(x, y, z, lv);
                BfsPropagateDynamic(neighbourhood, localCoords);
            }
            else
            {               
                //Otherwise, update light propagation
                //TODO incorporate opacity, for now assuming all non-air is opaque.                   
                
            }

            return neighbourhood.GetAllUsedNeighbourIds();
        }

        private void BfsPropagateDynamic(ChunkNeighbourhood neighbourhood, Vector3Int localCoords) 
        {
            Queue<Vector3Int> localCoordsToUpdate = new Queue<Vector3Int>();
            localCoordsToUpdate.Enqueue(localCoords);


            while (localCoordsToUpdate.Count > 0)
            {
                var coords = localCoordsToUpdate.Dequeue();
                var (x, y, z) = coords;
                var thisLightValue = neighbourhood.GetLightValue(x, y, z);
                var next = thisLightValue.Dynamic - 1;
                foreach (var offset in DirectionExtensions.Vectors)
                {
                    var neighbourCoord = coords + offset;
                    var neighbourLightValue = neighbourhood.GetLightValue(neighbourCoord.x, neighbourCoord.y, neighbourCoord.z);
                    if (neighbourLightValue.Dynamic < next)
                    {
                        neighbourLightValue.Dynamic = next;
                        neighbourhood.SetLightValue(neighbourCoord.x, neighbourCoord.y, neighbourCoord.z, neighbourLightValue);
                        localCoordsToUpdate.Enqueue(neighbourCoord);
                    }
                }

            }
        }

    }
}