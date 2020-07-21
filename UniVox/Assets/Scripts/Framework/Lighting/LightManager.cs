using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UniVox.Framework.Common;

namespace UniVox.Framework.Lighting
{
    public class LightManager : ILightManager
    {
        private IVoxelTypeManager voxelTypeManager;
        public void Initialise(IVoxelTypeManager voxelTypeManager)
        {
            this.voxelTypeManager = voxelTypeManager;
        }

        public void OnChunkGenerated(IChunkData chunkData, IChunkData aboveChunkData)
        {
            Profiler.BeginSample("LightOnChunkGenerated");
            //TODO compute sunlight
            //TODO compute point lights, for now assuming none are generated

            var flatLength = chunkData.Dimensions.x * chunkData.Dimensions.y * chunkData.Dimensions.z;
            var lightChunk = chunkData.lightChunk;

            //For now, just set sunlight to a fixed value
            for (int i = 0; i < flatLength; i++)
            {
                lightChunk[i] = new LightValue() { Sun = 15 };
            }

            Profiler.EndSample();
        }

        public List<Vector3Int> UpdateLightOnVoxelSet(ChunkNeighbourhood neighbourhood, Vector3Int localCoords, VoxelTypeID voxelType, VoxelTypeID previousType)
        {
            Profiler.BeginSample("LightOnVoxelSet");
            var lightEmissionCurrent = voxelTypeManager.GetLightEmission(voxelType);
            var lightEmissionPrevious = voxelTypeManager.GetLightEmission(previousType);

            var (x, y, z) = localCoords;

            Queue<Vector3Int> propagateQueue = null;

            if (lightEmissionPrevious > 1)
            {
                //if previous type emitted light, undo light 
                Profiler.BeginSample("RemoveLight");
                BfsRemovalDynamic(neighbourhood, localCoords, out propagateQueue);
                Profiler.EndSample();
            }
            else
            {
                propagateQueue = new Queue<Vector3Int>();
            }


            if (lightEmissionCurrent > 1)
            {
                //if voxel type emits light, bfs set light values.
                var lv = neighbourhood.GetLightValue(x, y, z);
                lv.Dynamic = lightEmissionCurrent;
                neighbourhood.SetLightValue(x, y, z, lv);
                propagateQueue.Enqueue(localCoords);
            }
            else
            {
                //Otherwise, update light propagation of neighbours into this block
                //TODO incorporate sunlight
                //TODO incorporate opacity 
            }

            BfsPropagateDynamic(neighbourhood, propagateQueue);

            Profiler.EndSample();
            return neighbourhood.GetAllUsedNeighbourIds();
        }

        private void BfsRemovalDynamic(ChunkNeighbourhood neighbourhood, Vector3Int localCoords, out Queue<Vector3Int> propagateQueue)
        {
            Queue<Vector3Int> removalQueue = new Queue<Vector3Int>();
            removalQueue.Enqueue(localCoords);
            propagateQueue = new Queue<Vector3Int>();

            while (removalQueue.Count > 0)
            {
                var coords = removalQueue.Dequeue();
                var (x, y, z) = coords;
                var currentLv = neighbourhood.GetLightValue(x, y, z);

                foreach (var offset in DirectionExtensions.Vectors)
                {
                    var neighbourCoord = coords + offset;
                    var neighbourLightValue = neighbourhood.GetLightValue(neighbourCoord.x, neighbourCoord.y, neighbourCoord.z);
                    if (neighbourLightValue.Dynamic != 0 && neighbourLightValue.Dynamic < currentLv.Dynamic)
                    {
                        //this neighbour must be removed
                        removalQueue.Enqueue(neighbourCoord);
                    }
                    else if (neighbourLightValue.Dynamic >= currentLv.Dynamic)
                    {
                        //This neighbour will need to re-propagate
                        propagateQueue.Enqueue(neighbourCoord);
                    }
                }

                //remove this light
                currentLv.Dynamic = 0;
                neighbourhood.SetLightValue(x, y, z, currentLv);
            }

        }

        private void BfsPropagateDynamic(ChunkNeighbourhood neighbourhood, Queue<Vector3Int> localCoordsToUpdate)
        {
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