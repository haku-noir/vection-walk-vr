using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 2つの RawImage を透明度を指定して重ね合わせるクラス
/// </summary>
/// <remarks>
/// 3つ以上には非対応
/// </remarks>
public class SetBlendingRatio : MonoBehaviour
{
    [SerializeField] private RawImage[] images;
    [Range(0, 1)] public float transparancy1 = 1f; // 基本的に一方の透明度は1で固定
    [Range(0, 1)] public float transparancy2 = 0.5f; // もう一方の透明度を調整して重ね合わせる

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log(images[0].color);
        Debug.Log(images[1].color);
    }

    // Update is called once per frame
    void Update()
    {
        if (images.Length != 2) {
            print("Only two images can be blended now.");
            return;
        }
        images[0].color = new Color(1, 1, 1, transparancy1);
        images[1].color = new Color(1, 1, 1, transparancy2);
    }
}
