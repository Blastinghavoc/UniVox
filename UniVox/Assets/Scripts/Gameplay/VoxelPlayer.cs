using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UniVox.Framework;

namespace UniVox.Gameplay
{
    [RequireComponent(typeof(Rigidbody))]
    public class VoxelPlayer : MonoBehaviour, IVoxelPlayer
    {
        [SerializeField] private Vector3 StartLocation = new Vector3(0.5f, 0, 0.5f);
        private VoxelWorldInterface WorldInterface;
        [SerializeField] private GameObject underwaterOverlay = null;
        [SerializeField] private SOVoxelTypeDefinition waterType = null;
        [SerializeField] private new Rigidbody rigidbody = null;
        private Camera playercam;
        private Vector3 prevCamPos;
        private Vector3 camPos;

        public Vector3 Position { get => rigidbody.position; set => rigidbody.position = value; }


        private void Start()
        {
            Position = StartLocation;
            WorldInterface = FindObjectOfType<VoxelWorldInterface>();
            Assert.IsNotNull(WorldInterface, $"A {typeof(VoxelPlayer)} must have a reference to a VoxelWorldInterface to operate");

            Assert.IsNotNull(waterType, $"A {typeof(VoxelPlayer)} must have a reference to a water type to operate");
            Assert.IsNotNull(underwaterOverlay, $"A {typeof(VoxelPlayer)} must have a reference to an underwater overlay to operate");
            playercam = Camera.main;
            camPos = playercam.transform.position;
            rigidbody.useGravity = false;
            StartCoroutine(SpawnpointFinder());
        }

        private IEnumerator SpawnpointFinder()
        {
            //Wait for chunk to have data
            while (!WorldInterface.IsChunkFullyGenerated(WorldInterface.WorldToChunkPosition(rigidbody.position)))
            {
                yield return null;
            }

            while (WorldInterface.TryGetVoxelType(rigidbody.position, out var voxelType) && voxelType != null)
            {
                rigidbody.MovePosition(rigidbody.position + Vector3.up);
                yield return null;
                //wait for chunk to have data
                while (!WorldInterface.IsChunkFullyGenerated(WorldInterface.WorldToChunkPosition(rigidbody.position)))
                {
                    yield return null;
                }
            }
            rigidbody.useGravity = true;
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
            prevCamPos = camPos;
            camPos = playercam.transform.position;

            if (camPos != prevCamPos)
            {
                //Moved
                if (WorldInterface.TryGetVoxelType(camPos, out var voxelType))
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