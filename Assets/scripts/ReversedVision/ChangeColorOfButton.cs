using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ボタンのクリック回数に応じて色を切り替えるクラス
/// </summary>
public class ChangeColorOfButton : MonoBehaviour
{
    private Button button;
    [System.NonSerialized] public int clickCount = 0;

    private void Start()
    {
        // ボタンコンポーネントを取得
        button = GetComponent<Button>();

        // ボタンにクリック時のイベントリスナーを追加
        button.onClick.AddListener(ToggleColor);
    }

    /// <summary>
    /// クリック回数が奇数なら黄、偶数なら白に設定する関数
    /// </summary>
    public void ToggleColor()
    {
        // クリック回数を増やす
        clickCount++;

        // クリック回数が奇数なら黄、偶数なら白に設定
        Color newColor = (clickCount % 2 == 1) ? Color.yellow : Color.white;
        button.image.color = newColor;
    }

}