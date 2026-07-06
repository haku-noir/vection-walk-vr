using UnityEngine;
using UnityEngine.UI;

public class TargetIndicator : MonoBehaviour
{
    public Camera mainCamera; // メインカメラ
    public Transform target; // ターゲット
    public RectTransform canvasRectTransform; // キャンバスのRectTransform
    public RectTransform lineRendererRectTransform; // ラインレンダラーのRectTransform

    void Update()
    {
        // ターゲットのスクリーン座標を取得
        Vector3 screenPos = mainCamera.WorldToScreenPoint(target.position);

        // スクリーン座標をキャンバスの座標に変換
        Vector2 canvasPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, screenPos, mainCamera, out canvasPos);

        // ラインの終点をターゲットの位置に設定
        lineRendererRectTransform.anchoredPosition = canvasPos;

        // ラインの始点をキャンバスの中心に設定
        Vector2 canvasCenter = new Vector2(0, 0);
        lineRendererRectTransform.pivot = new Vector2(0.5f, 0.5f);
        lineRendererRectTransform.sizeDelta = new Vector2(Vector2.Distance(canvasCenter, canvasPos), lineRendererRectTransform.sizeDelta.y);
        float angle = Mathf.Atan2(canvasPos.y - canvasCenter.y, canvasPos.x - canvasCenter.x) * Mathf.Rad2Deg;
        lineRendererRectTransform.localEulerAngles = new Vector3(0, 0, angle);
    }
}
