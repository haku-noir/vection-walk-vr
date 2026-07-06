using UnityEngine;

/// <summary>
/// カメラと UI の距離を調節するためのクラス．
/// UI に視野を映し，その UI を近づけたり遠ざけたりすれば擬似的に視野角を変更することができる．
/// </summary>
public class AdjustCanvasDistance : MonoBehaviour
{
    /// <summary>
    /// カメラコンポーネント
    /// </summary>
    [Tooltip("カメラ")]
    public Camera mainCamera; // インスペクターでメインのカメラをアタッチ
    /// <summary>
    /// UI
    /// </summary>
    [Tooltip("UI")]
    public Canvas canvas;
    /// <summary>
    /// トラッキングされた頭部
    /// </summary>
    [Tooltip("トラッキングされた頭部")]
    public GameObject head;
    /// <summary>
    /// 実現したい視野角
    /// </summary>
    [Tooltip("実現したい視野角")]
    public float targetFOV = 60f; // インスペクターで目標の視野角を指定
    public bool headSync = false;

    private Vector3 vec;
    private Vector3 vec_norm;
    private float initDistance;
    private Vector3 initCameraPos;

    void Start()
    {
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.renderMode = RenderMode.WorldSpace;

        initCameraPos = mainCamera.transform.position;
        vec = canvas.GetComponent<RectTransform>().position - mainCamera.transform.position;
        vec_norm = vec.normalized;
        initDistance = Vector3.Magnitude(vec);
    }

    private void Update()
    {
        AdjustDistanceToMatchFOV();
        // ここは適切でないかもしれない
        if (headSync)
            mainCamera.transform.position = new Vector3(head.transform.localPosition.x, head.transform.localPosition.y, mainCamera.transform.position.z);
        else
            mainCamera.transform.position = initCameraPos;
    }

    /// <summary>
    /// UI との距離を変更することで擬似的に視野角を変更する関数
    /// </summary>
    void AdjustDistanceToMatchFOV()
    {
        float currentFOV = mainCamera.fieldOfView;

        // 目標の視野角と現在の視野角の比率を計算
        float fovRatio = targetFOV / currentFOV;

        // カメラとCanvasの距離を調整
        float newDistance = initDistance * fovRatio;

        // Canvasを新しい位置に移動
        canvas.GetComponent<RectTransform>().position = initCameraPos + vec_norm * newDistance;
        //Debug.Log(initCameraPos);
        Debug.Log(vec_norm * newDistance);
    }
}
