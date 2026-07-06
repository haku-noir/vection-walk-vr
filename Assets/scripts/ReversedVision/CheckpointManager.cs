using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 円柱の表示・非表示を切り替えるクラス．実験の初期化も兼ねている．
/// </summary>
/// <remarks>
/// メインの管理・設定等は CylinderInteraction クラスで行う．
/// </remarks>
public class CheckpointManager : MonoBehaviour
{
    /// <summary>
    /// 周囲のランダムな位置に現れる円柱にタッチする実験を管理するクラス
    /// </summary>
    [SerializeField] CylinderInteraction cylinderInteraction;
    public bool showCylinder = false;
    private bool show_flag = false;
    private bool destroy_flag = false;
    private GameObject cylinder;

    void Update()
    {
        if (!show_flag && showCylinder)
        {
            setCylinder();
        }

        if (show_flag && !showCylinder)
        {
            destroyCylinder();
        }
    }

    /// <summary>
    /// 球を表示し，実験開始時刻をリセット
    /// </summary>
    void setCylinder()
    {
        cylinderInteraction.enabled = true;
        cylinderInteraction.start_time = Time.time;
        if (destroy_flag)
        {
            cylinder.SetActive(true);
        }

        show_flag = true;
    }

    /// <summary>
    /// 球を非表示にする
    /// </summary>
    void destroyCylinder()
    {
        cylinder = GameObject.Find("Cylinder");
        if (cylinder != null)
        {
            cylinder.SetActive(false);
            destroy_flag = true;
        }
        show_flag = false;
    }

    public void checkFromButton()
    {
        showCylinder = showCylinder ? false : true;
    }
}
