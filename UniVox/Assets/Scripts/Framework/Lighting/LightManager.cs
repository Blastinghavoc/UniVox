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
            var (emissionCurrent,absorptionCurrent) = voxelTypeManager.GetLightProperties(voxelType);
            var (emissionPrevious,absorptionPrevious) = voxelTypeManager.GetLightProperties(previousType);

            var (x, y, z) = localCoords;

            Queue<Vector3Int> propagateQueue = null;

            var previousLv = neighbourhood.GetLightValue(x, y, z);

            if (emissionPrevious > 1 || (previousLv.Dynamic >1 && absorptionCurrent!=absorptionPrevious))
            {
                //if there was light at this position, remove it
                Profiler.BeginSample("RemoveLight");
                BfsRemovalDynamic(neighbourhood, localCoords, out propagateQueue);
                Profiler.EndSample();
            }
            else
            {
                propagateQueue = new Queue<Vector3Int>();
            }


            if (emissionCurrent > 1)
            {
                //if voxel type emits light, bfs set light values.
                var lv = neighbourhood.GetLightValue(x, y, z);
                lv.Dynamic = emissionCurrent;
                neighbourhood.SetLightValue(x, y, z, lv);
                propagateQueue.Enqueue(localCoords);
            }

            //TODO incorporate sunlight
            //TODO incorporate opacity 


            BfsPropagateDynamic(neighbourhood, propagateQueue);

            Profiler.EndSample();
            return neighbourhood.GetAllUsedNeighbourIds();
        }

        private void BfsRemovalDynamic(ChunkNeighbourhood neighbourhood, Vector3Int localCoords, out Queue<Vector3Int> propagateQueue)
        {
            Queue<Vector3Int> removalQueue = new Queue<Vector3Int>();
            Queue<LightValue> lightValuesQueue = new Queue<LightValue>();
            removalQueue.Enqueue(localCoords);
            var initialLightValue = neighbourhood.GetLightValue(localCoords.x, localCoords.y, localCoords.z);
            lightValuesQueue.Enqueue(initialLightValue);
            initialLightValue.Dynamic = 0;
            neighbourhood.SetLightValue(localCoords.x, localCoords.y, localCoords.z, initialLightValue);

            propagateQueue = new Queue<Vector3Int>();

            int processed = 0;//TODO remove DEBUG

            while (removalQueue.Count > 0)
            {
                var coords = removalQueue.Dequeue();
                var currentLv = lightValuesQueue.Dequeue();

                foreach (var offset in DirectionExtensions.Vectors)
                {
                    var neighbourCoord = coords + offset;
                    var neighbourLightValue = neighbourhood.GetLightValue(neighbourCoord.x, neighbourCoord.y, neighbourCoord.z);
                    if (neighbourLightValue.Dynamic != 0 && neighbourLightValue.Dynamic < currentLv.Dynamic)
                    {
                        //this neighbour must be removed
                        removalQueue.Enqueue(neighbourCoord);
                        lightValuesQueue.Enqueue(neighbourLightValue);
                        //remove this light
                        neighbourLightValue.Dynamic = 0;
                        neighbourhood.SetLightValue(neighbourCoord.x, neighbourCoord.y, neighbourCoord.z, neighbourLightValue);
                    }
                    else if (neighbourLightValue.Dynamic >= currentLv.Dynamic)
                    {
                        //This neighbour will need to re-propagate
                        propagateQueue.Enqueue(neighbourCoord);
                    }
                }                
                processed++;
            }

            Debug.Log($"Removal processed {processed}");

        }

        private void BfsPropagateDynamic(ChunkNeighbourhood neighbourhood, Queue<Vector3Int> localCoordsToUpdate)
        {
            int processed = 0;//TODO remove DEBUG
            while (localCoordsToUpdate.Count > 0)
            {
                var coords = localCoordsToUpdate.Dequeue();
                var (x, y, z) = coords;
                var thisLightValue = neighbourhood.GetLightValue(x, y, z);

                foreach (var offset in DirectionExtensions.Vectors)
                {
                    var neighbourCoord = coords + offset;
                    var neighbourLightValue = neighbourhood.GetLightValue(neighbourCoord.x, neighbourCoord.y, neighbourCoord.z);
                    var (_, absorption) = voxelTypeManager.GetLightProperties(neighbourhood.GetVoxel(neighbourCoord.x, neighbourCoord.y, neighbourCoord.z));
                    var next = thisLightValue.Dynamic - absorption;          

                    if (neighbourLightValue.Dynamic < next)
                    {
                        neighbourLightValue.Dynamic = next;
                        neighbourhood.SetLightValue(neighbourCoord.x, neighbourCoord.y, neighbourCoord.z, neighbourLightValue);
                        localCoordsToUpdate.Enqueue(neighbourCoord);
                    }
                }
                processed++;
            }

            Debug.Log($"Propagation processed {processed}");
        }

    }
}