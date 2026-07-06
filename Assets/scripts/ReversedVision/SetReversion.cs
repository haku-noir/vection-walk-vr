using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 視野反転クラス．
/// 撮影用カメラの映像を UI の RawImage として映し，それを反転させたものを HMD のディスプレイに出力するカメラで見る．
/// </summary>
/// <remarks>
/// カメラ映像を直接反転させようとしたと上手くいかなかったため（ネットに参考になりそうな情報はある），一度 UI としての表示を介している．
/// 「カメラ→（反転）→ディスプレイ」 が理想だが，実装は「カメラ1→UI→（反転）→カメラ2→ディスプレイ」という流れだということ．
/// 現在は両眼視差には対応していない．（ある程度は拡張できるようにしているはずだが，細かい部分は雑なままで，確認すらしていない．）
/// そのため，基本的には "center_" から始まる変数のみ使用している．
/// </remarks>
public class SetReversion : MonoBehaviour
{
    /// <summary>
    /// /上下軸の反転の有無
    /// </summary>
    [Tooltip("上下軸の反転の有無")]
    public bool updown_reversion = false;
    /// <summary>
    /// 左右軸の反転の有無
    /// </summary>
    [Tooltip("左右軸の反転の有無")]
    public bool leftright_reversion = false;
    /// <summary>
    /// 前後軸の反転の有無
    /// </summary>
    [Tooltip("前後軸の反転の有無")]
    public bool frontback_reversion = false;
    
    // 管理用フラグ
    [System.NonSerialized] public bool leftright_flag = false;
    [System.NonSerialized] public bool updown_flag = false;
    [System.NonSerialized] public bool frontback_flag = false;

    private GameObject camerarig;
    private OVRCameraRig rigscript;
    /// <summary>
    /// トラッキングした頭部
    /// </summary>
    /// <remarks>
    /// CenterEyeAnchor などの HMD の運動に同期して動くオブジェクト
    /// </remarks>
    [Tooltip("トラッキングした頭部")]
    private GameObject hmd;

    /// <summary>
    /// 左目に出力する映像を撮影するカメラ（未使用）
    /// </summary>
    [Tooltip("左目に出力する映像を撮影するカメラ")]
    private GameObject left_camera;
    /// <summary>
    /// 両目に出力する映像を撮影するカメラ
    /// </summary>
    [Tooltip("両目に出力する映像を撮影するカメラ")]
    [SerializeField] private GameObject center_camera;
    /// <summary>
    /// 右目に出力する映像を撮影するカメラ（未使用）
    /// </summary>
    [Tooltip("右目に出力する映像を撮影するカメラ")]
    private GameObject right_camera;
    /// <summary>
    /// 左目の視野（未使用）
    /// </summary>
    [Tooltip("左目の視野")]
    private GameObject left_image;
    /// <summary>
    /// 両目の視野
    /// </summary>
    [Tooltip("両目の視野")]
    [SerializeField] private GameObject center_image;
    /// <summary>
    /// 右目の視野（未使用）
    /// </summary>
    [Tooltip("右目の視野")]
    private GameObject right_image;

    /// <summary>
    /// 前後軸反転時に手の位置も前後反転させるか否か
    /// </summary>
    [Tooltip("前後軸反転時に手の位置も前後反転させるか否か")]
    public bool showMirrorHand = false;
    /// <summary>
    /// 手の位置を前後反転させるためのスクリプト
    /// </summary>
    [Tooltip("手の位置を前後反転させるためのスクリプト")]
    [SerializeField] MirrorHand mirrorhand_script;

    /// <summary>
    /// 頭部垂直軸から目（HMD）までの距離
    /// </summary>
    [Tooltip("頭部垂直軸から目（HMD）までの距離")]
    [Range(0, 0.3f)] public float eye_rotation_diameter = 0.15f;

    void Start()
    {
        camerarig = GameObject.Find("OVRCameraRig");
        rigscript = camerarig.GetComponent<OVRCameraRig>();
        //hmd = GameObject.Find("CenterEyeAnchor");
        if (mirrorhand_script != null) mirrorhand_script.enabled = false;

        left_camera = GameObject.Find("LeftEyeCapture");
        if (center_camera == null) center_camera = GameObject.Find("CenterEyeCapture");
        right_camera = GameObject.Find("RightEyeCapture");
        left_image = GameObject.Find("LeftRawImage");
        if (center_image == null)  center_image = GameObject.Find("CenterRawImage");
        right_image = GameObject.Find("RightRawImage");

        //もしUnasssignedReferenceExceptionがでたら他のオブジェクトにもスクリプトをアタッチしてるかも
        //SetReversion[] setrev = FindObjectsOfType<SetReversion>();
        //foreach(var rev in setrev)
        //{
        //    Debug.Log(rev.gameObject.name);
        //}
        //SetMirrorHand();
    }

    void Update()
    {
        // 両眼視差非対応なので基本はここはfalseになる
        if (!rigscript.usePerEyeCameras)
        {
            left_image.SetActive(false);
            right_image.SetActive(false);
        }
        else
        {
            left_image.SetActive(true);
            right_image.SetActive(true);
        }
        
        // 視野反転切り替え
        if ((leftright_reversion != leftright_flag) || Input.GetKeyDown(KeyCode.L) || OVRInput.GetDown(OVRInput.RawButton.Y))
        {
            leftRightReversion();
        }
        if ((updown_reversion != updown_flag) || Input.GetKeyDown(KeyCode.U) || OVRInput.GetDown(OVRInput.RawButton.X))
        {
            upDownReversion();
        }
        if ((frontback_reversion != frontback_flag) || Input.GetKeyDown(KeyCode.F) || OVRInput.GetDown(OVRInput.RawButton.LHandTrigger))
        {
            frontBackReversion();
        }
        if (Input.GetKeyDown(KeyCode.X) || ((leftright_reversion == false && updown_reversion == false && frontback_reversion == false)
            && (leftright_flag == true || updown_flag == true || frontback_flag == true)))
        {
            resetScale();
        }
    }
    
    /// <summary>
    /// 視野反転をリセットする関数（バグの場合もこれで修正）
    /// </summary>
    /// <remarks>
    /// 何をリセットしているかは視野反転用の関数を見た方がわかりやすい．
    /// </remarks>
    public void resetScale() 
    {
        left_image.transform.localScale = GetDefaultVector(left_image.transform.localScale); 
        center_image.transform.localScale = GetDefaultVector(center_image.transform.localScale);
        right_image.transform.localScale = GetDefaultVector(right_image.transform.localScale);
        if (frontback_flag)
        {
            //left_camera.transform.localPosition = MultiplyZByMinusOne(left_camera.transform.localPosition);
            //center_camera.transform.localPosition = MultiplyZByMinusOne(center_camera.transform.localPosition);
            //right_camera.transform.localPosition = MultiplyZByMinusOne(right_camera.transform.localPosition);
            center_camera.transform.localPosition += (frontback_flag ? eye_rotation_diameter : -eye_rotation_diameter) * Vector3.forward;

            left_camera.transform.localRotation = Quaternion.identity;
            center_camera.transform.localRotation = Quaternion.identity;
            right_camera.transform.localRotation = Quaternion.identity;
            
        }

        leftright_flag = false;
        updown_flag = false;
        frontback_flag = false;
        leftright_reversion = false;
        updown_reversion = false;
        frontback_reversion = false;
        Debug.Log("normal");
    }

    /// <summary>
    /// 視野を左右反転させる関数
    /// </summary>
    public void leftRightReversion()
    {
        left_image.transform.localScale = MultiplyXByMinusOne(left_image.transform.localScale);
        center_image.transform.localScale = MultiplyXByMinusOne(center_image.transform.localScale);
        right_image.transform.localScale = MultiplyXByMinusOne(right_image.transform.localScale);

        leftright_flag = leftright_flag ? false : true;
        leftright_reversion = leftright_flag;
        Debug.Log("LR");
    }

    /// <summary>
    /// 視野を上下反転させる関数
    /// </summary>
    public void upDownReversion()
    {
        left_image.transform.localScale = MultiplyYByMinusOne(left_image.transform.localScale);
        center_image.transform.localScale = MultiplyYByMinusOne(center_image.transform.localScale);
        right_image.transform.localScale = MultiplyYByMinusOne(right_image.transform.localScale);

        updown_flag = updown_flag ? false : true;
        updown_reversion = updown_flag;
        Debug.Log("UD");
    }

    /// <summary>
    /// 視野を前後反転させる関数
    /// </summary>
    public void frontBackReversion()
    {
        // 目が頭の後ろにある想定で位置を調整
        //left_camera.transform.localPosition = MultiplyZByMinusOne(left_camera.transform.localPosition);
        //center_camera.transform.localPosition = MultiplyZByMinusOne(center_camera.transform.localPosition);
        //right_camera.transform.localPosition = MultiplyZByMinusOne(right_camera.transform.localPosition);
        center_camera.transform.localPosition += (frontback_flag ? eye_rotation_diameter : -eye_rotation_diameter) * Vector3.forward;

        // 目を180°回転
        left_camera.transform.Rotate(0, 180, 0, Space.Self);
        center_camera.transform.Rotate(0, 180, 0, Space.Self);
        right_camera.transform.Rotate(0, 180, 0, Space.Self);

        // 上の操作では前後反転＋左右反転なので、左右反転を打ち消す必要がある（左右反転フラグを切り替えないようにする）
        leftRightReversion();
        leftright_flag = leftright_flag ? false : true;
        leftright_reversion = leftright_flag;

        frontback_flag = frontback_flag ? false : true;
        frontback_reversion = frontback_flag;

        if (showMirrorHand && mirrorhand_script != null)
        {
            SetMirrorHand();
        }

        Debug.Log("FB");
    }

    /// <summary>
    /// 視野の前後軸反転時に手の位置も反転させて見せるための関数
    /// </summary>
    void SetMirrorHand()
    {
        if (mirrorhand_script != null)
        {
            mirrorhand_script.enabled = frontback_flag;

            mirrorhand_script.LhandCollider.enabled = !frontback_flag;
            if (mirrorhand_script.LHandAppearance != null)
                mirrorhand_script.LHandAppearance.SetActive(!frontback_flag);
            mirrorhand_script.mirrorLhand.SetActive(frontback_flag);

            mirrorhand_script.RHandCollider.enabled = !frontback_flag;
            if (mirrorhand_script.RHandAppearance != null)
                mirrorhand_script.RHandAppearance.SetActive(!frontback_flag);
            mirrorhand_script.mirrorRhand.SetActive(frontback_flag);
        }
    }

    /// <summary>
    /// ベクトルのx成分を-1倍する関数
    /// </summary>
    Vector3 MultiplyXByMinusOne(Vector3 inputVector)
    {
        return new Vector3(-inputVector.x, inputVector.y, inputVector.z);
    }

    /// <summary>
    /// ベクトルのy成分を-1倍する関数
    /// </summary>
    Vector3 MultiplyYByMinusOne(Vector3 inputVector)
    {
        return new Vector3(inputVector.x, -inputVector.y, inputVector.z);
    }

    /// <summary>
    /// ベクトルのz成分を-1倍する関数
    /// </summary>
    Vector3 MultiplyZByMinusOne(Vector3 inputVector)
    {
        return new Vector3(inputVector.x, inputVector.y, -inputVector.z);
    }

    /// <summary>
    /// ベクトルの各成分の絶対値をとる関数
    /// </summary>
    /// <remarks>
    /// 視野反転は-1倍操作であるため，絶対値をとれば反転がなくなる
    /// </remarks>
    Vector3 GetDefaultVector(Vector3 convertedVector)
    {
        return new Vector3(Mathf.Abs(convertedVector.x), Mathf.Abs(convertedVector.y), Mathf.Abs(convertedVector.z));
    }
}
