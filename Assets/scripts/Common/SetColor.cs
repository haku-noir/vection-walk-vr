using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// オブジェクトの色を設定するクラス
/// </summary>
public class SetColor : MonoBehaviour
{
    public bool coloring;
    public Color color;

    void Update()
    {
        if (coloring)
            gameObject.GetComponent<Renderer>().material.color = color;
        else
            gameObject.GetComponent<Renderer>().material.color = Color.white;
    }
}
