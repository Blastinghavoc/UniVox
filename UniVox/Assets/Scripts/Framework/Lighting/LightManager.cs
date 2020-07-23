using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UniVox.Framework.Common;
using static Utils.Helpers;

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

        public void OnChunkFullyGenerated(ChunkNeighbourhood neighbourhood, int[] heightMap)
        {
            Profiler.BeginSample("LightOnChunkGenerated");

            Queue<PropagationNode> propagateQueue = new Queue<PropagationNode>();

            var dimensions = neighbourhood.center.Dimensions;

            //TODO compute sunlight from above chunk if it's loaded

            Queue<PropagationNode> sunlightQueue = new Queue<PropagationNode>();

            var aboveChunkId = neighbourhood.center.ChunkID + Vector3Int.up;
            var yMax = dimensions.y - 1;

            if (ChunkWritable(aboveChunkId, neighbourhood))
            {
                Profiler.BeginSample("ReadFromAbove");
                //Above chunk is available, get the sunlight levels from its bottom border
                var aboveChunk = neighbourhood.GetChunkData(aboveChunkId);
                int y = 0;

                for (int z = 0; z < dimensions.z; z++)
                {
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        var sunlight = aboveChunk.GetLight(x, y, z).Sun;
                        var voxelAtTop = neighbourhood.center[x, yMax, z];
                        var (_, absorption) = voxelTypeManager.GetLightProperties(voxelAtTop);
                        if (absorption < LightValue.MaxIntensity && sunlight > 1) 
                        {
                            if (absorption == 1 && sunlight == LightValue.MaxIntensity)
                            {
                                //Do nothing, sunlight preserved
                            }
                            else
                            {
                                sunlight -= absorption;
                            }
                            neighbourhood.center.SetLight(x, yMax, z, new LightValue() { Sun = sunlight });
                            sunlightQueue.Enqueue(new PropagationNode()
                            {
                                localPosition = new Vector3Int(x, yMax, z),
                                chunkData = neighbourhood.center
                            });
                        }
                    }
                }
                Profiler.EndSample();
            }
            else
            {
                Profiler.BeginSample("GuessFromHM");
                //If above chunk not available, guess the sunlight level
                var chunkPosition = chunkManager.ChunkToWorldPosition(neighbourhood.center.ChunkID);
                var chunkTop = chunkPosition.y + dimensions.y;
                int mapIndex = 0;                

                for (int z = 0; z < dimensions.z; z++)
                {
                    for (int x = 0; x < dimensions.x; x++, mapIndex++)
                    {
                        var hm = heightMap[mapIndex];
                        if (hm < chunkTop)
                        {//Assume this column of voxels can see the sun, as it's above the height map
                            var voxelAtTop = neighbourhood.center[x, yMax, z];
                            var (_, absorption) = voxelTypeManager.GetLightProperties(voxelAtTop);
                            if (absorption < LightValue.MaxIntensity)
                            {
                                var sunlight = LightValue.MaxIntensity;
                                if (absorption > 1)
                                {
                                    sunlight -= absorption;
                                }

                                neighbourhood.center.SetLight(x, yMax, z, new LightValue() { Sun = sunlight });
                                sunlightQueue.Enqueue(new PropagationNode()
                                {
                                    localPosition = new Vector3Int(x, yMax, z),
                                    chunkData = neighbourhood.center
                                });
                            }
                        }
                    }
                }
                Profiler.EndSample();
            }

            PropagateSunlight(neighbourhood, sunlightQueue);

            Profiler.BeginSample("CheckForDynamicSources");
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        var pos = new Vector3Int(x, y, z);

                        var (emission, _) = voxelTypeManager.GetLightProperties(neighbourhood.center[x, y, z]);
                        //Check if voxel emits light, add to propagation queue if it does.
                        if (emission > 1)
                        {
                            var lv = neighbourhood.GetLight(x, y, z);
                            lv.Dynamic = emission;
                            neighbourhood.SetLight(x, y, z, lv);
                            propagateQueue.Enqueue(new PropagationNode()
                            {
                                localPosition = pos,
                                chunkData = neighbourhood.center
                            });
                        }
                    }
                }
            }
            Profiler.EndSample();

            CheckBoundaries(neighbourhood, propagateQueue);

            //Run propagation, but only in FullyGenerated chunks
            PropagateDynamic(neighbourhood, propagateQueue);

            Profiler.EndSample();
        }

        private void CheckBoundaries(ChunkNeighbourhood neighbourhood, Queue<PropagationNode> propagateQueue)
        {
            Profiler.BeginSample("CheckBoundaries");
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
                    var neighChunkData = neighbourhood.GetChunkData(neighChunkId);
                    foreach (var neighPos in AllPositionsOnChunkBorder(DirectionExtensions.Opposite[i], dimensions))
                    {
                        if (neighChunkData.GetLight(neighPos.x, neighPos.y, neighPos.z).Dynamic > 1)
                        {
                            propagateQueue.Enqueue(new PropagationNode()
                            {
                                localPosition = neighPos,
                                chunkData = neighChunkData
                            });
                        }
                    }
                }
            }
            Profiler.EndSample();
        }

        public List<Vector3Int> UpdateLightOnVoxelSet(ChunkNeighbourhood neighbourhood, Vector3Int localCoords, VoxelTypeID voxelType, VoxelTypeID previousType)
        {
            Profiler.BeginSample("LightOnVoxelSet");
            var (emissionCurrent, absorptionCurrent) = voxelTypeManager.GetLightProperties(voxelType);
            var (emissionPrevious, absorptionPrevious) = voxelTypeManager.GetLightProperties(previousType);

            var (x, y, z) = localCoords;

            Queue<PropagationNode> propagateQueue = null;

            var previousLv = neighbourhood.center.GetLight(x, y, z);

            bool dynamicWillBeDarker = emissionCurrent < previousLv.Dynamic && absorptionCurrent > absorptionPrevious;

            if (emissionPrevious > 1 || dynamicWillBeDarker)
            {
                //if there was dynamic light at this position, remove it
                RemovalDynamic(neighbourhood, localCoords, out propagateQueue);
            }
            else
            {
                propagateQueue = new Queue<PropagationNode>();
            }

            Queue<PropagationNode> sunlightPropagateQueue = null;
            if (previousLv.Sun > 0 && absorptionCurrent > absorptionPrevious)
            {//Placing a more opaque block in the path of sunlight
                RemovalSunlight(neighbourhood, localCoords, out sunlightPropagateQueue);
            }
            else
            {
                sunlightPropagateQueue = new Queue<PropagationNode>();
            }

            if (emissionCurrent > previousLv.Dynamic)
            {
                //if voxel type emits light greater than what was there before, bfs set light values.
                var lv = neighbourhood.GetLight(x, y, z);
                lv.Dynamic = emissionCurrent;
                neighbourhood.SetLight(x, y, z, lv);
                propagateQueue.Enqueue(new PropagationNode() { localPosition = localCoords, chunkData = neighbourhood.center });
            }
            else if (absorptionCurrent < absorptionPrevious)
            {
                //Update all neighbours to propagate into this voxel
                foreach (var offset in DirectionExtensions.Vectors)
                {
                    if (TryGetPropagateNode(offset + localCoords, neighbourhood.center, neighbourhood, out var node))
                    {
                        var neighLv = node.chunkData.GetLight(node.localPosition);
                        if (neighLv.Dynamic > 1)
                        {
                            propagateQueue.Enqueue(node);
                        }
                        if (neighLv.Sun > 1)
                        {
                            sunlightPropagateQueue.Enqueue(node);
                        }
                    }
                }
            }

            PropagateDynamic(neighbourhood, propagateQueue);
            PropagateSunlight(neighbourhood, sunlightPropagateQueue);

            Profiler.EndSample();
            return neighbourhood.GetAllUsedNeighbourIds();
        }

        private void RemovalDynamic(ChunkNeighbourhood neighbourhood, Vector3Int localCoords, out Queue<PropagationNode> propagateQueue)
        {
            Profiler.BeginSample("RemoveDynamic");
            Queue<RemovalNode> removalQueue = new Queue<RemovalNode>();
            propagateQueue = new Queue<PropagationNode>();
            LightValue initialLightValue;

            if (TryGetRemovalNode(localCoords, neighbourhood.center, neighbourhood, out var firstNode))
            {
                initialLightValue = firstNode.lv;
                removalQueue.Enqueue(firstNode);
            }
            else
            {
                return;
            }

            initialLightValue.Dynamic = 0;
            neighbourhood.SetLight(localCoords.x, localCoords.y, localCoords.z, initialLightValue);

            int processed = 0;//TODO remove DEBUG

            while (removalQueue.Count > 0)
            {
                var node = removalQueue.Dequeue();
                var coords = node.localPosition;
                var currentLv = node.lv;

                foreach (var offset in DirectionExtensions.Vectors)
                {
                    var neighbourCoord = coords + offset;

                    if (TryGetRemovalNode(neighbourCoord, node.chunkData, neighbourhood, out var newNode))
                    {
                        var neighLv = newNode.lv;
                        if (neighLv.Dynamic != 0 && neighLv.Dynamic < currentLv.Dynamic)
                        {
                            //this neighbour must be removed
                            removalQueue.Enqueue(newNode);
                            //remove this light
                            neighLv.Dynamic = 0;
                            newNode.chunkData.SetLight(newNode.localPosition, neighLv);
                        }
                        else if (neighLv.Dynamic >= currentLv.Dynamic)
                        {
                            //This neighbour will need to re-propagate
                            if (TryGetPropagateNode(neighbourCoord, node.chunkData, neighbourhood, out var propNode))
                            {
                                propagateQueue.Enqueue(propNode);
                            }
                        }

                    }
                }
                processed++;
            }

            if (processed > 0)
            {
                Debug.Log($"Removal processed {processed}");
            }
            Profiler.EndSample();
        }

        private void RemovalSunlight(ChunkNeighbourhood neighbourhood, Vector3Int localCoords, out Queue<PropagationNode> propagateQueue)
        {
            Profiler.BeginSample("RemoveSun");
            Queue<RemovalNode> removalQueue = new Queue<RemovalNode>();
            propagateQueue = new Queue<PropagationNode>();
            LightValue initialLightValue;

            if (TryGetRemovalNode(localCoords, neighbourhood.center, neighbourhood, out var firstNode))
            {
                initialLightValue = firstNode.lv;
                removalQueue.Enqueue(firstNode);
            }
            else
            {
                return;
            }

            initialLightValue.Sun = 0;
            neighbourhood.SetLight(localCoords.x, localCoords.y, localCoords.z, initialLightValue);

            int processed = 0;//TODO remove DEBUG

            while (removalQueue.Count > 0)
            {
                var node = removalQueue.Dequeue();
                var coords = node.localPosition;
                var currentLv = node.lv;

                foreach (var offset in DirectionExtensions.Vectors)
                {
                    var neighbourCoord = coords + offset;

                    if (TryGetRemovalNode(neighbourCoord, node.chunkData, neighbourhood, out var newNode))
                    {
                        var neighLv = newNode.lv;
                        if ((neighLv.Sun != 0 && neighLv.Sun < currentLv.Sun)||
                            (currentLv.Sun == LightValue.MaxIntensity) && offset.y == -1)//Also remove sunlight below max sunlight
                        {
                            //this neighbour must be removed
                            removalQueue.Enqueue(newNode);
                            //remove this light
                            neighLv.Sun = 0;
                            newNode.chunkData.SetLight(newNode.localPosition, neighLv);
                        }
                        else if (neighLv.Sun >= currentLv.Sun)
                        {
                            //This neighbour will need to re-propagate
                            if (TryGetPropagateNode(neighbourCoord, node.chunkData, neighbourhood, out var propNode))
                            {
                                propagateQueue.Enqueue(propNode);
                            }
                        }

                    }
                }
                processed++;
            }

            if (processed > 0)
            {
                Debug.Log($"Remove Sunlight processed {processed}");
            }
            Profiler.EndSample();
        }

        private void PropagateDynamic(ChunkNeighbourhood neighbourhood, Queue<PropagationNode> queue)
        {
            Profiler.BeginSample("PropagateDynamic");

            HashSet<PropagationNode> visited = new HashSet<PropagationNode>();

            int processed = 0;//TODO remove DEBUG
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                var coords = node.localPosition;
                var thisLightValue = node.chunkData.GetLight(coords);

                foreach (var offset in DirectionExtensions.Vectors)
                {
                    var neighbourCoord = coords + offset;
                    if (TryGetPropagateNode(neighbourCoord, node.chunkData, neighbourhood, out var newNode))
                    {
                        if (visited.Contains(newNode))
                        {//skip nodes we've already seen
                            continue;
                        }
                        var neighbourLightValue = newNode.chunkData.GetLight(newNode.localPosition);

                        var (_, absorption) = voxelTypeManager.GetLightProperties(newNode.chunkData[newNode.localPosition]);
                        var next = thisLightValue.Dynamic - absorption;

                        if (neighbourLightValue.Dynamic < next)
                        {
                            neighbourLightValue.Dynamic = next;
                            newNode.chunkData.SetLight(newNode.localPosition, neighbourLightValue);
                            queue.Enqueue(newNode);
                        }
                    }

                    processed++;
                }

                visited.Add(node);
            }

            if (processed > 0)
            {
                Debug.Log($"Propagation processed {processed}");
            }
            Profiler.EndSample();
        }

        private void PropagateSunlight(ChunkNeighbourhood neighbourhood, Queue<PropagationNode> queue)
        {
            Profiler.BeginSample("PropagateSun");

            HashSet<Vector3> visited = new HashSet<Vector3>();

            int processed = 0;//TODO remove DEBUG
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                var coords = node.localPosition;
                var thisLightValue = node.chunkData.GetLight(coords);

                foreach (var offset in DirectionExtensions.Vectors)
                {
                    var neighbourWorldPos = node.worldPos + offset;
                    if (visited.Contains(neighbourWorldPos))
                    {//skip nodes we've already seen
                        continue;
                    }
                    var neighbourCoord = coords + offset;

                    if (TryGetPropagateNode(neighbourCoord, node.chunkData, neighbourhood, out var newNode))
                    {
                        var neighbourLightValue = newNode.chunkData.GetLight(newNode.localPosition);

                        var (_, absorption) = voxelTypeManager.GetLightProperties(newNode.chunkData[newNode.localPosition]);
                        var next = thisLightValue.Sun - absorption;

                        if (offset.y == -1 && absorption == 1 && thisLightValue.Sun == LightValue.MaxIntensity)
                        {
                            next = LightValue.MaxIntensity;//Ignore normal light absorption when propagating sunlight down
                        }

                        if (neighbourLightValue.Sun < next)
                        {
                            neighbourLightValue.Sun = next;
                            newNode.chunkData.SetLight(newNode.localPosition, neighbourLightValue);
                            queue.Enqueue(newNode);
                        }
                    }

                    processed++;
                }

                visited.Add(node.worldPos);
            }

            if (processed > 0)
            {
                Debug.Log($"Sun Propagation processed {processed}");
            }
            Profiler.EndSample();
        }

        /// <summary>
        /// Returns true if the chunk is the center of the neighbourhood, or is
        /// fully generated
        /// </summary>
        /// <returns></returns>
        private bool ChunkWritable(Vector3Int chunkId, ChunkNeighbourhood neighbourhood)
        {
            if (chunkId.Equals(neighbourhood.center.ChunkID))
            {
                return true;
            }
            return chunkManager.IsChunkFullyGenerated(chunkId);// && InsideCuboid(chunkId,neighbourhood.center.ChunkID,Vector3Int.one);
        }

        private bool TryGetPropagateNode(Vector3Int localPosition, IChunkData chunkData, ChunkNeighbourhood neighbourhood, out PropagationNode node)
        {
            Profiler.BeginSample("TryGetPropagateNode");
            var chunkId = chunkData.ChunkID;
            AdjustForBounds(ref localPosition, ref chunkId, chunkData.Dimensions);
            if (chunkId.Equals(chunkData.ChunkID))
            {
                node = new PropagationNode()
                {
                    localPosition = localPosition,
                    chunkData = chunkData,
                    worldPos = chunkManager.ChunkToWorldPosition(chunkId) + localPosition
                };
                Profiler.EndSample();
                return true;
            }
            else if (ChunkWritable(chunkId, neighbourhood))
            {
                node = new PropagationNode()
                {
                    localPosition = localPosition,
                    chunkData = neighbourhood.GetChunkData(chunkId),
                    worldPos = chunkManager.ChunkToWorldPosition(chunkId) + localPosition
                };
                Profiler.EndSample();
                return true;
            }
            //Otherwise, this position is not valid         
            node = default;
            Profiler.EndSample();
            return false;
        }

        private bool TryGetRemovalNode(Vector3Int localPosition, IChunkData chunkData, ChunkNeighbourhood neighbourhood, out RemovalNode node)
        {
            var chunkId = chunkData.ChunkID;
            AdjustForBounds(ref localPosition, ref chunkId, chunkData.Dimensions);
            if (chunkId.Equals(chunkData.ChunkID))
            {
                node = new RemovalNode()
                {
                    localPosition = localPosition,
                    chunkData = chunkData,
                    lv = chunkData.GetLight(localPosition)
                };
                return true;
            }
            else if (ChunkWritable(chunkId, neighbourhood))
            {
                var newChunkData = neighbourhood.GetChunkData(chunkId);
                node = new RemovalNode()
                {
                    localPosition = localPosition,
                    chunkData = newChunkData,
                    lv = newChunkData.GetLight(localPosition)
                };
                return true;
            }
            //Otherwise, this position is not valid         
            node = default;
            return false;
        }

        private struct PropagationNode:IEquatable<PropagationNode>
        {
            public Vector3Int localPosition;
            public IChunkData chunkData;
            public Vector3 worldPos;//TODO incorporate in equality, DEBUG

            public bool Equals(PropagationNode other)
            {
                return chunkData == other.chunkData && localPosition.Equals(other.localPosition);
            }
            public override int GetHashCode()
            {
                //REF SRC: https://stackoverflow.com/questions/7813687/right-way-to-implement-gethashcode-for-this-struct
                unchecked // Overflow is fine, just wrap
                {
                    int hash = 17;//prime numbers!
                    hash = hash * 23 + localPosition.GetHashCode();
                    hash = hash * 23 + chunkData.ChunkID.GetHashCode();
                    return hash;
                }
            }
        }

        private struct RemovalNode : IEquatable<RemovalNode>
        {
            public Vector3Int localPosition;
            public IChunkData chunkData;
            public LightValue lv;

            public bool Equals(RemovalNode other)
            {
                return chunkData == other.chunkData && localPosition.Equals(other.localPosition)
                    && lv.Equals(other.lv);
            }

            public override int GetHashCode()
            {
                //REF SRC: https://stackoverflow.com/questions/7813687/right-way-to-implement-gethashcode-for-this-struct
                unchecked // Overflow is fine, just wrap
                {
                    int hash = 17;//prime numbers!
                    hash = hash * 23 + localPosition.GetHashCode();
                    hash = hash * 23 + chunkData.ChunkID.GetHashCode();
                    hash = hash * 23 + lv.GetHashCode();
                    return hash;
                }
            }
        }
    }
}