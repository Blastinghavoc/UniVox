using UnityEngine;

public class WireframeRenderer : MonoBehaviour
{
    void OnPreRender()
    {
        GL.wireframe = true;
    }

    void OnPostRender()
    {
        GL.wireframe = false;
    }
}