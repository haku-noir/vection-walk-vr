using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カメラのターゲットテクスチャを設定する
/// </summary>
public class SetTexture : MonoBehaviour
{
    public Camera camera;
    /// <summary>
    /// ターゲットテクスチャ
    /// </summary>
    RenderTexture rt; 

    // Start is called before the first frame update
    void Start()
    {
        var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.Default : RenderTextureFormat.ARGBHalf;
        rt = new RenderTexture(1920, 1832, 24, format);
        rt.Create();

        camera.targetTexture = rt;
    }

    // Update is called once per frame
    void Update()
    {
        camera.targetTexture = rt;
    }

    // 終了時に解放しておく。
    void OnApplicationQuit()
    {
        Debug.Log("Release");
        rt.Release();
    }
}
