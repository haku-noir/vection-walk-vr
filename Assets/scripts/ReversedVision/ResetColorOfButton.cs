using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3つのボタン全ての色を白にするクラス
/// </summary>
public class ResetColorOfButton : MonoBehaviour
{
    private ChangeColorOfButton _changescript;
    [SerializeField] Button button1;
    [SerializeField] Button button2;
    [SerializeField] Button button3;
    [SerializeField] Button resetButton;

    // Start is called before the first frame update
    void Start()
    {
        //reset_button = GetComponent<Button>();
        resetButton.onClick.AddListener(ResetColor);
    }

    /// <summary>
    /// クリック回数が奇数なら（ボタンが白色でないなら）クリック回数を増やす関数
    /// </summary>
    void ResetColor()
    {
        ChangeColorOfButton script = button1.GetComponent<ChangeColorOfButton>();
        if (script.clickCount % 2 != 0)
            script.ToggleColor();
        script = button2.GetComponent<ChangeColorOfButton>();
        if (script.clickCount % 2 != 0)
            script.ToggleColor();
        script = button3.GetComponent<ChangeColorOfButton>();
        if (script.clickCount % 2 != 0)
            script.ToggleColor();
    }

}
