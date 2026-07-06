using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// テクスチャにピクセル単位で表示されたランダムドットを運動させるクラス
/// </summary>
/// <remarks>
/// 渡邊さんまたは綿世さんがいろいろな運動を実装しているかも
/// </remarks>
public class QuickRandomDotTextureUI : MonoBehaviour
{
    /// <summary>
    /// テクスチャの幅[pixel]
    /// </summary>
    [Tooltip("テクスチャの幅[pixel]")]
    public int width = 256;
    /// <summary>
    /// テクスチャの高さ[pixel]
    /// </summary>
    [Tooltip("テクスチャの高さ[pixel]")]
    public int height = 256;
    /// <summary>
    /// 表示するドット（黒いピクセル）の数
    /// </summary>
    [Tooltip("表示するドット（黒いピクセル）の数")]
    public int dotCount = 1000;

    // RenderTexture ではピクセル単位で扱えなさそう
    /// <summary>
    /// テクスチャ本体
    /// </summary>
    private Texture2D texture;
    /// <summary>
    /// 色情報を含んだピクセル配列
    /// </summary>
    private Color[] pixels;
    /// <summary>
    /// ピクセル配列の一時保管用
    /// </summary>
    private Color[] pixels_tmp;
    /// <summary>
    /// テクスチャを張り付ける RawImage
    /// </summary>
    private RawImage image;

    /// <summary>
    /// ドットの色
    /// </summary>
    [Tooltip("ドットの色")]
    public Color dotColor = Color.black;
    /// <summary>
    /// 背景色
    /// </summary>
    [Tooltip("背景色")]
    public Color backColor = Color.white;

    /// <summary>
    /// ドットを自動で動かすか
    /// </summary>
    [Tooltip("ドットを自動で動かすか")]
    public bool slide = false;

    /// <summary>
    /// トラッキングされた頭部
    /// </summary>
    [Tooltip("トラッキングされた頭部")]
    public GameObject head;
    /// <summary>
    /// 頭部運動に同期させるか
    /// </summary>
    [Tooltip("頭部運動に同期させるか")]
    public bool headSync = false;
    /// <summary>
    /// 頭部の初期位置
    /// </summary>
    private Vector3 init_head_pos;

    void Start()
    {
        if (head != null)
        {
            init_head_pos = head.transform.localPosition;
        }

        texture = new Texture2D(width, height);
        pixels = new Color[width * height];
        pixels_tmp = new Color[width * height];

        // 背景
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = backColor;
            pixels_tmp[i] = backColor;
        }

        // ランダムなドットを生成
        for (int i = 0; i < dotCount; i++)
        {
            int x = UnityEngine.Random.Range(0, width);
            int y = UnityEngine.Random.Range(0, height);
            pixels[y * width + x] = dotColor;
            pixels_tmp[y * width + x] = dotColor;
        }

        image = GetComponent<RawImage>();
        setTextureToImage();
    }

    void FixedUpdate()
    {

        if (slide)
        {
            slideDots();
        }
        if (headSync && head != null)
        {
            syncHead();
        }
        
    }


    /// <summary>
    /// ドットをスライドさせる（ただピクセル配列のインデックスをずらしていくだけ）
    /// </summary>
    void slideDots()
    {
        for (int i = 0; i < pixels.Length - 1; i++)
        {
            pixels[i] = pixels_tmp[i + 1];
        }
        pixels[pixels.Length - 1] = pixels[0];

        setTextureToImage();

        // 配列のコピーを保持し、ドットの更新時にはこちらを利用する。
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels_tmp[i] = pixels[i];
        }
    }

    /// <summary>
    /// HMDとの同期を想定したもの（未完成）
    /// </summary>
    void syncHead()
    {
        Vector3 dif = head.transform.localPosition - init_head_pos;
        Vector2Int difXY = new Vector2Int((int)Math.Ceiling(dif.x * 100), (int)Math.Ceiling(dif.y * 100)); // xyzからxyに変換、単位をcmなどにしないと整数値に変換したときに0や1にしかならない
        
        // HMDに同期させて動かしたいのであれば、xy座標で計算してからインデックスに変換した方が計算がわかりやすいかも。
        for (int i = 0; i < pixels.Length; i++)
        {
            int index = (i + difXY.x + difXY.y * width) % pixels.Length;
            while (index < 0)
            {
                index += pixels.Length;
            }
            pixels[i] = pixels_tmp[index];
        }

        setTextureToImage();
    }

    /// <summary>
    /// Texture2D を RawImage のテクスチャとして設定する
    /// </summary>
    void setTextureToImage()
    {
        texture.SetPixels(pixels);
        texture.Apply();
        // このスクリプトがアタッチされているRaw Imageにテクスチャを設定
        image.texture = texture;
        // 仮に3Dオブジェクトに貼り付けるならこちら↓
        // GetComponent<Renderer>().material.mainTexture = texture;
    }
}
