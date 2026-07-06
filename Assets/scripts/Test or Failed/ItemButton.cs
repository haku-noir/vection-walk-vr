using UnityEngine;

public class ItemButton : MonoBehaviour
{
    private UnityEngine.UI.Button btnItem;

    void Start()
    {
        if (TryGetComponent(out btnItem))
        {
            btnItem.onClick.AddListener(OnClickItemButton);
        }
        else
        {
            Debug.Log("Button 未取得");

            DebugUIBuilder.instance.AddLabel("Button 未取得", DebugUIBuilder.DEBUG_PANE_CENTER);
            DebugUIBuilder.instance.Show();
        }
    }

    /// <summary>
    /// ボタン押下時の処理
    /// </summary>
    public void OnClickItemButton()
    {
        Debug.Log("ボタン押した");

        DebugUIBuilder.instance.AddLabel("ボタン押した", DebugUIBuilder.DEBUG_PANE_CENTER);
        DebugUIBuilder.instance.Show();
    }
}
