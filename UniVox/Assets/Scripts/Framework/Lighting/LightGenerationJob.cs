using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UniVox.Framework.Common;
using Utils;
using static UniVox.Framework.Jobified.JobUtils;
using static Utils.Helpers;

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

        public NativeQueue<int3> dynamicPropagationQueue;
        public NativeQueue<int3> sunlightPropagationQueue;

        private int dx;
        private int dxdy;

        public void Execute()
        {
            var yMax = data.dimensions.y - 1;
            var worldPos = data.chunkWorldPos;

            dx = data.dimensions.x;
            dxdy = data.dimensions.x * data.dimensions.y;

            //Check all neighbours for lighting contributions
            CheckBoundaries();

            if (data.directionsValid[(int)Direction.up])
            {
                //Above chunk is available, sunlight values have been obtained from the border.
            }
            else
            {

                //If above chunk not available, guess the sunlight level
                var chunkTop = worldPos.y + data.dimensions.y;
                int mapIndex = 0;

                for (int z = 0; z < data.dimensions.z; z++)
                {
                    for (int x = 0; x < data.dimensions.x; x++, mapIndex++)
                    {
                        var hm = data.heightmap[mapIndex];
                        if (hm < chunkTop)
                        {//Assume this column of voxels can see the sun, as it's above the height map
                            var localPos = new int3(x, yMax, z);
                            var localFlat = Helpers.MultiIndexToFlat(localPos, dx, dxdy);

                            var voxelAtTop = data.voxels[localFlat];
                            var absorption = data.voxelTypeToAbsorptionMap[voxelAtTop];

                            if (absorption < LightValue.MaxIntensity)
                            {
                                var sunlight = LightValue.MaxIntensity;
                                if (absorption > 1)
                                {
                                    sunlight -= absorption;
                                }

                                data.lights[localFlat] = new LightValue() { Sun = sunlight };

                                sunlightPropagationQueue.Enqueue(localPos);
                            }
                        }
                    }
                }
            }


            PropagateSunlight();

            //Check all voxels in the chunk to see if they emit light
            int flat = 0;
            for (int z = 0; z < data.dimensions.z; z++)
            {
                for (int y = 0; y < data.dimensions.y; y++)
                {
                    for (int x = 0; x < data.dimensions.x; x++,flat++)
                    {

                        var emission = data.voxelTypeToEmissionMap[data.voxels[flat]];
  
                        //Check if voxel emits light, add to propagation queue if it does.
                        if (emission > 1)
                        {
                            var lv = data.lights[flat];
                            lv.Dynamic = emission;
                            data.lights[flat] = lv;

                            var pos = new int3(x, y, z);
                            dynamicPropagationQueue.Enqueue(pos);                           
                        }
                    }
                }
            }

            PropagateDynamic();

        }

        /// <summary>
        /// Compute lighting contributions from the borders of all (valid) neighbouring chunks
        /// </summary>
        private void CheckBoundaries()
        {
            for (int i = 0; i < data.directionVectors.Length; i++)
            {
                Direction dir = (Direction)i;
                if (data.directionsValid[(int)dir])
                {
                    var offset = data.directionVectors[i];   

                    StartEndRange xRange = new StartEndRange() { start = 0, end = data.dimensions.x };
                    StartEndRange yRange = new StartEndRange() { start = 0, end = data.dimensions.y };
                    StartEndRange zRange = new StartEndRange() { start = 0, end = data.dimensions.z };

                    switch (dir)
                    {
                        case Direction.up:
                            yRange.start = yRange.end - 1;
                            break;
                        case Direction.down:
                            yRange.end = yRange.start + 1;
                            break;
                        case Direction.north:
                            zRange.start = zRange.end - 1;
                            break;
                        case Direction.south:
                            zRange.end = zRange.start + 1;
                            break;
                        case Direction.east:
                            xRange.start = xRange.end - 1;
                            break;
                        case Direction.west:
                            xRange.end = xRange.start + 1;
                            break;
                        default:
                            throw new ArgumentException($"direction {dir} was not recognised");
                    }


                    for (int z = zRange.start; z < zRange.end; z++)
                    {
                        for (int y = yRange.start; y < yRange.end; y++)
                        {
                            for (int x = xRange.start; x < xRange.end; x++)
                            {
                                var localPos = new int3(x, y, z);
                                var localFlat = MultiIndexToFlat(localPos, dx, dxdy);
                                var localLv = data.lights[localFlat];

                                var neighPos = localPos + offset;
                                var neighLv = GetLightValue(neighPos, data.lights, data.dimensions, data.neighbourData);

                                var voxel = data.voxels[localFlat];
                                var absorption = data.voxelTypeToAbsorptionMap[voxel];

                                //Check dynamic propagation
                                if (neighLv.Dynamic > 1)
                                {
                                    var next = neighLv.Dynamic - absorption;

                                    if (next > localLv.Dynamic)
                                    {
                                        localLv.Dynamic = next;                                        
                                        dynamicPropagationQueue.Enqueue(localPos);
                                    }                                    
                                }

                                //Check sun propagation
                                if (neighLv.Sun > 1)
                                {
                                    var sunlight = neighLv.Sun;

                                    if (absorption < LightValue.MaxIntensity && sunlight > 1)
                                    {
                                        if ((offset.y == 1) && absorption == 1 && sunlight == LightValue.MaxIntensity)
                                        {
                                            //Do nothing, sunlight preserved going downwards
                                        }
                                        else
                                        {
                                            sunlight -= absorption;
                                        }

                                        if (sunlight > localLv.Sun)
                                        {
                                            localLv.Sun = sunlight;
                                            sunlightPropagationQueue.Enqueue(localPos);
                                        }
                                    }
                                }

                                data.lights[localFlat] = localLv;
                            }
                        }
                    }
                }
            }
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
                        //TODO some way of signalling to adjacent chunk that it needs to propagate sunlight
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
                        //TODO some way of signalling to adjacent chunk that it needs to propagate dynamic light
                    }
                }
            }
        }
    }
}