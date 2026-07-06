using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// おそらくランダムドットのサンプル的なもの
/// </summary>
public class sideline : MonoBehaviour
{
    
    private GameObject Camera;
    private GameObject cube;
    private Vector3 initpos;
    public float speedratio = 1;
    public float dotsize = 3;
    public float distance = 20;

    // Start is called before the first frame update
    void Start()
    {
        Camera = GameObject.Find("Main Camera");
        initpos = Camera.transform.localPosition;

        cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.eulerAngles = new Vector3(45.0f, 0.0f, 0.0f);
        Vector3 angle = cube.transform.eulerAngles;
        angle.x = angle.x + 20;
        cube.transform.eulerAngles = angle;
        cube.transform.Rotate(10, 0, 0, Space.Self);
        cube.transform.localScale = dotsize * Vector3.one;
        cube.transform.parent = Camera.transform;
        cube.GetComponent<Renderer>().material.color = Color.red;
    }

    // Update is called once per frame
    void Update()
    {
        cube.transform.localScale = dotsize * Vector3.one;
        //前フレームとの変位を加えていく形にするか、初期位置との変位と基準位置との和を代入し続けるか
        Vector3 initcube = new Vector3(0f, 0f, distance);
        cube.transform.localPosition = - speedratio * (Camera.transform.localPosition - initpos) + initcube;
        // cube.transform.localPosition = -0.3f * cube.transform.localPosition; //fをつけないとdoubleになる

    }
}
