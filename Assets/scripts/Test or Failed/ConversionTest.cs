using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConversionTest : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        //　ゲームオブジェクトのワールドポイント
        Debug.Log("ワールド空間の位置: " + transform.position);
        //　このスクリプトを設定したゲームオブジェクトの位置をビューポートポイントに変換し、コンソールに表示する
        Debug.Log("ビューポートポイント " + Camera.main.WorldToViewportPoint(transform.position));
        //　このスクリプトが取り付けられたゲームオブジェクトの位置をスクリーンポイントに変換して表示する
        Debug.Log("スクリーンポイント: " + Camera.main.WorldToScreenPoint(transform.position));
    }
}