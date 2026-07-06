using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 出現禁止領域にチェックポイントが生成された場合に近くに移動させるクラス
/// </summary>
public class CheckPointBarrier : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("CheckPoint"))
        {
            other.gameObject.transform.position += new Vector3(10, 0, 0);
        }
    }
}
