using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniVox.Framework.Serialisation;

namespace UniVox.UI
{
    public class SavedGameListController : MonoBehaviour
    {
        public GameObject itemTemplate;

        private List<GameObject> items;
        private List<Text> labels;
        private List<Toggle> toggles;

        public void Populate()
        {
            items = new List<GameObject>();
            labels = new List<Text>();
            toggles = new List<Toggle>();
            foreach (var savename in SaveUtils.GetAllWorldNames())
            {
                var item = Instantiate(itemTemplate, transform);
                item.SetActive(true);
                var label = item.gameObject.GetComponentInChildren<Text>();
                label.text = savename;

                var toggle = item.gameObject.GetComponentInChildren<Toggle>();

                items.Add(item);
                labels.Add(label);
                toggles.Add(toggle);
            }
        }

        public bool TryGetSelected(out string selected)
        {
            selected = string.Empty;
            for (int i = 0; i < toggles.Count; i++)
            {
                if (toggles[i].isOn)
                {
                    selected = labels[i].text;
                    return true;
                }
            }
            return false;
        }

        public void DeleteSelected()
        {
            for (int i = 0; i < toggles.Count; i++)
            {
                if (toggles[i].isOn)
                {
                    labels.RemoveAt(i);
                    toggles.RemoveAt(i);
                    var item = items[i];
                    items.RemoveAt(i);

                    Destroy(item);
                    break;
                }
            }
        }

        public void SetInteractable(bool value) 
        {
            for (int i = 0; i < toggles.Count; i++)
            {
                toggles[i].interactable = value;
            }
        }
    }
}