using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 利き手でない（メインとなるスクリプトがアタッチされていない）方の手で触れた際にそのスクリプトを実行するクラス．ControllerTouchSphere 用．
/// </summary>
public class NondominantHandTouch : MonoBehaviour
{
    public ControllerTouchSphere script;

    private void OnTriggerEnter(Collider other)
    {
        if (script.enabled)
        {
            script.touchedController = OVRInput.Controller.LTouch;
            script.OnTriggerEnter(other);
            script.touchedController = OVRInput.Controller.RTouch;
        }
    }
}
