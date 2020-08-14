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

        /// <summary>
        /// Holds one entry for each of the 6 cardinal directions,
        /// used to determine if neighbour chunks need remeshing.
        /// </summary>
        public NativeArray<bool> lightsChangedOnBorder;

        private int dx;
        private int dxdy;
        private int3 maxIndices;

        public void Dispose()
        {
            data.Dispose();
            dynamicPropagationQueue.Dispose();
            sunlightPropagationQueue.Dispose();

            sunlightNeighbourUpdates.Dispose();
            dynamicNeighbourUpdates.Dispose();

            lightsChangedOnBorder.Dispose();
        }

        public void Execute()
        {
            dx = data.dimensions.x;
            dxdy = data.dimensions.x * data.dimensions.y;
            maxIndices = data.dimensions - 1;

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
                            NotifyBordersOfUpdatedValue(childCoords);
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
                            NotifyBordersOfUpdatedValue(childCoords);
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

        /// <summary>
        /// Checks whether the coords are on any of the chunk borders,
        /// and notifies those borders that a value has changed.
        /// Does not bother to notify borders for directions which are not valid,
        /// these chunks will pick up the information later if and when they become valid.
        /// </summary>
        /// <param name="coords"></param>
        private void NotifyBordersOfUpdatedValue(int3 coords)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                Direction positiveAxisDirection;
                Direction negativeAxisDirection;
                if (axis == 0)
                {
                    positiveAxisDirection = Direction.east;
                    negativeAxisDirection = Direction.west;
                }
                else if (axis == 1)
                {
                    positiveAxisDirection = Direction.up;
                    negativeAxisDirection = Direction.down;
                }
                else
                {
                    positiveAxisDirection = Direction.north;
                    negativeAxisDirection = Direction.south;
                }

                if (coords[axis] == 0 && data.directionsValid[(int)negativeAxisDirection])
                {
                    lightsChangedOnBorder[(int)negativeAxisDirection] = true;
                }
                else if (coords[axis] == maxIndices[axis] && data.directionsValid[(int)positiveAxisDirection])
                {
                    lightsChangedOnBorder[(int)positiveAxisDirection] = true;
                }
            }
        }
    }
}