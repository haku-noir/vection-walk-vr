using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    public Vector3 scale = new Vector3 (-1, 1, 1);
    private Camera cam;

    // Start is called before the first frame update
    void Start()
    {

        cam = this.gameObject.GetComponent<Camera>();
    }

    // Update is called once per frame
    private void OnPreCull()
    {
        cam.ResetWorldToCameraMatrix();
        cam.ResetProjectionMatrix();
        Debug.Log("Before : " + cam.projectionMatrix);
        cam.projectionMatrix = cam.projectionMatrix * Matrix4x4.Scale(scale);
        Debug.Log("After :  " + cam.projectionMatrix);
    }

    private void OnPreRender()
    {
        //if (scale.x * scale.y * scale.z < 0)
        //    GL.invertCulling = true;
    }
    private void OnPostRender()
    {
        GL.invertCulling = false;
    }
}
