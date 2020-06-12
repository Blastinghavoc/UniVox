using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System;

namespace UniVox.Framework
{
    public class VoxelTypeManager : MonoBehaviour
    {
        public class VoxelTypeData
        {
            public SOVoxelTypeDefinition definition;
            public float[] zIndicesPerFace;
        }

        public Material VoxelMaterial;

        [SerializeField] private SOVoxelTypeDefinition[] VoxelTypes = new SOVoxelTypeDefinition[1];

        private Dictionary<SOVoxelTypeDefinition, ushort> DefinitionToIDMap;

        private List<VoxelTypeData> typeData;

        //Ensure that default initialisation of a VoxelData instance is Air
        public const ushort AIR_ID = 0;

        public void Initialise()
        {
            if (VoxelTypes.Length < 1)
            {
                return;
            }

            DefinitionToIDMap = new Dictionary<SOVoxelTypeDefinition, ushort>();
            typeData = new List<VoxelTypeData>();

            Debug.Log($"Generating texture array for {VoxelTypes.Length} voxel types");

            Assert.IsTrue(VoxelTypes.Length < ushort.MaxValue - 1, $"Can only have a maximum of {ushort.MaxValue - 1} voxel types");

            Dictionary<Texture2D, int> uniqueTextures = new Dictionary<Texture2D, int>();

            typeData.Add(null);//Add null entry for air

            ushort currentID = AIR_ID + 1;

            int currentZ = 0;

            RectInt commonTexSize = new RectInt(0, 0, VoxelTypes[0].FaceTextures[0].width, VoxelTypes[0].FaceTextures[0].height);


            foreach (var item in VoxelTypes)
            {
                Assert.AreEqual(Directions.NumDirections, item.FaceTextures.Length, $"Voxel type {item.name} does not define a texture for each face");

                DefinitionToIDMap.Add(item, currentID);

                float[] FaceZIndices = new float[Directions.NumDirections];

                //Determine Z indices for each face
                for (int i = 0; i < Directions.NumDirections; i++)
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

                typeData.Add(new VoxelTypeData() { definition = item, zIndicesPerFace = FaceZIndices });

                currentID++;
            }

            CreateTextureArray(uniqueTextures, commonTexSize);
        }

        public VoxelTypeData GetData(ushort voxelTypeID)
        {
            return typeData[voxelTypeID];
        }

        public ushort GetId(SOVoxelTypeDefinition def)
        {
            if (DefinitionToIDMap.TryGetValue(def, out var id))
            {
                return id;
            }
            throw new IndexOutOfRangeException($"No id has been generated for the voxel type {def.DisplayName}");
        }

        public void CreateTextureArray(Dictionary<Texture2D, int> SourceTextures, RectInt commonTexSize)
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
            VoxelMaterial.SetTexture("_MainTex", texture2DArray);

        }
    }
}