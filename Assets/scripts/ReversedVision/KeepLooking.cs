using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///  対象を視野中央から左右5°以内に捉えているかを判定するクラス
/// </summary>
public class KeepLooking : MonoBehaviour
{
    public Camera mainCamera;
    public GameObject head;
    /// <summary>
    /// 視野中央に収めるべきターゲット
    /// </summary>
    [Tooltip("視野中央に収めるべきターゲット")]
    public GameObject targetObject;

    /// <summary>
    /// 管理用
    /// </summary>
    private int fixed_count = 0;
    /// <summary>
    /// 視線が逸れた回数
    /// </summary>
    [System.NonSerialized] public int out_count = 0;
    /// <summary>
    /// 視線が逸れていたフレーム数
    /// </summary>
    [System.NonSerialized] public int out_frame = 0;
    private bool out_flag = false;
    [System.NonSerialized] public float target_angle = 0;

    /// <summary>
    /// 視線が逸れているときに警告音を鳴らすときのスクリプト
    /// </summary>
    [Tooltip("視線が逸れているときに警告音を鳴らすときのスクリプト")]
    [SerializeField] Alerm _alerm;
    /// <summary>
    /// 警告音を鳴らすか否か
    /// </summary>
    [Tooltip("警告音を鳴らすか否か")]
    public bool onAlerm = false;

    private void FixedUpdate()
    {
        fixed_count = (fixed_count + 1) % 12;
        print(out_count);
        //print(Mathf.Abs(head.transform.position.x) > 0.2);
        if (!IsLooking())
        {
            out_frame++;
            if (fixed_count > 10)
            {
                //print("miss");
                if (onAlerm)
                {
                    NoticeMissing();
                }
                if (!out_flag)
                {
                    out_count++;
                    out_flag = true;
                }
            }
        }
        else
        {
            //_alerm.audioSource.loop = false;
            if (out_flag)
            {
                out_flag = false;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.timeScale == 0)
        {
            out_count = 0;
            out_flag = false;
        }
    }

    private void NoticeMissing()
    {
        _alerm.PlaySound();
    }

    /// <summary>
    ///  対象を視野中央から左右5°以内に捉えているかを判定する関数（前後反転時注意）
    /// </summary>
    /// <remarks>
    /// 前後反転時に対応していないので要修正（ターゲットを前後反対の位置のものに変更しなければならない）
    /// </remarks>
    bool IsLooking()
    {
        // ターゲットの方向ベクトルを取得
        Vector3 toTarget = targetObject.transform.position - mainCamera.transform.position;
        Vector2 toTarget_projected = new Vector2(toTarget.x, toTarget.z);
        Vector2 fromCamera_projected = new Vector2(mainCamera.transform.forward.x, mainCamera.transform.forward.z);
        // カメラの前方ベクトルとターゲットの方向ベクトルの角度を計算
        target_angle = Vector2.SignedAngle(fromCamera_projected, toTarget_projected);
        print(target_angle);

        // カメラの視野角が半分なので、左右計10°以内に入っているか判定
        if (Mathf.Abs(target_angle) < 5)
        {
            // 左右10°以内に入っている場合の処理
            Debug.Log("Object is within 10 degrees of the center axis.");
            return true;
        }
        else
        {
            // 左右10°以内に入っていない場合の処理
            Debug.Log("Object is not within 10 degrees of the center axis.");
            return false;
        }

        //// カメラの中央に Ray を飛ばす
        //Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        //RaycastHit hit;

        //// Rayが何かに当たったら
        //if (Physics.Raycast(ray, out hit))
        //{
        //    // 当たったオブジェクトのタグが "CheckPoint" の場合
        //    if (hit.collider.CompareTag("CheckPoint"))
        //    {
        //        print("hit");
        //        return true;
        //    }
        //}
        //return false;
    }
}
