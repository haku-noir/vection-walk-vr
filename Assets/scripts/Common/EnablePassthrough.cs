using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// パススルーを Unity 内で見るための簡易クラス
/// </summary>
public class EnablePassthrough : MonoBehaviour
{
    [SerializeField] OVRManager _ovrmanager;

    void Update()
    {
        if (OVRInput.GetDown(OVRInput.RawButton.B))
        {
            _ovrmanager.isInsightPassthroughEnabled = true;
            GameObject cuber = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cuber.transform.position = new Vector3(0.3f, 1f, 0.3f);
            cuber.GetComponent<Renderer>().material.color = Color.red;
        }
    }
}
