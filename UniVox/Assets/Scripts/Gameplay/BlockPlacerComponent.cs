using UnityEngine;
using System.Collections;
using UnityEngine.Assertions;
using UnityEngine.UIElements;
using UniVox.Framework;

namespace UniVox.Gameplay
{
    public class BlockPlacerComponent : MonoBehaviour
    {
        public float MaxPlacementDistance = 10;
        public MouseButton BreakButton = MouseButton.LeftMouse;
        public MouseButton PlaceButton = MouseButton.RightMouse;
        public MouseButton PickButton = MouseButton.MiddleMouse;

        public GameObject IndicatorPrefab;
        public SOVoxelTypeDefinition blockToPlace;

        private VoxelWorldInterface WorldInterface;
        private GameObject Indicator;

        private void Start()
        {

            WorldInterface = FindObjectOfType<VoxelWorldInterface>();

            Indicator = Instantiate(IndicatorPrefab);
            Indicator.transform.parent = transform;
            Indicator.transform.position = transform.position;
            Indicator.SetActive(false);
            //Ensure the indicator is rendered after other transparent materials
            Indicator.GetComponent<MeshRenderer>().material.renderQueue++;

            Assert.IsNotNull(WorldInterface, "A BlockPlacer must have a reference to a VoxelWorldInterface to operate");

        }

        private void Update()
        {
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            //Ray ray = new Ray(transform.position, Camera.main.transform.forward);
            RaycastHit raycastHit;
            bool hitAnything = Physics.Raycast(ray, out raycastHit, MaxPlacementDistance, LayerMask.GetMask("Voxels"));

            if (hitAnything)
            {
                Indicator.SetActive(true);
                Indicator.transform.position = WorldInterface.CenterOfVoxelAt(raycastHit.point + -0.1f * raycastHit.normal);
                Indicator.transform.rotation = Quaternion.Euler(0, 0, 0);
            }
            else
            {
                Indicator.SetActive(false);
                return;
            }

            //Delete block
            if (Input.GetMouseButtonDown((int)BreakButton))
            {
                if (hitAnything)
                {
                    WorldInterface.RemoveVoxel(raycastHit.point + -0.1f * raycastHit.normal);
                }
            }


            //Place block
            if (Input.GetMouseButtonDown((int)PlaceButton))
            {
                if (hitAnything)
                {
                    WorldInterface.PlaceVoxel(raycastHit.point + 0.1f * raycastHit.normal, blockToPlace);
                }
            }

            if (Input.GetMouseButtonDown((int)PickButton))
            {
                if (hitAnything)
                {
                    if (WorldInterface.TryGetVoxelType(raycastHit.point + -0.1f * raycastHit.normal,out var voxelType))
                    {
                        blockToPlace = voxelType;
                    }
                }
            }



        }

    }
}