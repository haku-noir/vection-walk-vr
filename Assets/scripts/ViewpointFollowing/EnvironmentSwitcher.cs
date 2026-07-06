using UnityEngine;

/// <summary>
/// 環境オブジェクトの密度（視覚刺激のオプティカルフロー量）を切り替えるクラス．
/// 数字キーで切替: 1 = 高密度, 2 = 低密度, 3 = オブジェクトなし．
/// 同じ収録軌跡のまま環境条件だけを変えて実験するために使う．
/// </summary>
public class EnvironmentSwitcher : MonoBehaviour
{
    /// <summary>
    /// 高密度環境（オブジェクト多）の親オブジェクト
    /// </summary>
    [Tooltip("高密度環境（オブジェクト多）の親")]
    public GameObject envRich;

    /// <summary>
    /// 低密度環境（オブジェクト少）の親オブジェクト
    /// </summary>
    [Tooltip("低密度環境（オブジェクト少）の親")]
    public GameObject envSparse;

    /// <summary>
    /// 現在の密度条件（0 = 高密度, 1 = 低密度, 2 = なし）
    /// </summary>
    public int CurrentDensity { get; private set; }

    private void Start()
    {
        SetDensity(0); // 初期状態は高密度
    }

    private void Update()
    {
        // Update は timeScale = 0 でも動くため，停止中（試行間）に条件を切り替えられる
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SetDensity(0);
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SetDensity(1);
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SetDensity(2);
    }

    /// <summary>
    /// 環境密度を設定する
    /// </summary>
    /// <param name="density">0 = 高密度, 1 = 低密度, 2 = なし</param>
    public void SetDensity(int density)
    {
        CurrentDensity = density;
        if (envRich != null) envRich.SetActive(density == 0);
        if (envSparse != null) envSparse.SetActive(density == 1);
        Debug.Log("[EnvironmentSwitcher] 環境密度: " + new[] { "高密度", "低密度", "なし" }[density]);
    }
}
