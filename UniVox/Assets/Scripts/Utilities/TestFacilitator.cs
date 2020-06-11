using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestFacilitator : MonoBehaviour
{
    public bool TestMode = false;

    void Awake()
    {
        if (TestMode)
        {
            foreach (Transform child in transform)
            {
                //deactivate all world implementations before they can start
                child.gameObject.SetActive(false);
            }
        }
    }

}
