using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UniVox.Framework.Common;

namespace UniVox.Framework.Lighting
{
    public class LightManager : ILightManager
    {
        private IVoxelTypeManager voxelTypeManager;
        private IChunkManager chunkManager;
        public void Initialise(IChunkManager chunkManager, IVoxelTypeManager voxelTypeManager)
        {
            this.chunkManager = chunkManager;
            this.voxelTypeManager = voxelTypeManager;
        }

        public void OnChunkFullyGenerated(ChunkNeighbourhood neighbourhood)
        {
            Profiler.BeginSample("LightOnChunkGenerated");
            //TODO compute sunlight
            //TODO compute point lights, for now assuming none are generated

            Queue<Vector3Int> propagateQueue = new Queue<Vector3Int>();

            var dimensions = neighbourhood.center.Dimensions;
            var maxBounds = (dimensions - Vector3Int.one).ToNative();

            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        var pos = new Vector3Int(x, y, z);

                        var (emission, _) = voxelTypeManager.GetLightProperties(neighbourhood.GetVoxel(x, y, z));
                        //For now, just set sunlight to a fixed value
                        neighbourhood.SetLight(x, y, z, new LightValue() { Sun = 15, Dynamic = emission });
                        //Check if voxel emits light, add to propagation queue if it does.
                        if (emission > 1)
                        {
                            propagateQueue.Enqueue(pos);
                        }
                    }
                }
            }

            CheckBoundaries(neighbourhood, propagateQueue);

            //Run propagation, but only in FullyGenerated chunks
            BfsPropagateDynamic(neighbourhood, propagateQueue, true);

            Profiler.EndSample();
        }

        private void CheckBoundaries(ChunkNeighbourhood neighbourhood, Queue<Vector3Int> propagateQueue) 
        {
            var dimensions = neighbourhood.center.Dimensions;
            for (int i = 0; i < DirectionExtensions.numDirections; i++)
            {
                Direction dir = (Direction)i;
                var chunkOffset = DirectionExtensions.Vectors[i];
          
                //Check if chunk is FullyGenerated
                var neighChunkId = neighbourhood.center.ChunkID + chunkOffset;
                if (chunkManager.IsChunkFullyGenerated(neighChunkId)) 
                {
                    var positionOffset = dimensions * chunkOffset;
                    var chunkData = neighbourhood.GetChunkData(neighChunkId);
                    foreach (var neighPos in Utils.Helpers.AllPositionsOnChunkBorder(DirectionExtensions.Opposite[i],dimensions))
                    {
                        if (chunkData.GetLight(neighPos.x, neighPos.y, neighPos.z).Dynamic > 1)
                        {
                            propagateQueue.Enqueue(neighPos+positionOffset);
                        }
                    }
                }
            }
        
        }

        //private void CheckBoundary(int3 pos, int3 maxBounds, ChunkNeighbourhood neighbourhood, Queue<Vector3Int> propagateQueue)
        //{
        //    for (int axis = 0; axis < 3; axis++)
        //    {
        //        if (pos[axis] == 0)
        //        {
        //            int3 neighPos = pos;
        //            neighPos[axis] = -1;
        //            var neighPosFromCenter = neighPos;
        //            var neighChunkId = neighbourhood.extendedIndexChunkId(ref neighPos.x, ref neighPos.y, ref neighPos.z);
        //            if (chunkManager.IsChunkFullyGenerated(neighChunkId))
        //            {
        //                var chunkData = neighbourhood.GetChunkData(neighChunkId);
        //                if (chunkData.GetLight(neighPos.x, neighPos.y, neighPos.z).Dynamic > 1)
        //                {
        //                    propagateQueue.Enqueue(neighPosFromCenter.ToBasic());
        //                }
        //            }
        //        }
        //        if (pos[axis] == maxBounds[axis])
        //        {
        //            int3 neighPos = pos;
        //            neighPos[axis] += 1;
        //            var neighPosFromCenter = neighPos;
        //            var neighChunkId = neighbourhood.extendedIndexChunkId(ref neighPos.x, ref neighPos.y, ref neighPos.z);
        //            if (chunkManager.IsChunkFullyGenerated(neighChunkId))
        //            {
        //                var chunkData = neighbourhood.GetChunkData(neighChunkId);
        //                if (chunkData.GetLight(neighPos.x, neighPos.y, neighPos.z).Dynamic > 1)
        //                {
        //                    propagateQueue.Enqueue(neighPosFromCenter.ToBasic());
        //                }
        //            }
        //        }
        //    }            
        //}

        public List<Vector3Int> UpdateLightOnVoxelSet(ChunkNeighbourhood neighbourhood, Vector3Int localCoords, VoxelTypeID voxelType, VoxelTypeID previousType)
        {
            Profiler.BeginSample("LightOnVoxelSet");
            var (emissionCurrent, absorptionCurrent) = voxelTypeManager.GetLightProperties(voxelType);
            var (emissionPrevious, absorptionPrevious) = voxelTypeManager.GetLightProperties(previousType);

            var (x, y, z) = localCoords;

            Queue<Vector3Int> propagateQueue = null;

            var previousLv = neighbourhood.GetLight(x, y, z);

            bool willBeDarker = emissionCurrent < previousLv.Dynamic && absorptionCurrent > absorptionPrevious;

            if (emissionPrevious > 1 || willBeDarker)
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


            if (emissionCurrent > previousLv.Dynamic)
            {
                //if voxel type emits light greater than what was there before, bfs set light values.
                var lv = neighbourhood.GetLight(x, y, z);
                lv.Dynamic = emissionCurrent;
                neighbourhood.SetLight(x, y, z, lv);
                propagateQueue.Enqueue(localCoords);
            }
            else if (absorptionCurrent < absorptionPrevious)
            {
                //Update all neighbours to propagate into this voxel
                foreach (var offset in DirectionExtensions.Vectors)
                {
                    propagateQueue.Enqueue(localCoords + offset);
                }
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
            var initialLightValue = neighbourhood.GetLight(localCoords.x, localCoords.y, localCoords.z);
            lightValuesQueue.Enqueue(initialLightValue);
            initialLightValue.Dynamic = 0;
            neighbourhood.SetLight(localCoords.x, localCoords.y, localCoords.z, initialLightValue);

            propagateQueue = new Queue<Vector3Int>();

            int processed = 0;//TODO remove DEBUG

            while (removalQueue.Count > 0)
            {
                var coords = removalQueue.Dequeue();
                var currentLv = lightValuesQueue.Dequeue();

                foreach (var offset in DirectionExtensions.Vectors)
                {
                    var neighbourCoord = coords + offset;
                    var neighbourLightValue = neighbourhood.GetLight(neighbourCoord.x, neighbourCoord.y, neighbourCoord.z);
                    if (neighbourLightValue.Dynamic != 0 && neighbourLightValue.Dynamic < currentLv.Dynamic)
                    {
                        //this neighbour must be removed
                        removalQueue.Enqueue(neighbourCoord);
                        lightValuesQueue.Enqueue(neighbourLightValue);
                        //remove this light
                        neighbourLightValue.Dynamic = 0;
                        neighbourhood.SetLight(neighbourCoord.x, neighbourCoord.y, neighbourCoord.z, neighbourLightValue);
                    }
                    else if (neighbourLightValue.Dynamic >= currentLv.Dynamic)
                    {
                        //This neighbour will need to re-propagate
                        propagateQueue.Enqueue(neighbourCoord);
                    }
                }
                processed++;
            }

            if (processed > 0)
            {
                Debug.Log($"Removal processed {processed}");
            }

        }

        private void BfsPropagateDynamic(ChunkNeighbourhood neighbourhood, Queue<Vector3Int> localCoordsToUpdate, bool fullyGeneratedOnly = false)
        {
            Profiler.BeginSample("PropagateDynamic");

            int processed = 0;//TODO remove DEBUG
            while (localCoordsToUpdate.Count > 0)
            {
                var coords = localCoordsToUpdate.Dequeue();
                var (x, y, z) = coords;
                var thisLightValue = neighbourhood.GetLight(x, y, z);

                foreach (var offset in DirectionExtensions.Vectors)
                {
                    var neighbourCoord = coords + offset;
                    if (fullyGeneratedOnly && !checkIfPositionInFullyGeneratedOrSelf(neighbourhood, neighbourCoord))
                    {
                        continue;
                    }

                    var neighbourLightValue = neighbourhood.GetLight(neighbourCoord.x, neighbourCoord.y, neighbourCoord.z);
                    var (_, absorption) = voxelTypeManager.GetLightProperties(neighbourhood.GetVoxel(neighbourCoord.x, neighbourCoord.y, neighbourCoord.z));
                    var next = thisLightValue.Dynamic - absorption;

                    if (neighbourLightValue.Dynamic < next)
                    {
                        neighbourLightValue.Dynamic = next;
                        neighbourhood.SetLight(neighbourCoord.x, neighbourCoord.y, neighbourCoord.z, neighbourLightValue);
                        localCoordsToUpdate.Enqueue(neighbourCoord);
                    }
                }
                processed++;
            }

            if (processed > 0)
            {
                Debug.Log($"Propagation processed {processed}");
            }
            Profiler.EndSample();
        }

        /// <summary>
        /// Returns true if the local position is in the center chunk of the neighbourhood,
        /// or is in a Fully Generated chunk.
        /// </summary>
        /// <returns></returns>
        private bool checkIfPositionInFullyGeneratedOrSelf(ChunkNeighbourhood neighbourhood, Vector3Int localCoord)
        {
            var (x, y, z) = localCoord;
            var chunkId = neighbourhood.extendedIndexChunkId(ref x, ref y, ref z);
            if (chunkId.Equals(neighbourhood.center.ChunkID))
            {
                return true;
            }
            return chunkManager.IsChunkFullyGenerated(chunkId);
        }
    }
}