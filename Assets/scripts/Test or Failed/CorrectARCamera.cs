using UnityEngine;
using System.Collections;
[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class CorrectARCamera : MonoBehaviour
{
    public Material mat;

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        Graphics.Blit(src, dest, mat);
    }
}
