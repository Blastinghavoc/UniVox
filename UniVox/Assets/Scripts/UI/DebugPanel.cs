using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniVox.Framework;

namespace UniVox.UI
{
    public class DebugPanel : MonoBehaviour
    {
        private VoxelWorldInterface world = null;
        private Transform player = null;
        [SerializeField] private GameObject textPrefab = null;
        [SerializeField] private int fontSize = 25;

        private class DebugItem
        {
            public string name = "";
            public string value = "";
            public Text display;

            public DebugItem(string name)
            {
                this.name = name;
            }

            public void Apply()
            {
                display.text = $"{name}: {value}";
            }

            public void Update(string value) 
            {
                this.value = value;
                Apply();
            }
        }

        private Dictionary<string, DebugItem> debugItems = new Dictionary<string, DebugItem>();
        private bool show = true;

        private void Start()
        {
            world = FindObjectOfType<VoxelWorldInterface>();
            player = GameObject.FindGameObjectsWithTag("Player")[0].transform;

            List<DebugItem> items = new List<DebugItem>() {
                new DebugItem("Coords"),
                new DebugItem("ChunkID")
            };

            foreach (var item in items)
            {
                var display = Instantiate(textPrefab, transform).GetComponent<Text>();
                display.fontSize = fontSize;
                item.display = display;
                item.Apply();
                debugItems.Add(item.name, item);
            }

            Toggle();

        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                Toggle();
            }

            if (show)
            {
                debugItems["Coords"].Update(player.position.ToString());
                debugItems["ChunkID"].Update(world.WorldToChunkPosition(player.position).ToString());
            }

        }

        private void Toggle() 
        {
            show = !show;
            foreach (var item in debugItems.Values)
            {
                item.display.gameObject.SetActive(show);
            }
        }
    }
}