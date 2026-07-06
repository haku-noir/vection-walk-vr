using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 平面上のランダムドットを動かすクラス
/// </summary>
public class PlaneDot : MonoBehaviour
{

    private GameObject Camera;
    // private GameObject[] obj;
    public int numDots = 200;
    private Vector3 initCameraPos;

    /// <summary>
    /// y方向のHMDの運動に同期させないか
    /// </summary>
    [Tooltip("y方向のHMDの運動に同期させないか")]
    public bool onlyX = true;
    /// <summary>
    /// HMD の運動に対して同期して動くランダムドットの運動の速さの比
    /// </summary>
    [Tooltip("HMD の運動に対して同期して動くランダムドットの運動の速さの比（普通は1）")]
    public float speedratio = 1;
    /// <summary>
    /// ランダムドットの大きさ
    /// </summary>
    [Tooltip("ランダムドットの大きさ")]
    public float dotsize = 0.03f;
    /// <summary>
    /// /// ランダムドットが表示される平面のまでの距離
    /// </summary>
    [Tooltip("ランダムドットが表示される平面のまでの距離")]
    public float distance = 1;
    /// <summary>
    /// ランダムドットが表示される平面の幅
    /// </summary>
    [Tooltip("ランダムドットが表示される平面の幅")]
    public float width = 2;
    /// <summary>
    /// ランダムドットが表示される平面の高さ
    /// </summary>
    [Tooltip("ランダムドットが表示される平面の高さ")]
    public float height = 2;

    // <summary>
    /// 受動運動視（ドットが自動で運動）
    /// </summary>
    [Tooltip("ドットが自動で運動")]
    public bool passive = false;
    /// <summary>
    /// 円柱端での反射
    /// </summary>
    [Tooltip("ドットが円柱の一端まで来た時に速度反転して跳ね返ってくるか他端から出てくるか")]
    public bool reflection = false;
    /// <summary>
    /// 自動で運動する際の速度
    /// </summary>
    [Tooltip("自動で運動する際の速度")]
    public float velocity = 1;
    //private bool direction = true;

    //public SingleDot _singledot;
    struct Dot
    {
        public GameObject obj;
        public Vector3 initPos;
        public Vector3 speed;
        public int direction;
    }
    private Dot[] dots;


    // private Vector3[] initObjPos;
    // private Vector3[] vel;

    // Start is called before the first frame update
    void Start()
    {
        Camera = GameObject.Find("CenterEyeAnchor");
        initCameraPos = Camera.transform.localPosition;

        dots = new Dot[numDots];
        // obj = new GameObject[numDots];
        // initObjPos = new Vector3[numDots];
        // vel = new Vector3[numDots];
        for (int i = 0; i < numDots; i++)
        {
            dots[i].obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            // obj[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dots[i].obj.transform.parent = Camera.transform;
            // obj[i].transform.parent = Camera.transform;

            dots[i].initPos = new Vector3(Random.Range(-width/2, width/2), Random.Range(-height/2, height/2), distance);
            // initObjPos[i] = distance * Vector3.forward;
            // initObjPos[i] += new Vector3(Random.Range(-width, width), Random.Range(-width, width), 0);
            dots[i].obj.transform.localPosition = dots[i].initPos;
            // obj[i].transform.localPosition = initObjPos[i];
            dots[i].speed = velocity * Vector3.right;
            // vel[i] = velocity * Vector3.right;
            dots[i].direction = 1;
        }
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < numDots; i++)
        {
            // initObjPos[i] = new Vector3(initObjPos[i].x, initObjPos[i].y, distance);
            if (passive) // 自動で動く（受動運動視）
            {
                float sub = Mathf.Abs(dots[i].obj.transform.localPosition.x) - Mathf.Abs(width/2);
                if (sub >= 0)
                {
                    if (reflection)
                        dots[i].direction = (int)-Mathf.Sign(dots[i].obj.transform.localPosition.x);
                    //vel[i] = -Mathf.Sign(obj[i].transform.localPosition.x) * velocity * Vector3.right;
                    else
                        dots[i].obj.transform.localPosition = new Vector3((int)-Mathf.Sign(dots[i].obj.transform.localPosition.x)*width/2+sub, dots[i].obj.transform.localPosition.y, dots[i].obj.transform.localPosition.z);
                }
                dots[i].speed = dots[i].direction * velocity * Vector3.right;
                dots[i].obj.transform.localPosition += dots[i].speed * Time.deltaTime;
                dots[i].obj.transform.localPosition = new Vector3(dots[i].obj.transform.localPosition.x, dots[i].obj.transform.localPosition.y, distance);
                // Vector3 nowObjPos = obj[i].transform.localPosition;
                // nowObjPos.z = distance;
                // obj[i].transform.localPosition = nowObjPos;
                
            }
            else // 能動
            {
                dots[i].initPos = new Vector3(dots[i].initPos.x, dots[i].initPos.y, distance);
                Vector3 dif = Camera.transform.localPosition - initCameraPos;
                if (onlyX)
                {
                    dots[i].obj.transform.localPosition = -speedratio * dif.x * Vector3.right + dots[i].initPos;
                }
                else
                {
                    dots[i].obj.transform.localPosition = -speedratio * dif + dots[i].initPos;
                }
                if (i == 0)
                {
                    Debug.Log(dif);
                    Debug.Log(dots[i].initPos);
                    Debug.Log(dots[i].obj.transform.localPosition);
                }
            }
            dots[i].obj.transform.localScale = dotsize * Vector3.one;
            //前フレームとの変位を加えていく形にするか、初期位置との変位と基準位置との和を代入し続けるか
            // obj.transform.localPosition = -0.3f * obj.transform.localPosition; //fをつけないとdoubleになる
        }
    }

    
}
