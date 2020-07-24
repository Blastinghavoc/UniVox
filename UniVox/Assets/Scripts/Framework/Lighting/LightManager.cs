using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UniVox.Framework.Common;
using static Utils.Helpers;

namespace UniVox.Framework.Lighting
{
    public class LightManager : ILightManager
    {
        private IVoxelTypeManager voxelTypeManager;
        private IChunkManager chunkManager;
        private Vector3Int chunkDimensions;
        public void Initialise(IChunkManager chunkManager, IVoxelTypeManager voxelTypeManager)
        {
            this.chunkManager = chunkManager;
            this.voxelTypeManager = voxelTypeManager;
            chunkDimensions = chunkManager.ChunkDimensions;
        }

        public void OnChunkFullyGenerated(ChunkNeighbourhood neighbourhood, int[] heightMap)
        {
            Profiler.BeginSample("LightOnChunkGenerated");

            Queue<PropagationNode> propagateQueue = new Queue<PropagationNode>();

            //TODO compute sunlight from above chunk if it's loaded

            Queue<PropagationNode> sunlightQueue = new Queue<PropagationNode>();

            var aboveChunkId = neighbourhood.center.ChunkID + Vector3Int.up;
            var yMax = chunkDimensions.y - 1;
            var centerWorldPos = chunkManager.ChunkToWorldPosition(neighbourhood.center.ChunkID).ToInt();

            if (ChunkWritable(aboveChunkId, neighbourhood))
            {
                Profiler.BeginSample("ReadFromAbove");
                //Above chunk is available, get the sunlight levels from its bottom border
                var aboveChunk = neighbourhood.GetChunkData(aboveChunkId);
                int y = 0;

                for (int z = 0; z < chunkDimensions.z; z++)
                {
                    for (int x = 0; x < chunkDimensions.x; x++)
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
                            var localPos = new Vector3Int(x, yMax, z);
                            sunlightQueue.Enqueue(new PropagationNode()
                            {
                                localPosition = localPos,
                                chunkData = neighbourhood.center,
                                worldPos = centerWorldPos+ localPos
                            });;
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
                var chunkTop = chunkPosition.y + chunkDimensions.y;
                int mapIndex = 0;                

                for (int z = 0; z < chunkDimensions.z; z++)
                {
                    for (int x = 0; x < chunkDimensions.x; x++, mapIndex++)
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
                                var localPos = new Vector3Int(x, yMax, z);
                                sunlightQueue.Enqueue(new PropagationNode()
                                {
                                    localPosition = localPos,
                                    chunkData = neighbourhood.center,
                                    worldPos = centerWorldPos + localPos
                                });
                            }
                        }
                    }
                }
                Profiler.EndSample();
            }

            PropagateSunlight(neighbourhood, sunlightQueue);

            Profiler.BeginSample("CheckForDynamicSources");
            for (int z = 0; z < chunkDimensions.z; z++)
            {
                for (int y = 0; y < chunkDimensions.y; y++)
                {
                    for (int x = 0; x < chunkDimensions.x; x++)
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

            for (int i = 0; i < DirectionExtensions.numDirections; i++)
            {
                Direction dir = (Direction)i;
                var chunkOffset = DirectionExtensions.Vectors[i];

                //Check if chunk is FullyGenerated
                var neighChunkId = neighbourhood.center.ChunkID + chunkOffset;
                if (chunkManager.IsChunkFullyGenerated(neighChunkId))
                {
                    var positionOffset = chunkDimensions * chunkOffset;
                    var neighChunkData = neighbourhood.GetChunkData(neighChunkId);
                    foreach (var neighPos in AllPositionsOnChunkBorder(DirectionExtensions.Opposite[i], chunkDimensions))
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

        private void PropagateSunlightBFS(ChunkNeighbourhood neighbourhood, Queue<PropagationNode> queue)
        {
            Profiler.BeginSample("PropagateSun");

            HashSet<Vector3Int> visited = new HashSet<Vector3Int>();

            int processed = 0;//TODO remove DEBUG
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                var coords = node.localPosition;
                var thisLightValue = node.chunkData.GetLight(coords);

                foreach (var child in GetAllValidChildrenForPropagation(node, visited, neighbourhood))
                {
                    var neighbourLightValue = child.chunkData.GetLight(child.localPosition);

                    var (_, absorption) = voxelTypeManager.GetLightProperties(child.chunkData[child.localPosition]);
                    var next = thisLightValue.Sun - absorption;

                    if ((child.worldPos.y < node.worldPos.y) && absorption == 1 && thisLightValue.Sun == LightValue.MaxIntensity)
                    {
                        next = LightValue.MaxIntensity;//Ignore normal light absorption when propagating sunlight down
                    }

                    if (neighbourLightValue.Sun < next)
                    {
                        neighbourLightValue.Sun = next;
                        child.chunkData.SetLight(child.localPosition, neighbourLightValue);
                        queue.Enqueue(child);
                    }

                    processed++;
                }

                //foreach (var offset in DirectionExtensions.Vectors)
                //{
                //    var neighbourWorldPos = node.worldPos + offset;
                //    if (visited.Contains(neighbourWorldPos))
                //    {//skip nodes we've already seen
                //        continue;
                //    }
                //    var neighbourCoord = coords + offset;

                //    if (TryGetPropagateNode(neighbourCoord, node.chunkData, neighbourhood, out var newNode))
                //    {
                //        var neighbourLightValue = newNode.chunkData.GetLight(newNode.localPosition);

                //        var (_, absorption) = voxelTypeManager.GetLightProperties(newNode.chunkData[newNode.localPosition]);
                //        var next = thisLightValue.Sun - absorption;

                //        if (offset.y == -1 && absorption == 1 && thisLightValue.Sun == LightValue.MaxIntensity)
                //        {
                //            next = LightValue.MaxIntensity;//Ignore normal light absorption when propagating sunlight down
                //        }

                //        if (neighbourLightValue.Sun < next)
                //        {
                //            neighbourLightValue.Sun = next;
                //            newNode.chunkData.SetLight(newNode.localPosition, neighbourLightValue);
                //            queue.Enqueue(newNode);
                //        }
                //    }

                //    processed++;
                //}

                //TODO remove visited set completely
                //visited.Add(node.worldPos);
            }

            if (processed > 0)
            {
                Debug.Log($"Sun Propagation processed {processed}");
            }
            Profiler.EndSample();
        }

        //Attempt to be more efficient by means of scan-lining
        private void PropagateSunlight(ChunkNeighbourhood neighbourhood, Queue<PropagationNode> queue) 
        {
            Profiler.BeginSample("PropagateSunlightSpecial");
            Dictionary<IChunkData, HashSet<Vector3Int>> chunksToLocalPositionsDict = new Dictionary<IChunkData, HashSet<Vector3Int>>();
            Dictionary<IChunkData, HashSet<Vector3Int>> gotFinalValue = new Dictionary<IChunkData, HashSet<Vector3Int>>();
            var nonDownOffsets = new Vector3Int[] 
            { 
                DirectionExtensions.Vectors[(int)Direction.north],
                DirectionExtensions.Vectors[(int)Direction.south],
                DirectionExtensions.Vectors[(int)Direction.east],
                DirectionExtensions.Vectors[(int)Direction.west],
                DirectionExtensions.Vectors[(int)Direction.up],
            };
            while (queue.Count > 0)
            {//propagate sunlight downwards
                var node = queue.Dequeue();
                var chunkData = node.chunkData;
                var parentLocal = node.localPosition;
                var parentLv = chunkData.GetLight(parentLocal);
                var parentWorldPos = node.worldPos;

                while (true) 
                { 
                    //Add all non-down neighbours to dict
                    for (int i = 0; i < nonDownOffsets.Length; i++)
                    {
                        var offset = nonDownOffsets[i];
                        var neighData = chunkData;
                        var neighLocal = parentLocal + offset;
                        var neighChunkId = chunkData.ChunkID;

                        if (!LocalPositionInsideChunkBounds(neighLocal,chunkDimensions))
                        {
                            AdjustForBounds(ref neighLocal, ref neighChunkId, chunkDimensions);
                            if (ChunkWritable(neighChunkId,neighbourhood))
                            {
                                neighData = neighbourhood.GetChunkData(neighChunkId);
                            }
                            else
                            {
                                continue;
                            }
                        }

                        if (chunksToLocalPositionsDict.TryGetValue(neighData, out var set))
                        {
                            set.Add(neighLocal);
                        }
                        else
                        {
                            set = new HashSet<Vector3Int>();
                            set.Add(neighLocal);
                            chunksToLocalPositionsDict.Add(neighData, set);
                        }
                    }

                    //propagate downwards
                    var childLocal = parentLocal;
                    childLocal.y -= 1;
                    var childWorldPos = parentWorldPos;
                    childWorldPos.y -= 1;

                    bool stopHere = false;

                    if (childLocal.y < 0)
                    {
                        stopHere = true;
                        var belowChunkId = chunkData.ChunkID;
                        belowChunkId.y -= 1;
                        if (ChunkWritable(belowChunkId,neighbourhood))
                        {
                            var adjustedChildLocal = childLocal;
                            adjustedChildLocal.y += chunkDimensions.y;
                            var neighChunkData = neighbourhood.GetChunkData(belowChunkId);
                            chunkData = neighChunkData;
                            childLocal = adjustedChildLocal;

                            //Add to queue
                            queue.Enqueue(new PropagationNode()
                            {
                                chunkData = neighChunkData,
                                localPosition = adjustedChildLocal,
                                worldPos = childWorldPos
                            }) ;
                        }
                        else
                        {
                            break;
                        }
                    }                

                    var lv = chunkData.GetLight(childLocal);

                    var (_, absorption) = voxelTypeManager.GetLightProperties(chunkData[childLocal]);
                    var next = parentLv.Sun - absorption;

                    //At this point we are always propagating downwards
                    if (absorption == 1 && parentLv.Sun == LightValue.MaxIntensity)
                    {
                        next = LightValue.MaxIntensity;//Ignore normal light absorption when propagating sunlight down
                        //The child has the max light value, it should not be visited again
                        if (gotFinalValue.TryGetValue(chunkData,out var set))
                        {
                            set.Add(childLocal);
                        }
                        else
                        {
                            set = new HashSet<Vector3Int>();
                            set.Add(childLocal);
                            gotFinalValue.Add(chunkData, set);
                        }
                    }

                    if (lv.Sun < next)
                    {
                        lv.Sun = next;
                        chunkData.SetLight(childLocal, lv);
                    }

                    if (stopHere)
                    {//We've gone as far down as we can in this chunk
                        break;
                    }

                    //Set variables for next loop
                    parentLv = lv;
                    parentLocal = childLocal;
                    parentWorldPos = childWorldPos;
                }
            }

            //Process remaining positions by standard bfs. Queue is clear by now
            Assert.IsTrue(queue.Count == 0);
            foreach (var chunkData in chunksToLocalPositionsDict.Keys)
            {
                var chunkPos = chunkManager.ChunkToWorldPosition(chunkData.ChunkID).ToInt();
                var processSet = chunksToLocalPositionsDict[chunkData];                
                if (gotFinalValue.TryGetValue(chunkData,out var excludeSet))
                {
                    processSet.ExceptWith(excludeSet);
                }
                foreach (var item in processSet)
                {
                    queue.Enqueue(new PropagationNode()
                    {
                        chunkData = chunkData,
                        localPosition = item,
                        worldPos = chunkPos + item
                    });
                }
            }
            PropagateSunlightBFS(neighbourhood, queue);
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

        private IEnumerable<PropagationNode> GetAllValidChildrenForPropagation(PropagationNode parent, HashSet<Vector3Int> visited,ChunkNeighbourhood neighbourhood) 
        {
            foreach (var offset in DirectionExtensions.Vectors) 
            {
                PropagationNode child = parent;
                child.worldPos = parent.worldPos + offset;
                if (!visited.Contains(child.worldPos))
                {
                    var chunkId = child.chunkData.ChunkID;
                    child.localPosition += offset;
                    if (LocalPositionInsideChunkBounds(child.localPosition, chunkDimensions))
                    {
                        yield return child;
                    }
                    else
                    {
                        AdjustForBounds(ref child.localPosition, ref chunkId, chunkDimensions);

                        Profiler.BeginSample("ChunkWritable");
                        var writable = ChunkWritable(chunkId, neighbourhood);
                        Profiler.EndSample();
                        if (writable)
                        {
                            child.chunkData = neighbourhood.GetChunkData(chunkId);
                            yield return child;
                        }
                    }
                }
            }
        }

        private bool TryGetPropagateNode(Vector3Int localPosition, IChunkData chunkData, ChunkNeighbourhood neighbourhood, out PropagationNode node)
        {
            Profiler.BeginSample("TryGetPropagateNode");
            var chunkId = chunkData.ChunkID;
            Profiler.BeginSample("AdjustForBounds");
            AdjustForBounds(ref localPosition, ref chunkId, chunkDimensions);
            Profiler.EndSample();
            if (chunkId.Equals(chunkData.ChunkID))
            {
                node = new PropagationNode()
                {
                    localPosition = localPosition,
                    chunkData = chunkData,
                    worldPos = chunkManager.ChunkToWorldPosition(chunkId).ToInt() + localPosition
                };
                Profiler.EndSample();
                return true;
            }
            else 
            {
                Profiler.BeginSample("ChunkWritable");
                var writable = ChunkWritable(chunkId, neighbourhood);
                Profiler.EndSample();
                if (writable)
                {
                    node = new PropagationNode()
                    {
                        localPosition = localPosition,
                        chunkData = neighbourhood.GetChunkData(chunkId),
                        worldPos = chunkManager.ChunkToWorldPosition(chunkId).ToInt() + localPosition
                    };
                    Profiler.EndSample();
                    return true;
                }
            }
            //Otherwise, this position is not valid         
            node = default;
            Profiler.EndSample();
            return false;
        }

        private bool TryGetRemovalNode(Vector3Int localPosition, IChunkData chunkData, ChunkNeighbourhood neighbourhood, out RemovalNode node)
        {
            var chunkId = chunkData.ChunkID;
            AdjustForBounds(ref localPosition, ref chunkId, chunkDimensions);
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
            public Vector3Int worldPos;//TODO incorporate in equality, DEBUG

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