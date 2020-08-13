using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UniVox.UI
{
    public class ToggleListController : MonoBehaviour 
    {
        public GameObject itemTemplate;

        private List<GameObject> items;
        private List<Text> labels;
        private List<Toggle> toggles;

        public event Action OnChanged = delegate { };

        public void Populate(string[] labelTexts)
        {
            items = new List<GameObject>();
            labels = new List<Text>();
            toggles = new List<Toggle>();
            foreach (var txt in labelTexts)
            {
                var item = Instantiate(itemTemplate, transform);
                item.SetActive(true);
                var label = item.gameObject.GetComponentInChildren<Text>();
                label.text = txt;

                var toggle = item.gameObject.GetComponentInChildren<Toggle>();
                toggle.onValueChanged.AddListener(delegate { OnToggleChanged(); });

                items.Add(item);
                labels.Add(label);
                toggles.Add(toggle);
            }
        }

        private void OnToggleChanged() 
        {
            OnChanged.Invoke();
        }

        /// <summary>
        /// Get the label of the first selected item if there is one
        /// </summary>
        /// <param name="selected"></param>
        /// <returns></returns>
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

        public string[] GetAllSelected() 
        {
            List<string> list = new List<string>();
            for (int i = 0; i < toggles.Count; i++)
            {
                if (toggles[i].isOn)
                {
                    list.Add(labels[i].text);
                }
            }
            return list.ToArray();
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