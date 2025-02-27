﻿using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;
using UniVox.Framework;
using UniVox.Gameplay.Inventory;
using UniVox.UI;

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

        public Vector3 LocationToPlaceBlock { get; private set; }
        public Vector3 LocationToDeleteBlock { get; private set; }

        [Range(0, 3)]
        public int rotationX;
        [Range(0, 3)]
        public int rotationY;
        [Range(0, 3)]
        public int rotationZ;

        public VoxelWorldInterface WorldInterface { get; private set; }

        public HotbarController hotbar;

        private GameObject Indicator;
        private UIManager UImanager;

        private void Start()
        {

            WorldInterface = FindObjectOfType<VoxelWorldInterface>();
            Assert.IsNotNull(WorldInterface, "A BlockPlacer must have a reference to a VoxelWorldInterface to operate");
            UImanager = FindObjectOfType<UIManager>();
            Assert.IsNotNull(UImanager, "Block placer could not find a UI manager");

            Indicator = Instantiate(IndicatorPrefab);
            Indicator.transform.parent = transform;
            Indicator.transform.position = transform.position;
            Indicator.SetActive(false);
            //Ensure the indicator is rendered after other transparent materials
            Indicator.GetComponent<MeshRenderer>().material.renderQueue++;


        }

        private void Update()
        {
            if (UImanager.CursorInUseByUI)
            {
                return;//Don't run block placement logic when UI is using the cursor.
            }

            blockToPlace = hotbar.Selected;

            Ray ray = Camera.main.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            //Ray ray = new Ray(transform.position, Camera.main.transform.forward);
            RaycastHit raycastHit;
            bool hitAnything = Physics.Raycast(ray, out raycastHit, MaxPlacementDistance, LayerMask.GetMask("Voxels"));

            if (hitAnything)
            {
                Indicator.SetActive(true);
                Indicator.transform.position = WorldInterface.CenterOfVoxelAt(raycastHit.point + -0.1f * raycastHit.normal);
                Indicator.transform.rotation = Quaternion.Euler(0, 0, 0);

                LocationToPlaceBlock = raycastHit.point + 0.1f * raycastHit.normal;
                LocationToDeleteBlock = raycastHit.point + -0.1f * raycastHit.normal;
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
                    WorldInterface.RemoveVoxel(LocationToDeleteBlock);
                }
            }


            //Place block
            if (Input.GetMouseButtonDown((int)PlaceButton))
            {
                if (hitAnything)
                {
                    //Simple rotation calculation based on the normal of the face the player is looking at

                    rotationZ = Mathf.RoundToInt(Vector3.Dot(raycastHit.normal, Vector3.left));
                    if (rotationZ == -1)
                    {
                        rotationZ = 3;
                    }
                    rotationX = Mathf.RoundToInt(Vector3.Dot(raycastHit.normal, Vector3.forward));
                    if (rotationX == -1)
                    {
                        rotationX = 3;
                    }
                    rotationY = 0;

                    var rotation = new VoxelRotation() { x = rotationX, y = rotationY, z = rotationZ };

                    if (blockToPlace.rotationConfiguration != null && blockToPlace.rotationConfiguration.RotationValid(rotation))
                    {
                        WorldInterface.PlaceVoxel(LocationToPlaceBlock, blockToPlace, rotation);
                    }
                    else
                    {
                        WorldInterface.PlaceVoxel(LocationToPlaceBlock, blockToPlace);
                    }
                }
            }

            if (Input.GetMouseButtonDown((int)PickButton))
            {
                if (hitAnything)
                {
                    if (WorldInterface.TryGetVoxelTypeAndID(LocationToDeleteBlock, out var voxelType, out var ID))
                    {
                        hotbar.SetCurrentItem(new InventoryItem() { ID = ID, typeDefinition = voxelType });
                    }
                }
            }

        }

    }
}