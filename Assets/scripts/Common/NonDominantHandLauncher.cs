using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 利き手でない（メインとなるスクリプトがアタッチされていない）方の手で触れた際にそのスクリプトを実行するクラス．SphereLauncher クラス用．
/// </summary>
public class NonDominantHandLauncher : MonoBehaviour
{
    [SerializeField] SphereLauncher script;

    private void OnTriggerEnter(Collider other)
    {
        if (script.enabled)
        {
            script.OnTriggerEnter(other);
        }
    }
}
