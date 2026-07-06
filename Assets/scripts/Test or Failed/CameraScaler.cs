using UnityEngine;

public class CameraScaler : MonoBehaviour
{
    public Vector3 scale = new Vector3(-1, 1, 1);
    private Camera camera;

    void Start()
    {
        camera = GetComponent<Camera>();
        //scale = new Vector3(1, -1, 1);
    }

    void OnPreCull()
    {
        camera.ResetWorldToCameraMatrix();
        camera.ResetProjectionMatrix();
        camera.projectionMatrix = camera.projectionMatrix * Matrix4x4.Scale(scale);
    }

    void OnPreRender()
    {
        if (scale.x * scale.y * scale.z < 0)
        {
            GL.invertCulling = true;
        }
    }

    void OnPostRender()
    {
        GL.invertCulling = false;
    }
}