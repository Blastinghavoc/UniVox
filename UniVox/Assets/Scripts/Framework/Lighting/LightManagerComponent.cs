using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniVox.Framework.Lighting
{
    public class LightManagerComponent : MonoBehaviour, ILightManager
    {
        public string GlobalLightName;
        [Range(0, 1)]
        public float GlobalLightValue;
        public Light sunLight;
        public Material skyboxMaterial;
        public bool parallel;

        private ILightManager lightManager;

        public void Initialise(IChunkManager chunkManager, IVoxelTypeManager voxelTypeManager)
        {
            var lm = new LightManager();
            lm.Parallel = parallel;
            lightManager = lm;
            lightManager.Initialise(chunkManager,voxelTypeManager);            
        }

        private void OnDestroy()
        {
            if (lightManager is IDisposable disp)
            {
                disp.Dispose();
            }
        }

        public void OnChunkFullyGenerated(ChunkNeighbourhood neighbourhood, int[] heightMap)
        {
            lightManager.OnChunkFullyGenerated(neighbourhood, heightMap);
        }

        public List<Vector3Int> UpdateLightOnVoxelSet(ChunkNeighbourhood neighbourhood, Vector3Int localCoords, VoxelTypeID voxelType, VoxelTypeID previousType)
        {
            return lightManager.UpdateLightOnVoxelSet(neighbourhood, localCoords, voxelType, previousType);
        }

        // Start is called before the first frame update
        void Start()
        {
            //Shader.SetGlobalFloat(GlobalLightName, GlobalLightValue);
        }

        // Update is called once per frame
        void Update()
        {
            sunLight.transform.rotation = Quaternion.Euler(Mathf.Lerp(-90, 90, GlobalLightValue),0,0);
            skyboxMaterial.SetFloat("_Exposure", GlobalLightValue);
            Shader.SetGlobalFloat(GlobalLightName, GlobalLightValue);
        }
    }
}