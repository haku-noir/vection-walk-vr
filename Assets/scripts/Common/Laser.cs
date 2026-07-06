using UnityEngine;

public class Laser : MonoBehaviour
{
    [SerializeField] public Transform target; // ターゲットの位置
    public Transform camera;
    public LineRenderer lineRenderer; // ラインレンダラーコンポーネント
    public float laserLength = 100.0f; // レーザーの最大長さ
    public CrosshairAlignment _crosshairalignment;

    void Start()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        // ラインレンダラーの初期設定
        lineRenderer.positionCount = 2; // ラインのポイント数
        lineRenderer.startWidth = 0.02f; // ラインの開始幅
        lineRenderer.endWidth = 0.02f; // ラインの終了幅

        Transform target = _crosshairalignment.crosshairInstance.transform;
    }

    void Update()
    {
        if (target != null)
        {
            // カメラの位置からターゲットに向かう方向を計算
            Vector3 direction = (target.position - camera.position).normalized;

            // ラインの開始点と終了点を設定
            lineRenderer.SetPosition(0, camera.position);
            lineRenderer.SetPosition(1, target.position);
        }
        else
        {
            // ターゲットがない場合はレーザーを最大長さに設定
            lineRenderer.SetPosition(0, camera.position);
            lineRenderer.SetPosition(1, camera.position + camera.forward * laserLength);
        }
    }
}
