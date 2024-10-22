﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UniVox.Framework.Common;
using UniVox.Framework.Jobified;

namespace UniVox.Framework
{
    public class VoxelTypeManager : MonoBehaviour, IVoxelTypeManager
    {

        [SerializeField] private SOVoxelTypeDefinition[] VoxelTypes = new SOVoxelTypeDefinition[1];

        private Dictionary<SOVoxelTypeDefinition, VoxelTypeID> DefinitionToIDMap;

        private List<VoxelTypeData> typeData;

        private Material[] materialIdToMaterialMap;

        public ushort LastVoxelID { get => (ushort)(typeData.Count - 1); }

        #region Job Compatibility
        public NativeMeshDatabase nativeMeshDatabase;
        public NativeVoxelTypeDatabase nativeVoxelTypeDatabase;
        #endregion

        private bool disposed = false;

        public void Initialise()
        {
            if (VoxelTypes.Length < 1)
            {
                return;
            }

            DefinitionToIDMap = new Dictionary<SOVoxelTypeDefinition, VoxelTypeID>();
            typeData = new List<VoxelTypeData>();
            typeData.Add(new VoxelTypeData());//Add empty entry for air

            //Debug.Log($"Generating texture array for {VoxelTypes.Length} voxel types");

            Assert.IsTrue(VoxelTypes.Length < ushort.MaxValue - 1, $"Can only have a maximum of {ushort.MaxValue - 1} voxel types");

            var materialIDMap = new Dictionary<Material, ushort>();
            var typesByMaterialID = new List<List<SOVoxelTypeDefinition>>();

            //Process voxel types and split by material used
            ushort currentID = VoxelTypeID.AIR_ID + 1;
            foreach (var item in VoxelTypes)
            {
                Assert.AreEqual(DirectionExtensions.numDirections, item.FaceTextures.Length, $"Voxel type {item.name} does not define a texture for each face");

                DefinitionToIDMap.Add(item, (VoxelTypeID)currentID);

                if (!materialIDMap.TryGetValue(item.material, out var matID))
                {
                    //New material
                    matID = (ushort)materialIDMap.Count;
                    typesByMaterialID.Add(new List<SOVoxelTypeDefinition>());
                    materialIDMap.Add(item.material, matID);
                }

                typesByMaterialID[matID].Add(item);
                typeData.Add(new VoxelTypeData() { definition = item, materialID = matID });

                currentID++;
            }

            RectInt commonTexSize = new RectInt(0, 0, VoxelTypes[0].FaceTextures[0].width, VoxelTypes[0].FaceTextures[0].height);
            //For each unique material, create texture array and record zIndices

            foreach (var pair in materialIDMap)
            {
                var material = pair.Key;
                var types = typesByMaterialID[pair.Value];

                Dictionary<Texture2D, int> uniqueTextures = new Dictionary<Texture2D, int>();
                int currentZ = 0;

                foreach (var item in types)
                {
                    Assert.AreEqual(DirectionExtensions.numDirections, item.FaceTextures.Length, $"Voxel type {item.name} does not define a texture for each face");

                    float[] FaceZIndices = new float[DirectionExtensions.numDirections];

                    //Determine Z indices for each face
                    for (int i = 0; i < DirectionExtensions.numDirections; i++)
                    {
                        var tex = item.FaceTextures[i];
                        if (uniqueTextures.TryGetValue(tex, out var ZIndex))
                        {
                            //Reuse existing texture
                            FaceZIndices[i] = ZIndex;
                        }
                        else
                        {
                            //Add new texture and increment currentZ
                            uniqueTextures.Add(tex, currentZ);
                            FaceZIndices[i] = currentZ;
                            currentZ++;
                        }
                    }

                    typeData[DefinitionToIDMap[item]].zIndicesPerFace = FaceZIndices;

                }

                CreateTextureArray(material, uniqueTextures, commonTexSize);
            }

            materialIdToMaterialMap = new Material[materialIDMap.Count];
            foreach (var item in materialIDMap)
            {
                materialIdToMaterialMap[item.Value] = item.Key;
            }

            InitialiseJobified();
        }

        private void InitialiseJobified()
        {
            nativeMeshDatabase = NativeMeshDatabaseExtensions.FromTypeData(typeData);
            nativeVoxelTypeDatabase = NativeVoxelTypeDatabaseExtensions.FromTypeData(typeData);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                nativeMeshDatabase.Dispose();
                nativeVoxelTypeDatabase.Dispose();
                disposed = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="voxelType"></param>
        /// <returns>EmissionIntensity,DiminishLightAmount</returns>
        public (int, int) GetLightProperties(VoxelTypeID voxelType)
        {
            try
            {
                var def = typeData[voxelType].definition;
                if (def != null)
                {
                    var lightConfig = def.lightConfiguration;
                    if (lightConfig != null)
                    {
                        return (lightConfig.EmissionIntensity, lightConfig.DiminishLightAmount);
                    }
                }
                return (0, 1);
            }
            catch (IndexOutOfRangeException e)
            {
                throw new IndexOutOfRangeException($"No voxel type exists for id {voxelType}", e);
            }
        }

        public VoxelTypeData GetData(ushort voxelTypeID)
        {
            return typeData[voxelTypeID];
        }

        public VoxelTypeID GetId(SOVoxelTypeDefinition def)
        {
            if (DefinitionToIDMap.TryGetValue(def, out var id))
            {
                return id;
            }
            throw new IndexOutOfRangeException($"No id has been generated for the voxel type {def.DisplayName}");
        }

        public SOVoxelTypeDefinition GetDefinition(VoxelTypeID id)
        {
            try
            {
                return typeData[id].definition;
            }
            catch (IndexOutOfRangeException e)
            {
                throw new IndexOutOfRangeException($"No voxel type exists for id {id}", e);
            }
        }

        public Material GetMaterial(ushort materialID)
        {
            try
            {
                return materialIdToMaterialMap[materialID];
            }
            catch (IndexOutOfRangeException e)
            {
                throw new IndexOutOfRangeException($"No material exists with id {materialID}", e);
            }
        }

        private void CreateTextureArray(Material material, Dictionary<Texture2D, int> SourceTextures, RectInt commonTexSize)
        {
            //REF: Based on https://medium.com/@calebfaith/how-to-use-texture-arrays-in-unity-a830ae04c98b

            //NOTE: All source textures must have same dimensions.

            // Create Texture2DArray
            Texture2DArray texture2DArray = new Texture2DArray(
                commonTexSize.width,
                commonTexSize.height,
                SourceTextures.Count,
                TextureFormat.RGBA32,
                true, false);

            // Apply settings
            texture2DArray.filterMode = FilterMode.Point;
            texture2DArray.wrapMode = TextureWrapMode.Repeat;

            //Copy textures to array
            foreach (var pair in SourceTextures)
            {
                texture2DArray.SetPixels(pair.Key.GetPixels(0), pair.Value, 0);
            }

            // Apply changes
            texture2DArray.Apply();

            // Apply the texture to material
            material.SetTexture("_MainTex", texture2DArray);

        }
    }
}