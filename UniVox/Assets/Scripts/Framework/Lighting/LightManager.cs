using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UniVox.Framework.Common;
using UniVox.Framework.Jobified;
using Utils;
using static Utils.Helpers;

namespace UniVox.Framework.Lighting
{
    public class LightManager : ILightManager, IDisposable
    {
        private IVoxelTypeManager voxelTypeManager;
        private IChunkManager chunkManager;
        private Vector3Int chunkDimensions;
        public bool Parallel { get; set; } = false;

        public NativeArray<int> voxelTypeToEmissionMap;
        public NativeArray<int> voxelTypeToAbsorptionMap;
        public NativeArray<int3> directionVectors;

        public void Initialise(IChunkManager chunkManager, IVoxelTypeManager voxelTypeManager)
        {
            this.chunkManager = chunkManager;
            this.voxelTypeManager = voxelTypeManager;
            chunkDimensions = chunkManager.ChunkDimensions;


            List<int> emissions = new List<int>();
            List<int> absorptions = new List<int>();
            for (ushort i = 0; i <= voxelTypeManager.LastVoxelID; i++)
            {
                var (emission, absorption) = voxelTypeManager.GetLightProperties((VoxelTypeID)i);
                emissions.Add(emission);
                absorptions.Add(absorption);
            }

            voxelTypeToEmissionMap = emissions.ToArray().ToNative(Allocator.Persistent);
            voxelTypeToAbsorptionMap = absorptions.ToArray().ToNative(Allocator.Persistent);

            int3[] dVecs = new int3[DirectionExtensions.numDirections];
            for (int i = 0; i < DirectionExtensions.numDirections; i++)
            {
                dVecs[i] = DirectionExtensions.Vectors[i].ToNative();
            }
            directionVectors = dVecs.ToNative(Allocator.Persistent);
        }

        public void Dispose() 
        {
            voxelTypeToEmissionMap.SmartDispose();
            voxelTypeToAbsorptionMap.SmartDispose();
            directionVectors.SmartDispose();
        }

        //TODO refactor to not use neighbourhood, but use chunkId instead
        public void OnChunkFullyGenerated(ChunkNeighbourhood neighbourhood, int[] heightMap)
        {
            Profiler.BeginSample("LightOnChunkGenerated");

            var chunkId = neighbourhood.center.ChunkID;

            var jobData = getJobData(chunkId);

            var generationJob = new LightGenerationJob() { data = jobData,
                dynamicPropagationQueue = new NativeQueue<int3>(Allocator.Persistent),
                sunlightPropagationQueue = new NativeQueue<int3>(Allocator.Persistent),
                heightmap = heightMap.ToNative()
            };

            //TODO support parallel execution (scheduling)
            generationJob.Run();
            generationJob.Dispose();

            var propagationJob = new LightPropagationJob() { data = jobData,
                sunlightPropagationQueue = generationJob.sunlightPropagationQueue,
                dynamicPropagationQueue = generationJob.dynamicPropagationQueue,
                sunlightNeighbourUpdates = new LightJobNeighbourUpdates(Allocator.Persistent),
                dynamicNeighbourUpdates = new LightJobNeighbourUpdates(Allocator.Persistent)
            };

            propagationJob.Run();
            neighbourhood.center.LightmapFromNative(jobData.lights);
            propagationJob.Dispose();

            ResolveUpdates(propagationJob, chunkId);

            Profiler.EndSample();

            //Update neighbours

        }

        private class UpdateListsPair : IDisposable
        {
            public NativeList<int3> sunlight;
            public NativeList<int3> dynamic;

            public void Dispose()
            {
                sunlight.Dispose();
                dynamic.Dispose();
            }
        }

        private void ResolveUpdates(LightPropagationJob propagationJob, Vector3Int chunkId) 
        {
            Queue<KeyValuePair<Vector3Int, UpdateListsPair>> pending = new Queue<KeyValuePair<Vector3Int, UpdateListsPair>>();
           
            for (Direction dir = 0; (int)dir < DirectionExtensions.numDirections; dir++)
            {
                UpdateListsPair pair = new UpdateListsPair();

                pair.sunlight = propagationJob.sunlightNeighbourUpdates[dir];
                pair.dynamic = propagationJob.dynamicNeighbourUpdates[dir];

                if (pair.dynamic.Length > 0 || pair.sunlight.Length > 0)
                {
                    pending.Enqueue(new KeyValuePair<Vector3Int, UpdateListsPair>(chunkId + DirectionExtensions.Vectors[(int)dir],pair));
                }
                else
                {
                    pair.Dispose();
                }
            }

            //TODO process the queue by issuing propagation jobs
            while (pending.Count > 0)
            {
                var kvp = pending.Dequeue();
                kvp.Value.Dispose();//TODO tmp
            }
        }

        private LightJobData getJobData(Vector3Int chunkId) 
        {
            NativeArray<bool> directionsValid = new NativeArray<bool>(DirectionExtensions.numDirections, Allocator.Persistent);
            for (int i = 0; i < DirectionExtensions.numDirections; i++)
            {
                var offset = DirectionExtensions.Vectors[i];
                var neighId = chunkId + offset;
                directionsValid[i] = chunkManager.IsChunkFullyGenerated(neighId);
            }

            var chunkData = chunkManager.GetChunkData(chunkId);

            LightJobData jobData = new LightJobData(chunkId.ToNative(),
                chunkManager.ChunkToWorldPosition(chunkId).ToInt().ToNative(),
                chunkDimensions.ToNative(),
                chunkData.ToNative(Allocator.Persistent),
                chunkData.LightToNative(Allocator.Persistent),
                JobUtils.CacheNeighbourData(chunkId, chunkManager),
                directionsValid,
                voxelTypeToEmissionMap,
                voxelTypeToAbsorptionMap,
                directionVectors
                );
            return jobData;
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

            int processed = 0;//TODO remove DEBUG
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                var coords = node.localPosition;
                var thisLightValue = node.chunkData.GetLight(coords);

                //TODO use this technique in propagating dynamic light too?
                foreach (var child in GetAllValidChildrenForPropagation(node, neighbourhood))
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

        //TODO decrease GC allocations in this method
        private IEnumerable<PropagationNode> GetAllValidChildrenForPropagation(PropagationNode parent,ChunkNeighbourhood neighbourhood) 
        {
            foreach (var offset in DirectionExtensions.Vectors) 
            {
                PropagationNode child = parent;
                child.worldPos = parent.worldPos + offset;
                
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