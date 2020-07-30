using Newtonsoft.Json.Bson;
using PerformanceTesting;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
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
        private IHeightMapProvider heightMapProvider;
        private Vector3Int chunkDimensions;
        public bool Parallel { get; set; } = false;
        public int MaxLightUpdates { get; set; } = 48;

        public int MaxChunksGeneratedPerUpdate { get; set; } = 24;

        private NativeArray<int> voxelTypeToEmissionMap;
        private NativeArray<int> voxelTypeToAbsorptionMap;
        private NativeArray<int3> directionVectors;

        private Dictionary<Vector3Int, ChunkUpdateRequest> pendingLightUpdatesByChunk;
        private Queue<Vector3Int> pendingLightUpdatesOrder;
        private HashSet<Vector3Int> chunksWhereLightChangedOnBorderSinceLastUpdate;

        public void Initialise(IVoxelTypeManager voxelTypeManager, IChunkManager chunkManager, IHeightMapProvider heightMapProvider)
        {
            this.chunkManager = chunkManager;
            this.voxelTypeManager = voxelTypeManager;
            this.heightMapProvider = heightMapProvider;
            chunkDimensions = chunkManager.ChunkDimensions;
            pendingLightUpdatesByChunk = new Dictionary<Vector3Int, ChunkUpdateRequest>();
            pendingLightUpdatesOrder = new Queue<Vector3Int>();
            chunksWhereLightChangedOnBorderSinceLastUpdate = new HashSet<Vector3Int>();

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

        /// <summary>
        /// Runs a fixed number of lighting propagation updates,
        /// and returns a set of the chunk ids alterred by these updates.
        /// </summary>
        /// <returns></returns>
        public HashSet<Vector3Int> Update() 
        {
            Profiler.BeginSample("LightManagerUpdate");
            //Process a finite number of the pending updates.
            int processedThisUpdate = 0;
            Queue<WipPropagationUpdate> jobsInProgress = new Queue<WipPropagationUpdate>();

            var touchedByUpdate = new HashSet<Vector3Int>(chunksWhereLightChangedOnBorderSinceLastUpdate);
            chunksWhereLightChangedOnBorderSinceLastUpdate.Clear();

            while (processedThisUpdate < MaxLightUpdates && pendingLightUpdatesOrder.Count > 0)
            {
                var chunkId = pendingLightUpdatesOrder.Dequeue();
                var chunkUpdate = pendingLightUpdatesByChunk[chunkId];
                pendingLightUpdatesByChunk.Remove(chunkId);

                if (!chunkManager.IsChunkFullyGenerated(chunkId))
                {                    
                    //skip, as this chunk is no longer valid for updates
                    continue;
                }

                touchedByUpdate.Add(chunkId);
                var data = getJobData(chunkId);

                Assert.IsTrue(chunkUpdate.borderUpdateRequests.Count > 0);

                //Run border resolution jobs for each face update request

                //common queues
                var dynamicPropagationQueue = new NativeQueue<int3>(Allocator.Persistent);
                var sunlightPropagationQueue = new NativeQueue<int3>(Allocator.Persistent);

                BorderResolutionJob[] borderResolutionJobs = new BorderResolutionJob[chunkUpdate.borderUpdateRequests.Count];
 
                for (int i = 0; i < chunkUpdate.borderUpdateRequests.Count; i++)
                {
                    var borderUpdate = chunkUpdate.borderUpdateRequests[i];      

                    BorderResolutionJob borderJob = new BorderResolutionJob()
                    {
                        data = data,
                        dynamicPropagationQueue = dynamicPropagationQueue,
                        sunlightPropagationQueue = sunlightPropagationQueue,
                        dynamicFromBorder = borderUpdate.dynamic.ToNative(Allocator.Persistent),
                        sunlightFromBorder = borderUpdate.sunlight.ToNative(Allocator.Persistent),
                        toDirection = borderUpdate.borderDirection
                    };

                    borderResolutionJobs[i] = borderJob;
                }

                ///Just one propagation job will be run, with propagation queues
                ///derived from all the border update requests
                LightPropagationJob propJob = new LightPropagationJob()
                {
                    data = data,
                    sunlightNeighbourUpdates = new LightJobNeighbourUpdates(Allocator.Persistent),
                    dynamicNeighbourUpdates = new LightJobNeighbourUpdates(Allocator.Persistent),
                    sunlightPropagationQueue = sunlightPropagationQueue,
                    dynamicPropagationQueue = dynamicPropagationQueue,
                    lightsChangedOnBorder = new NativeArray<bool>(DirectionExtensions.numDirections, Allocator.Persistent)
                };

                if (Parallel)
                {
                    WipPropagationUpdate updateJob = new WipPropagationUpdate()
                    {
                        borderJobs = borderResolutionJobs,
                        propJob = propJob
                    };

                    JobHandle handle = new JobHandle();
                    //Chain dependencies of border resolution jobs
                    for (int i = 0; i < borderResolutionJobs.Length; i++)
                    {
                        handle = borderResolutionJobs[i].Schedule(handle);
                    }
                    
                    handle = propJob.Schedule(handle);
                    updateJob.handle = handle;
                    jobsInProgress.Enqueue(updateJob); 
                }
                else
                {
                    for (int i = 0; i < borderResolutionJobs.Length; i++)
                    {
                        borderResolutionJobs[i].Run();
                        borderResolutionJobs[i].Dispose();
                    }

                    propJob.Run();
                    var chunkdata = chunkManager.GetChunkData(data.chunkId.ToBasic());
                    chunkdata.SetLightMap(data.lights.ToArray());

                    ///If any light value changes on any of the borders (not just the ones from which
                    ///propagation started), the corresponding neighbour will need to be re-meshed.
                    for (int i = 0; i < propJob.lightsChangedOnBorder.Length; i++)
                    {
                        var neighbourChunkId = chunkId + DirectionExtensions.Vectors[i];
                        if (propJob.lightsChangedOnBorder[i])
                        {
                            touchedByUpdate.Add(neighbourChunkId);
                        }
                    }

                    QueuePropagationUpdates(propJob);
                    propJob.Dispose();
                }



                processedThisUpdate++;
            }

            Assert.AreEqual(pendingLightUpdatesOrder.Count,pendingLightUpdatesByChunk.Count);

            //Complete the jobs
            while (jobsInProgress.Count > 0)
            {
                var wip = jobsInProgress.Dequeue();
                wip.handle.Complete();

                for (int i = 0; i < wip.borderJobs.Length; i++)
                {
                    wip.borderJobs[i].Dispose();
                }

                var chunkId = wip.propJob.data.chunkId.ToBasic();
                var chunkdata = chunkManager.GetChunkData(chunkId);
                chunkdata.SetLightMap(wip.propJob.data.lights.ToArray());

                ///If any light value changes on any of the borders (not just the ones from which
                ///propagation started), the corresponding neighbour will need to be re-meshed.
                for (int i = 0; i < wip.propJob.lightsChangedOnBorder.Length; i++)
                {
                    var neighbourChunkId = chunkId + DirectionExtensions.Vectors[i];
                    if (wip.propJob.lightsChangedOnBorder[i])
                    {
                        touchedByUpdate.Add(neighbourChunkId);                        
                    }
                }

                QueuePropagationUpdates(wip.propJob);
                wip.propJob.Dispose();
            }

            Profiler.EndSample();
            return touchedByUpdate;
        }

        private struct WipPropagationUpdate
        {
            public BorderResolutionJob[] borderJobs;
            public LightPropagationJob propJob;
            public JobHandle handle;
        }

        public AbstractPipelineJob<LightmapGenerationJobResult> CreateGenerationJob(Vector3Int chunkId) 
        {
            int[] heightMap = heightMapProvider.GetHeightMapForColumn(new Vector2Int(chunkId.x, chunkId.z));

            var jobData = getJobData(chunkId);           

            var generationJob = new LightGenerationJob()
            {
                data = jobData,
                dynamicPropagationQueue = new NativeQueue<int3>(Allocator.Persistent),
                sunlightPropagationQueue = new NativeQueue<int3>(Allocator.Persistent),
                heightmap = heightMap.ToNative()
            };

            var propagationJob = new LightPropagationJob()
            {
                data = jobData,
                sunlightPropagationQueue = generationJob.sunlightPropagationQueue,
                dynamicPropagationQueue = generationJob.dynamicPropagationQueue,
                sunlightNeighbourUpdates = new LightJobNeighbourUpdates(Allocator.Persistent),
                dynamicNeighbourUpdates = new LightJobNeighbourUpdates(Allocator.Persistent),
                lightsChangedOnBorder = new NativeArray<bool>(DirectionExtensions.numDirections,Allocator.Persistent)
            };

            Func<LightmapGenerationJobResult> cleanup = () => {
                var result = new LightmapGenerationJobResult();

                result.lights = jobData.lights.ToArray();
                generationJob.Dispose();

                QueuePropagationUpdates(propagationJob);

                ///If any light value changes on any of the borders 
                ///the corresponding neighbour will need to be re-meshed.
                for (int i = 0; i < propagationJob.lightsChangedOnBorder.Length; i++)
                {
                    var neighbourChunkId = chunkId + DirectionExtensions.Vectors[i];
                    if (propagationJob.lightsChangedOnBorder[i])
                    {
                        chunksWhereLightChangedOnBorderSinceLastUpdate.Add(neighbourChunkId);
                    }
                }

                propagationJob.Dispose();                

                return result;
            };

            if (!Parallel)
            {
                return new BasicFunctionJob<LightmapGenerationJobResult>(() =>
                {
                    generationJob.Run();
                    propagationJob.Run();
                    return cleanup();
                });
            }

            var genHandle = generationJob.Schedule();
            var propHandle = propagationJob.Schedule(genHandle);

            return new PipelineUnityJob<LightmapGenerationJobResult>(propHandle, cleanup);

        }

        private void QueuePropagationUpdates(LightPropagationJob propJob) 
        {
            var chunkId = propJob.data.chunkId.ToBasic();
            for (Direction dir = 0; (int)dir < DirectionExtensions.numDirections; dir++)
            {
                BorderUpdateRequest borderUpdate = new BorderUpdateRequest();
                var UpdateChunkId = chunkId + DirectionExtensions.Vectors[(int)dir];
                borderUpdate.borderDirection = DirectionExtensions.Opposite[(int)dir];
                borderUpdate.sunlight = propJob.sunlightNeighbourUpdates[dir].ToArray();
                borderUpdate.dynamic = propJob.dynamicNeighbourUpdates[dir].ToArray();

                if (borderUpdate.dynamic.Length > 0 || borderUpdate.sunlight.Length > 0)
                {                   

                    if (pendingLightUpdatesByChunk.TryGetValue(UpdateChunkId,out var chunkUpdateRequest))
                    {
                        chunkUpdateRequest.borderUpdateRequests.Add(borderUpdate);
                    }
                    else
                    {
                        chunkUpdateRequest = new ChunkUpdateRequest() { borderUpdateRequests = new List<BorderUpdateRequest>() };
                        chunkUpdateRequest.borderUpdateRequests.Add(borderUpdate);

                        pendingLightUpdatesByChunk.Add(UpdateChunkId, chunkUpdateRequest);
                        pendingLightUpdatesOrder.Enqueue(UpdateChunkId);
                    }
                }
            }
        }

        private class ChunkUpdateRequest 
        {
            public List<BorderUpdateRequest> borderUpdateRequests;
        }

        private class BorderUpdateRequest 
        {            
            public Direction borderDirection;
            public int3[] sunlight;
            public int3[] dynamic;
        }        

        public void ApplyGenerationResult(Vector3Int chunkId, LightmapGenerationJobResult result) 
        {
            var chunkData = chunkManager.GetChunkData(chunkId);
            chunkData.SetLightMap(result.lights);
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

            var chunkData = chunkManager.GetReadOnlyChunkData(chunkId);

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
            for (int i = 0; i < DirectionExtensions.Vectors.Length; i++)
            {
                var offset = DirectionExtensions.Vectors[i];

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
            public Vector3Int worldPos;

            public bool Equals(PropagationNode other)
            {
                return chunkData == other.chunkData && localPosition.Equals(other.localPosition) && worldPos.Equals(other.worldPos);
            }
            public override int GetHashCode()
            {
                //REF SRC: https://stackoverflow.com/questions/7813687/right-way-to-implement-gethashcode-for-this-struct
                unchecked // Overflow is fine, just wrap
                {
                    int hash = 17;//prime numbers!
                    hash = hash * 23 + localPosition.GetHashCode();
                    hash = hash * 23 + chunkData.ChunkID.GetHashCode();
                    hash = hash * 23 + worldPos.GetHashCode();
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