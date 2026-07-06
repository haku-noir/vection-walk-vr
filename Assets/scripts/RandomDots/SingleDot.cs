using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SingleDot : MonoBehaviour
{

    private GameObject Camera;
    private GameObject obj;
    private Vector3 initCameraPos;

    public bool onlyX = true;
    public float speedratio = 1;
    public float dotsize = 0.3f;
    public float distance = 1;

    public bool passive = false;
    public float width = 1;
    public float velocity = 1;
    //private bool direction = true;

    private Vector3 initObjPos;

    // Start is called before the first frame update
    void Start()
    {
        Camera = GameObject.Find("CenterEyeAnchor");
        initCameraPos = Camera.transform.localPosition;

        obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.transform.parent = Camera.transform;
        obj.transform.localPosition = initObjPos = distance * Vector3.forward;
    }

    // Update is called once per frame
    void Update()
    {
        SetPosition(obj, Camera, initObjPos, initCameraPos, distance, width, speedratio, velocity);
        obj.transform.localScale = dotsize * Vector3.one;
        //前フレームとの変位を加えていく形にするか、初期位置との変位と基準位置との和を代入し続けるか
        // obj.transform.localPosition = -0.3f * obj.transform.localPosition; //fをつけないとdoubleになる

    }
    public void SetPosition(GameObject obj, GameObject Camera, Vector3 initObjPos, Vector3 initCameraPos, float distance, float width, float speedratio, float velocity)
    {
        initObjPos = new Vector3(initObjPos.x, initObjPos.y, distance);
        if (passive) // 自動で動く（受動運動視）
        {
            if (Mathf.Abs(obj.transform.localPosition.x) >= width) velocity *= -1;
            obj.transform.localPosition += new Vector3(velocity, 0, 0) * Time.deltaTime;
            if (onlyX)
            {
                Vector3 nowObjPos = obj.transform.localPosition;
                nowObjPos.z = distance;
                obj.transform.localPosition = nowObjPos;
            }
        }
        else
        {
            Vector3 dif = Camera.transform.localPosition - initCameraPos;
            if (onlyX)
            {
                obj.transform.localPosition = -speedratio * dif.x * Vector3.right + initObjPos;
            }
            else
            {
                obj.transform.localPosition = -speedratio * dif + initObjPos;
            }
        }
    }
}
