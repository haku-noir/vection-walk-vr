using UnityEngine;
using UnityEngine.UI;

public class RandomDotTextureUI : MonoBehaviour
{
    public int width = 400;
    public int height = 400;
    public int dotCount = 1000;

    void Start()
    {
        Texture2D texture = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];

        // 背景を白にする
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        System.Random rand = new System.Random();

        // ランダムなドットを生成
        for (int i = 0; i < dotCount; i++)
        {
            int x = rand.Next(width);
            int y = rand.Next(height);
            pixels[y * width + x] = Color.black;
        }

        texture.SetPixels(pixels);
        texture.Apply();

        // このスクリプトがアタッチされているRaw Imageにテクスチャを設定
        GetComponent<RawImage>().texture = texture;
    }
}
