using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UniVox.Framework;
using UniVox.Framework.Jobified;

namespace UniVox.Implementations.ProcGen
{
    public class BiomeDatabaseComponent: MonoBehaviour
    {
        public SOBiomeConfiguration config;
        public NativeBiomeDatabase BiomeDatabase { get; private set; }

        private void Start()
        {
            var typeManager = (VoxelTypeManager)FindObjectOfType(typeof(VoxelTypeManager));
            BiomeDatabase = ConfigToNative(config, typeManager);
        }

        public NativeBiomeDatabase ConfigToNative(SOBiomeConfiguration config,VoxelTypeManager typeManager) 
        {
            List<NativeVoxelRange> allLayersList = new List<NativeVoxelRange>();
            List<StartEnd> biomeLayersList = new List<StartEnd>();
            List<NativeBiomeMoistureDefinition> allMoistureDefsList = new List<NativeBiomeMoistureDefinition>();
            List<NativeElevationZone> allElevationZonesList = new List<NativeElevationZone>();

            Dictionary<SOBiomeDefinition, int> biomeIds = new Dictionary<SOBiomeDefinition, int>(); 

            foreach (var elevationEntry in config.elevationLowToHigh)
            {
                NativeElevationZone elevationZone = new NativeElevationZone();
                elevationZone.maxElevationPercentage = elevationEntry.max;
                elevationZone.moistureLevels = new StartEnd() { start = allMoistureDefsList.Count };

                foreach (var moistureEntry in elevationEntry.moistureLevelsLowToHigh)
                {
                    NativeBiomeMoistureDefinition moistureDef = new NativeBiomeMoistureDefinition();
                    moistureDef.maxMoisturePercentage = moistureEntry.max;

                    if (!biomeIds.TryGetValue(moistureEntry.biomeDefinition,out var id))
                    {                   
                        //Create new biome data
                        id = biomeLayersList.Count;
                        biomeIds.Add(moistureEntry.biomeDefinition,id );

                        Assert.IsTrue(moistureEntry.biomeDefinition.topLayers.Count > 0,$"All biome definitions must have at least one layer,{moistureEntry.biomeDefinition.name} does not");
                        StartEnd layersForThisBiome = new StartEnd() { start = allLayersList.Count };

                        foreach (var layer in moistureEntry.biomeDefinition.topLayers)
                        {
                            NativeVoxelRange nativeLayer = new NativeVoxelRange();
                            nativeLayer.depth = layer.depth;
                            nativeLayer.voxelID = typeManager.GetId(layer.voxelType);
                            allLayersList.Add(nativeLayer);
                        }

                        layersForThisBiome.end = allLayersList.Count;
                        biomeLayersList.Add(layersForThisBiome);
                    }
                    
                    moistureDef.biomeID = id;

                    allMoistureDefsList.Add(moistureDef);
                }

                elevationZone.moistureLevels.end = allMoistureDefsList.Count;
                allElevationZonesList.Add(elevationZone);
            }

            NativeBiomeDatabase biomeDatabase = new NativeBiomeDatabase();
            biomeDatabase.defaultVoxelId = typeManager.GetId(config.defaultVoxelType);
            biomeDatabase.allLayers = new NativeArray<NativeVoxelRange>(allLayersList.ToArray(), Allocator.Persistent);
            biomeDatabase.biomeLayers = new NativeArray<StartEnd>(biomeLayersList.ToArray(), Allocator.Persistent);
            biomeDatabase.allMoistureDefs = new NativeArray<NativeBiomeMoistureDefinition>(allMoistureDefsList.ToArray(), Allocator.Persistent);
            biomeDatabase.allElevationZones = new NativeArray<NativeElevationZone>(allElevationZonesList.ToArray(), Allocator.Persistent);

            return biomeDatabase;
        }

        private void OnDestroy()
        {
            //Dispose of biome database
            BiomeDatabase.allLayers.SmartDispose();
            BiomeDatabase.biomeLayers.SmartDispose();
            BiomeDatabase.allMoistureDefs.SmartDispose();
            BiomeDatabase.allElevationZones.SmartDispose();
        }
    }
}