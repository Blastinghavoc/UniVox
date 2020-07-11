using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UniVox.Framework;
using UniVox.Framework.Common;
using Utils;

namespace UniVox.Implementations.ProcGen
{
    public class BiomeDatabaseComponent: MonoBehaviour,IDisposable
    {
        public SOBiomeConfiguration config;
        public NativeBiomeDatabase BiomeDatabase { get; private set; }

        private Dictionary<SOBiomeDefinition, int> biomeIds = new Dictionary<SOBiomeDefinition, int>();
        private List<SOBiomeDefinition> biomeDefinitionsById = new List<SOBiomeDefinition>();

        private bool initialised = false;
        private bool disposed = false;

        public void Initialise() 
        {
            if (!initialised)
            {
                var typeManager = (VoxelTypeManager)FindObjectOfType(typeof(VoxelTypeManager));
                BiomeDatabase = ConfigToNative(config, typeManager);
            }
        }

        public SOBiomeDefinition GetBiomeDefinition(int id) 
        {
            try
            {
                return biomeDefinitionsById[id];
            }
            catch (IndexOutOfRangeException e)
            {
                throw new ArgumentException($"No biomed definition exists for id {id}", e);
            }
        }

        public int GetBiomeID(SOBiomeDefinition def) 
        {
            if (biomeIds.TryGetValue(def,out var id))
            {
                return id;
            }
            throw new ArgumentException($"No id has been generated for biome definition {def.name}");
        }

        public float GetMaxElevationFraction(SOBiomeDefinition def) 
        {
            try
            {
                var zonesContainingDef = config.elevationLowToHigh.Where(
                    (_) => _.moistureLevelsLowToHigh.Any(
                        (moistDef)=>moistDef.biomeDefinition.Equals(def)
                        )
                    );
                return zonesContainingDef.Select((zone) => zone.max).Max();
            }
            catch (Exception e)
            {
                throw new ArgumentException($"A max elevation value could not be found for biome {def.name}",e);
            }            
        }

        public NativeBiomeDatabase ConfigToNative(SOBiomeConfiguration config,VoxelTypeManager typeManager) 
        {
            List<NativeVoxelRange> allLayersList = new List<NativeVoxelRange>();
            List<StartEndRange> biomeLayersList = new List<StartEndRange>();
            List<NativeBiomeMoistureDefinition> allMoistureDefsList = new List<NativeBiomeMoistureDefinition>();
            List<NativeElevationZone> allElevationZonesList = new List<NativeElevationZone>();


            foreach (var elevationEntry in config.elevationLowToHigh)
            {
                NativeElevationZone elevationZone = new NativeElevationZone();
                elevationZone.maxElevationPercentage = elevationEntry.max;
                elevationZone.moistureLevels = new StartEndRange() { start = allMoistureDefsList.Count };

                foreach (var moistureEntry in elevationEntry.moistureLevelsLowToHigh)
                {
                    NativeBiomeMoistureDefinition moistureDef = new NativeBiomeMoistureDefinition();
                    moistureDef.maxMoisturePercentage = moistureEntry.max;

                    if (!biomeIds.TryGetValue(moistureEntry.biomeDefinition,out var id))
                    {                   
                        //Create new biome data
                        id = biomeLayersList.Count;
                        biomeIds.Add(moistureEntry.biomeDefinition,id );
                        biomeDefinitionsById.Add(moistureEntry.biomeDefinition);

                        Assert.IsTrue(moistureEntry.biomeDefinition.topLayers.Count > 0,$"All biome definitions must have at least one layer,{moistureEntry.biomeDefinition.name} does not");
                        StartEndRange layersForThisBiome = new StartEndRange() { start = allLayersList.Count };

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

            VoxelTypeID defaultVoxelType = new VoxelTypeID(VoxelTypeManager.AIR_ID);
            if (config.defaultVoxelType != null)
            {
                defaultVoxelType = typeManager.GetId(config.defaultVoxelType);
            }

            biomeDatabase.defaultVoxelType = defaultVoxelType;

            biomeDatabase.allLayers = new NativeArray<NativeVoxelRange>(allLayersList.ToArray(), Allocator.Persistent);
            biomeDatabase.biomeLayers = new NativeArray<StartEndRange>(biomeLayersList.ToArray(), Allocator.Persistent);
            biomeDatabase.allMoistureDefs = new NativeArray<NativeBiomeMoistureDefinition>(allMoistureDefsList.ToArray(), Allocator.Persistent);
            biomeDatabase.allElevationZones = new NativeArray<NativeElevationZone>(allElevationZonesList.ToArray(), Allocator.Persistent);

            return biomeDatabase;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                //Dispose of biome database
                BiomeDatabase.allLayers.SmartDispose();
                BiomeDatabase.biomeLayers.SmartDispose();
                BiomeDatabase.allMoistureDefs.SmartDispose();
                BiomeDatabase.allElevationZones.SmartDispose();

                disposed = true;
            }
        }
    }
}