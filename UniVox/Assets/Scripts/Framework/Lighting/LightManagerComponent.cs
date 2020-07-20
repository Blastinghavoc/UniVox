using UnityEngine;

namespace UniVox.Framework.Lighting
{
    public class LightManagerComponent : MonoBehaviour
    {
        public string GlobalLightName;
        [Range(0,1)]
        public float GlobalLightValue;

        private LightManager lightManager;

        private void Awake()
        {
            lightManager = new LightManager();
        }

        // Start is called before the first frame update
        void Start()
        {
            //Shader.SetGlobalFloat(GlobalLightName, GlobalLightValue);
        }

        // Update is called once per frame
        void Update()
        {
            Shader.SetGlobalFloat(GlobalLightName, GlobalLightValue);
        }       
    }
}