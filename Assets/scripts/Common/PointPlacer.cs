using UnityEngine;

public class PointPlacer : MonoBehaviour
{
    // 配置する点のプレハブをインスペクターから割り当てる（例: Sphere）
    public GameObject pointPrefab;

    // 中央の点の位置（距離20の位置）
    public float distance = 20f;

    // 配置する視野角のリスト
    public float[] fieldOfViewAngles = { 10f, 20f, 30f, 40f, 50f, 60f, 70f, 80f };

    // 各視野角ごとの方向数
    public int numberOfDirections = 8;

    // 色の開始と終了（グラデーション用）
    public Color startColor = Color.blue;
    public Color endColor = Color.red;

    void Start()
    {
        if (pointPrefab == null)
        {
            Debug.LogError("Point Prefabが割り当てられていません！");
            return;
        }

        // 視野角の数
        int angleCount = fieldOfViewAngles.Length;

        for (int i = 0; i < angleCount; i++)
        {
            float fovAngle = fieldOfViewAngles[i];

            // 視野角をラジアンに変換
            float theta = Mathf.Deg2Rad * fovAngle;

            // 色をグラデーションで設定
            float t = (float)i / (angleCount - 1); // 0から1の範囲
            Color pointColor = Color.Lerp(startColor, endColor, t);

            for (int j = 0; j < numberOfDirections; j++)
            {
                // 各方向の角度（ラジアン）
                float phi = Mathf.Deg2Rad * (j * (360f / numberOfDirections));

                // 球面座標系を使用して位置を計算
                float x = distance * Mathf.Sin(theta) * Mathf.Cos(phi);
                float y = distance * Mathf.Sin(theta) * Mathf.Sin(phi);
                float z = distance * Mathf.Cos(theta);

                Vector3 pointPosition = new Vector3(x, y, z);

                // プレハブを配置
                GameObject point = Instantiate(pointPrefab, pointPosition, Quaternion.identity, this.transform);

                // 色を設定
                Renderer renderer = point.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(renderer.sharedMaterial);
                    renderer.material.color = pointColor;
                }
                else
                {
                    Debug.LogWarning("プレハブにRendererがアタッチされていません！");
                }
            }
        }

        // 中央の点を配置（オプション）
        // GameObject central = Instantiate(pointPrefab, Vector3.zero, Quaternion.identity, this.transform);
        // Renderer centralRenderer = central.GetComponent<Renderer>();
        // if (centralRenderer != null)
        // {
        //     centralRenderer.material.color = Color.white;
        // }
    }
}
