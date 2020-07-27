using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UniVox.Framework.Common;
using static UniVox.Framework.Jobified.JobUtils;
using static Utils.Helpers;

namespace UniVox.Framework.Lighting
{
    [BurstCompile]
    public struct LightPropagationJob : IJob, ILightPropagationJob, IDisposable
    {
        public LightJobData data;

        public NativeQueue<int3> dynamicPropagationQueue { get; set; }
        public NativeQueue<int3> sunlightPropagationQueue { get; set; }

        public LightJobNeighbourUpdates sunlightNeighbourUpdates { get; set; }
        public LightJobNeighbourUpdates dynamicNeighbourUpdates { get; set; }        

        private int dx;
        private int dxdy;

        public void Dispose()
        {
            data.Dispose();
            dynamicPropagationQueue.Dispose();
            sunlightPropagationQueue.Dispose();
        }

        public void Execute()
        {
            dx = data.dimensions.x;
            dxdy = data.dimensions.x * data.dimensions.y;
            PropagateSunlight();
            PropagateDynamic();
        }

        private void PropagateSunlight()
        {
            while (sunlightPropagationQueue.Count > 0)
            {
                var parentCoords = sunlightPropagationQueue.Dequeue();
                var parentFlat = MultiIndexToFlat(parentCoords, dx, dxdy);
                var parentLV = data.lights[parentFlat];

                for (int i = 0; i < data.directionVectors.Length; i++)
                {
                    var offset = data.directionVectors[i];

                    var childCoords = parentCoords + offset;

                    if (LocalPositionInsideChunkBounds(childCoords, data.dimensions))
                    {
                        //Propagate sunlight
                        var childFlat = MultiIndexToFlat(childCoords, dx, dxdy);
                        var childLV = data.lights[childFlat];

                        var absorption = data.voxelTypeToAbsorptionMap[data.voxels[childFlat]];

                        var next = parentLV.Sun - absorption;

                        if ((offset.y == -1) && absorption == 1 && parentLV.Sun == LightValue.MaxIntensity)
                        {
                            next = LightValue.MaxIntensity;//Ignore normal light absorption when propagating sunlight down
                        }

                        if (childLV.Sun < next)
                        {
                            childLV.Sun = next;
                            data.lights[childFlat] = childLV;
                            sunlightPropagationQueue.Enqueue(childCoords);
                        }
                    }
                    else
                    {
                        //Determine if sunlight needs to be propagated into neighbour                    

                        if (!data.directionsValid[i])
                        {
                            continue;//No need to signal a chunk that is not valid for updates
                        }

                        var childLV = GetLightValue(childCoords, data.lights, data.dimensions, data.neighbourData);

                        var absorption = data.voxelTypeToAbsorptionMap[GetVoxel(childCoords, data.voxels, data.dimensions, data.neighbourData)];

                        var next = parentLV.Sun - absorption;

                        if ((offset.y == -1) && absorption == 1 && parentLV.Sun == LightValue.MaxIntensity)
                        {
                            next = LightValue.MaxIntensity;//Ignore normal light absorption when propagating sunlight down
                        }

                        if (childLV.Sun < next)
                        {
                            //Signals the adjacent chunk that this position needs updating
                            sunlightNeighbourUpdates[(Direction)i].Add(ModuloChunkDimensions(childCoords, data.dimensions));
                        }
                    }
                }
            }

        }

        private void PropagateDynamic()
        {
            while (dynamicPropagationQueue.Count > 0)
            {
                var parentCoords = dynamicPropagationQueue.Dequeue();
                var parentFlat = MultiIndexToFlat(parentCoords, dx, dxdy);
                var parentLV = data.lights[parentFlat];

                for (int i = 0; i < data.directionVectors.Length; i++)
                {
                    var offset = data.directionVectors[i];

                    var childCoords = parentCoords + offset;

                    if (LocalPositionInsideChunkBounds(childCoords, data.dimensions))
                    {
                        //Propagate dynamic light
                        var childFlat = MultiIndexToFlat(childCoords, dx, dxdy);
                        var childLV = data.lights[childFlat];

                        var absorption = data.voxelTypeToAbsorptionMap[data.voxels[childFlat]];

                        var next = parentLV.Dynamic - absorption;

                        if (childLV.Dynamic < next)
                        {
                            childLV.Dynamic = next;
                            data.lights[childFlat] = childLV;
                            dynamicPropagationQueue.Enqueue(childCoords);
                        }
                    }
                    else
                    {
                        //Determine if dynamic light needs to be propagated into neighbour                    

                        if (!data.directionsValid[i])
                        {
                            continue;//No need to signal a chunk that is not valid for updates
                        }

                        var childLV = GetLightValue(childCoords, data.lights, data.dimensions, data.neighbourData);

                        var absorption = data.voxelTypeToAbsorptionMap[GetVoxel(childCoords, data.voxels, data.dimensions, data.neighbourData)];

                        var next = parentLV.Dynamic - absorption;

                        if (childLV.Dynamic < next)
                        {
                            //Signals the adjacent chunk that this position needs updating
                            dynamicNeighbourUpdates[(Direction)i].Add(ModuloChunkDimensions(childCoords, data.dimensions));
                        }
                    }
                }
            }
        }        
    }
}