using UnityEngine;

public class LaserCylinder : MonoBehaviour
{
    public Transform target; // ターゲットの位置
    public GameObject laserPrefab; // レーザープレハブ

    private GameObject laserInstance; // 生成されたレーザーのインスタンス

    void Start()
    {
        if (laserPrefab == null)
        {
            Debug.LogError("Laser prefab is not assigned.");
            return;
        }

        // レーザープレハブを生成
        laserInstance = Instantiate(laserPrefab, transform.position, Quaternion.identity);
    }

    void Update()
    {
        if (target != null)
        {
            // カメラの位置からターゲットに向かう方向を計算
            Vector3 direction = (target.position - transform.position).normalized;

            // レーザーの位置と回転を設定
            laserInstance.transform.position = (transform.position + target.position) / 2;
            laserInstance.transform.LookAt(target.position);
            laserInstance.transform.localScale = new Vector3(0.05f, 0.05f, Vector3.Distance(transform.position, target.position) / 2);
        }
        else
        {
            // ターゲットがない場合、レーザーを非表示にするか、適切な処理を行う
            laserInstance.SetActive(false);
        }
    }
}
