using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 視野を直接は反転させず，視座運動に対する視野の流れのみを反転させる．
/// すなわち，視座運動がないとき観察者は反転に気づかない．
/// </summary>
public class SetVelRev : MonoBehaviour
{
    public bool updown_reversion = false;
    public bool leftright_reversion = false;
    public bool frontback_reversion = false;

    private GameObject camerarig;
    private OVRCameraRig rigscript;
    private GameObject hmd;

    private GameObject left_camera;
    private GameObject center_camera;
    private GameObject right_camera;
    private GameObject left_image;
    private GameObject center_image;
    private GameObject right_image;

    private bool leftright_flag = false;
    private bool updown_flag = false;
    private bool frontback_flag = false;
    private Vector3 center_rot = Vector3.zero;
    private Vector3 center_mov = Vector3.zero;
    private Vector3 eye_rot = Vector3.zero;
    private Vector3 eye_mov = Vector3.zero;

    public bool showMirrorHand = false;
    [SerializeField] MirrorHand mirrorhand_script;

    [Range(0, 0.3f)] public float eye_rotation_diameter = 0.2f; // 目の首を中心とする回転半径は0.1，直径は0.2

    private Vector3 rot_rev = Vector3.one;
    private Vector3 mov_rev = Vector3.one;

    // Start is called before the first frame update
    void Start()
    {
        camerarig = GameObject.Find("OVRCameraRig");
        rigscript = camerarig.GetComponent<OVRCameraRig>();
        hmd = GameObject.Find("CenterEyeAnchor");
        if (mirrorhand_script != null) mirrorhand_script.enabled = false;

        left_camera = GameObject.Find("LeftEyeCapture");
        center_camera = GameObject.Find("CenterEyeCapture");
        right_camera = GameObject.Find("RightEyeCapture");
        left_image = GameObject.Find("LeftRawImage");
        center_image = GameObject.Find("CenterRawImage");
        right_image = GameObject.Find("RightRawImage");

        //もしUnasssignedReferenceExceptionがでたら他のオブジェクトにもスクリプトをアタッチしてるかも
        //SetReversion[] setrev = FindObjectsOfType<SetReversion>();
        //foreach(var rev in setrev)
        //{
        //    Debug.Log(rev.gameObject.name);
        //}
        SetMirrorHand();
    }

    // Update is called once per frame
    void Update()
    {

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

        // 条件を変更した時1回のみ呼ばれる
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

        Vector3 dif_rot = hmd.transform.eulerAngles - center_rot;

        center_camera.transform.eulerAngles = eye_rot + new Vector3(rot_rev.x * dif_rot.x, rot_rev.y * dif_rot.y, rot_rev.z * dif_rot.z);
        print("rot_rev : " + rot_rev);
        print("center_rot : " + center_rot);
        print("head_rot : " + hmd.transform.eulerAngles);
        print("dif_rot : " + dif_rot);
        print("result : " + center_camera.transform.eulerAngles);
    }
    
    // リセット（バグの場合もこれで修正を）
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

    private void save_head_rotation()
    {
        center_rot = hmd.transform.localEulerAngles;
        center_mov = hmd.transform.localPosition;
        eye_rot = center_camera.transform.eulerAngles;
        eye_mov = center_camera.transform.position;
    }

    public void leftRightReversion()
    {
        // left_camera.transform.localScale = MultiplyXByMinusOne(left_image.transform.localScale);
        // center_camera.transform.eulerAngles = center_rot - (hmd.transform.eulerAngles - center_rot);
        // right_image.transform.localScale = MultiplyXByMinusOne(right_image.transform.localScale);

        leftright_flag = leftright_flag ? false : true;
        leftright_reversion = leftright_flag;

        save_head_rotation();

        rot_rev = MultiplyYByMinusOne(rot_rev);
        rot_rev = MultiplyZByMinusOne(rot_rev);
        mov_rev = MultiplyXByMinusOne(mov_rev);

       
        Debug.Log("LR");
    }

    public void upDownReversion()
    {
        // left_image.transform.localScale = MultiplyYByMinusOne(left_image.transform.localScale);
        //center_image.transform.localScale = MultiplyYByMinusOne(center_image.transform.localScale);
        // right_image.transform.localScale = MultiplyYByMinusOne(right_image.transform.localScale);

        updown_flag = updown_flag ? false : true;
        updown_reversion = updown_flag;

        save_head_rotation();

        rot_rev = MultiplyXByMinusOne(rot_rev);
        rot_rev = MultiplyZByMinusOne(rot_rev);
        mov_rev = MultiplyYByMinusOne(mov_rev);

        Debug.Log("UD");
    }

    public void frontBackReversion()
    {
        // 頭の後ろにある想定で位置を調整
        //left_camera.transform.localPosition = MultiplyZByMinusOne(left_camera.transform.localPosition);
        //center_camera.transform.localPosition = MultiplyZByMinusOne(center_camera.transform.localPosition);
        //right_camera.transform.localPosition = MultiplyZByMinusOne(right_camera.transform.localPosition);
        //center_camera.transform.localPosition += (frontback_flag ? eye_rotation_diameter : -eye_rotation_diameter) * Vector3.forward;

        // ローカル座標で180°回転
        // left_camera.transform.Rotate(0, 180, 0, Space.Self);
        //center_camera.transform.Rotate(0, 180, 0, Space.Self);
        // right_camera.transform.Rotate(0, 180, 0, Space.Self);

        // 上の操作では前後反転＋左右反転なので、左右反転を打ち消す必要がある
        //leftRightReversion();
        //leftright_flag = leftright_flag ? false : true;
        //leftright_reversion = leftright_flag;

        frontback_flag = frontback_flag ? false : true;
        frontback_reversion = frontback_flag;

        save_head_rotation();

        rot_rev = MultiplyXByMinusOne(rot_rev);
        rot_rev = MultiplyYByMinusOne(rot_rev);
        mov_rev = MultiplyZByMinusOne(mov_rev);

        if (showMirrorHand && mirrorhand_script != null)
        {
            SetMirrorHand();
        }

        Debug.Log("FB");
    }

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

    // x成分を-1倍する関数
    Vector3 MultiplyXByMinusOne(Vector3 inputVector)
    {
        return new Vector3(-inputVector.x, inputVector.y, inputVector.z);
    }

    // y成分を-1倍する関数
    Vector3 MultiplyYByMinusOne(Vector3 inputVector)
    {
        return new Vector3(inputVector.x, -inputVector.y, inputVector.z);
    }

    // z成分を-1倍する関数
    Vector3 MultiplyZByMinusOne(Vector3 inputVector)
    {
        return new Vector3(inputVector.x, inputVector.y, -inputVector.z);
    }

    // 各成分の絶対値をとる関数
    Vector3 GetDefaultVector(Vector3 convertedVector)
    {
        return new Vector3(Mathf.Abs(convertedVector.x), Mathf.Abs(convertedVector.y), Mathf.Abs(convertedVector.z));
    }
}
