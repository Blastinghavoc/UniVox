using System;
using System.Collections.Generic;
using UnityEngine;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Framework.Lighting
{
    public class LightManagerComponent : MonoBehaviour, ILightManager
    {
        [SerializeField] private string GlobalLightName = "";
        [Range(0, 1)]
        [SerializeField] private float GlobalLightValue = 1;

        [SerializeField] private ShaderVariable[] shaderVariables = new ShaderVariable[0];

        [Range(-180,180)]
        public float TimeOfDay;

        public Light sunLight;
        public Material skyboxMaterial;
        public bool parallel;
        [Range(0,100)]
        public int MaxGenPerUpdate = 24;
        [Range(0, 400)]
        public int MaxPropPerUpdate = 48;


        private ILightManager lightManager;

        public int MaxChunksGeneratedPerUpdate => MaxGenPerUpdate;

        public void Initialise(IVoxelTypeManager voxelTypeManager, IChunkManager chunkManager,IHeightMapProvider heightMapProvider)
        {
            var lm = new LightManager();
            lm.Parallel = parallel;
            lm.MaxChunksGeneratedPerUpdate = MaxChunksGeneratedPerUpdate;
            lm.MaxLightUpdates = MaxPropPerUpdate;
            lightManager = lm;
            lightManager.Initialise(voxelTypeManager, chunkManager,heightMapProvider);            
        }

        private void OnDestroy()
        {
            if (lightManager is IDisposable disp)
            {
                disp.Dispose();
            }
        }

        public List<Vector3Int> UpdateLightOnVoxelSet(ChunkNeighbourhood neighbourhood, Vector3Int localCoords, VoxelTypeID voxelType, VoxelTypeID previousType)
        {
            return lightManager.UpdateLightOnVoxelSet(neighbourhood, localCoords, voxelType, previousType);
        }

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            sunLight.transform.rotation = Quaternion.Euler(TimeOfDay,0,0);
            GlobalLightValue = Mathf.InverseLerp(-1,1, Mathf.Sin(Mathf.Deg2Rad * TimeOfDay));
            skyboxMaterial.SetFloat("_Exposure", GlobalLightValue);

            Shader.SetGlobalFloat(GlobalLightName, GlobalLightValue);
            for (int i = 0; i < shaderVariables.Length; i++)
            {
                var variable = shaderVariables[i];
                Shader.SetGlobalFloat(variable.name, variable.value);
            }

        }

        HashSet<Vector3Int> ILightManager.Update() 
        {
            return lightManager.Update();
        }

        public void ApplyGenerationResult(Vector3Int chunkId, LightmapGenerationJobResult result)
        {
            lightManager.ApplyGenerationResult(chunkId, result);
        }

        public AbstractPipelineJob<LightmapGenerationJobResult> CreateGenerationJob(Vector3Int chunkId)
        {
            return lightManager.CreateGenerationJob(chunkId);
        }
    }

    [System.Serializable]
    public class ShaderVariable 
    {
        public string name;
        public float value;
    }
}