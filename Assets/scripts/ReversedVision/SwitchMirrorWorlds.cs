using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 視野の反転と同時にオブジェクトを反転させるためのクラス．
/// </summary>
/// <remarks>
/// プレイヤーを取り囲む部屋を反転させることで，視野の反転に気づかせません．
/// SetReversion クラスを引数として取り入れ，反転フラグに応じて部屋を反転させるのがスマートですが，今回は簡易化しています．
/// </remarks>
public class SwitchMirrorWorlds : MonoBehaviour
{
    //[SerializeField] private SetReversion _setreversion;

    [SerializeField] private GameObject room_origin;
    //public GameObject normal_room;
    //public GameObject LR_room;
    //public GameObject UD_room;
    //public GameObject FB_room;

    // Start is called before the first frame update
    void Start()
    {
        //LR_room.SetActive(false);
        //UD_room.SetActive(false);
        //FB_room.SetActive(false);
        //normal_room.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        //if (Input.GetKeyDown(KeyCode.L) || OVRInput.GetDown(OVRInput.RawButton.Y))
        //{
        //    normal_room.SetActive(normal_room.activeSelf ? false : true);
        //    LR_room.SetActive(true);
        //    UD_room.SetActive(false);
        //    FB_room.SetActive(false);
        //}
        //if (Input.GetKeyDown(KeyCode.U) || OVRInput.GetDown(OVRInput.RawButton.X))
        //{
        //    normal_room.SetActive(false);
        //    LR_room.SetActive(false);
        //    UD_room.SetActive(true);
        //    FB_room.SetActive(false);
        //}
        //if (Input.GetKeyDown(KeyCode.F) || OVRInput.GetDown(OVRInput.RawButton.LHandTrigger))
        //{
        //    normal_room.SetActive(false);
        //    LR_room.SetActive(false);
        //    UD_room.SetActive(false);
        //    FB_room.SetActive(true);
        //}

        if (Input.GetKeyDown(KeyCode.L) || OVRInput.GetDown(OVRInput.RawButton.Y))
        {
            room_origin.transform.localScale = MultiplyXByMinusOne(room_origin.transform.localScale);
        }
        if (Input.GetKeyDown(KeyCode.U) || OVRInput.GetDown(OVRInput.RawButton.X))
        {
            room_origin.transform.localScale = MultiplyYByMinusOne(room_origin.transform.localScale);
        }
        if (Input.GetKeyDown(KeyCode.F) || OVRInput.GetDown(OVRInput.RawButton.LHandTrigger))
        {
            room_origin.transform.localScale = MultiplyZByMinusOne(room_origin.transform.localScale);
        }
    }

    /// <summary>
    /// x成分を-1倍する関数
    /// </summary>
    Vector3 MultiplyXByMinusOne(Vector3 inputVector)
    {
        return new Vector3(-inputVector.x, inputVector.y, inputVector.z);
    }

    /// <summary>
    /// y成分を-1倍する関数
    /// </summary>
    Vector3 MultiplyYByMinusOne(Vector3 inputVector)
    {
        return new Vector3(inputVector.x, -inputVector.y, inputVector.z);
    }

    /// <summary>
    /// z成分を-1倍する関数
    /// </summary>
    Vector3 MultiplyZByMinusOne(Vector3 inputVector)
    {
        return new Vector3(inputVector.x, inputVector.y, -inputVector.z);
    }
}
