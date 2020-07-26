using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UniVox.Framework.Common;
using UniVox.Framework.Jobified;
using Utils;

namespace UniVox.Framework.Lighting
{
    /// <summary>
    /// Job to generate the lightmap data for a single chunk, 
    /// based on its neighbours if applicable.
    /// </summary>
    [BurstCompile]
    public struct LightGenerationJob : IJob
    {
        public LightJobData data;
        public void Execute()
        {
            NativeQueue<int3> dynamicPropagationQueue = new NativeQueue<int3>(Allocator.Temp);
            NativeQueue<int3> sunlightPropagationQueue = new NativeQueue<int3>(Allocator.Temp);

            var yMax = data.dimensions.y - 1;
            var worldPos = data.chunkWorldPos;

            if (data.directionsValid[(int)Direction.up])
            {
                
                //Above chunk is available, get the sunlight levels from its bottom border
                data.neighbourData.
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
                                worldPos = worldPos + localPos
                            }); ;
                        }
                    }
                }
                
            }
            else
            {
                
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
                                    worldPos = worldPos + localPos
                                });
                            }
                        }
                    }
                }
            }

            PropagateSunlight(neighbourhood, sunlightQueue);

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

            CheckBoundaries(neighbourhood, propagateQueue);

            //Run propagation, but only in FullyGenerated chunks
            PropagateDynamic(neighbourhood, propagateQueue);


        }
    }

    [BurstCompile]
    public struct LightJobData : IDisposable
    {
        [ReadOnly] public int3 chunkId;
        [ReadOnly] public int3 chunkWorldPos;
        [ReadOnly] public int3 dimensions;//Dimensions of chunk

        [ReadOnly] public NativeArray<VoxelTypeID> voxels;//Voxel data
        public NativeArray<LightValue> lights;//per-voxel light data

        [ReadOnly] public NeighbourData neighbourData;//neighbour voxel and light data
        [ReadOnly] public NativeArray<bool> directionsValid;//for each neighbour direction, has that chunk been fully generated yet

        [ReadOnly] public NativeArray<int> voxelTypeToEmissionMap;
        [ReadOnly] public NativeArray<int> voxelTypeToAbsorptionMap;

        public void Dispose()
        {
            voxels.SmartDispose();
            lights.SmartDispose();
            neighbourData.Dispose();
            directionsValid.SmartDispose();
            //The voxelTypeToX maps are externally owned, so not disposed here
        }
    }
}