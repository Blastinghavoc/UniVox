using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UniVox.Framework;

namespace UniVox.Gameplay
{

    public class VoxelPlayer : MonoBehaviour,IVoxelPlayer 
    {
        private VoxelWorldInterface WorldInterface;
        [SerializeField] private GameObject underwaterOverlay = null;
        [SerializeField] private SOVoxelTypeDefinition waterType = null;
        private Camera playercam;
        private Vector3 prevPos;
        private Vector3 pos;
        private new Rigidbody rigidbody;

        public Vector3 Position { get => pos; set => pos = value; }

        private void Start()
        {
            WorldInterface = FindObjectOfType<VoxelWorldInterface>();
            Assert.IsNotNull(WorldInterface, $"A {typeof(VoxelPlayer)} must have a reference to a VoxelWorldInterface to operate");

            Assert.IsNotNull(waterType, $"A {typeof(VoxelPlayer)} must have a reference to a water type to operate");
            Assert.IsNotNull(underwaterOverlay, $"A {typeof(VoxelPlayer)} must have a reference to an underwater overlay to operate");
            playercam = Camera.main;
            pos = playercam.transform.position;
            rigidbody = GetComponent<Rigidbody>();
        }

        public void AllowMove(bool allow) 
        {
            if (allow)
            {
                //Remove constraints
                rigidbody.constraints &= ~RigidbodyConstraints.FreezePosition;
            }
            else
            {
                rigidbody.constraints |= RigidbodyConstraints.FreezePosition;
            }                
        }

        private void Update()
        {
            Profiler.BeginSample("VoxelPlayer");
            prevPos = pos;
            pos = playercam.transform.position;

            if (pos!=prevPos)
            {
                //Moved
                if (WorldInterface.TryGetVoxelType(pos,out var voxelType))
                {
                    if (voxelType == waterType)
                    {
                        underwaterOverlay.SetActive(true);
                    }
                    else
                    {
                        underwaterOverlay.SetActive(false);
                    }
                }

            }            

            Profiler.EndSample();
        }
    }
}