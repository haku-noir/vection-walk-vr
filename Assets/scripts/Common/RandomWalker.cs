using System.IO;
using System;
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Collections;

/// <summary>
/// ランダムウォークをさせるためのクラス
/// </summary>
public class RandomWalker : MonoBehaviour
{
    /// <summary>
    /// 移動範囲
    /// </summary>
    /// <remarks>
    /// 初期位置からこの距離しか上下左右に動けない
    /// </remarks>
    [Tooltip("初期位置から上下左右にそれぞれこの距離しか動けない")]
    public float moveRange = 1f;
    /// <summary>
    /// 移動速度
    /// </summary>
    [Tooltip("移動速度")]
    public float stepSize = 1f;
    /// <summary>
    /// 平均化フィルタのサイズ
    /// </summary>
    [Tooltip("平均化フィルタのサイズ")]
    public int frameHistorySize = 100;
    private float startTime = 1f;
    /// <summary>
    /// 管理用
    /// </summary>
    private float cycleTime;
    /// <summary>
    /// ランダムウォークの方向が変化する周期
    /// </summary>
    [Tooltip("ランダムウォークの方向が変化する周期")]
    public float periodicTime;
    /// <summary>
    /// 移動方向（角度）のパターン数
    /// </summary>
    /// <remarks>
    /// 4であれば移動方向は0, pi/2, pi. 2pi/3の4パターン
    /// </remarks>
    [Tooltip("移動方向（角度）のパターン数．4であれば移動方向は0, pi/2, pi. 2pi/3の4パターン．")]
    public int anglePatternNum = 0;
    /// <summary>
    /// 視対象のサイズ
    /// </summary>
    [Tooltip("視対象のサイズ")]
    public float gazePointSize = 0;

    private List<Vector3> frameHistory;
    private Vector3 initialPosition;
    private Vector3 velocity = Vector3.zero;

    /// <summary>
    /// トラッキングされた頭部
    /// </summary>
    [Tooltip("トラッキングされた頭部")]
    [SerializeField] private GameObject head;
    /// <summary>
    /// ランダムウォークする視対象
    /// </summary>
    [Tooltip("ランダムウォークする視対象（自動設定のためインスペクターからの設定不要）")]
    [SerializeField] private GameObject gazePoint;

    private List<Vector3> posLog = new List<Vector3>(2048);
    private List<Vector3> gazeLog = new List<Vector3>(2048);
    private List<float> timeLog = new List<float>(2048);
    private float start_time;
    private List<Vector3> positionLog = new List<Vector3>(2048);
    private List<Vector3> rotationLog = new List<Vector3>(2048);


    void Start()
    {
        start_time = Time.time;

        // フレームの履歴を初期化
        frameHistory = new List<Vector3>(frameHistorySize);
        for (int i = 0; i < frameHistorySize; i++)
        {
            frameHistory.Add(Vector3.zero);
        }

        // 初期位置を保存
        initialPosition = transform.position;

        startTime = Time.time;

        if (head == null) head = GameObject.Find("CenterEyeAnchor");
        gazePoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        gazePoint.transform.localScale = Vector3.one * gazePointSize;
    }

    private void FixedUpdate()
    {
        Vector3 dirVec = head.transform.eulerAngles;
        //print("dirvec" + dirVec);
        dirVec = new Vector3(Mathf.Tan(Mathf.Deg2Rad * dirVec.y), -Mathf.Tan(Mathf.Deg2Rad * dirVec.x), 1);
        float dist = transform.position.z - head.transform.position.z;
        Vector3 gazeVec = dist * dirVec;
        gazePoint.transform.position = head.transform.position + gazeVec;

        posLog.Add(transform.position);
        gazeLog.Add(gazePoint.transform.position);
        timeLog.Add(Time.time - start_time);
        positionLog.Add(head.transform.localPosition);
        rotationLog.Add(head.transform.localEulerAngles);
    }

    void Update()
    {
        if (OVRInput.GetDown(OVRInput.RawButton.B) || Input.GetKeyDown(KeyCode.O))
        {
            if (Time.timeScale == 0)
            {
                PauseGame();
                start_time = Time.time;
                timeLog.Clear();
                posLog.Clear();
                gazeLog.Clear();
                positionLog.Clear();
                rotationLog.Clear();
            }
            else
            {
                start_time = -1f;
                PauseGame();
            }
        }

        // ランダムな方向に移動
        Vector3 randomDirection = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), 0).normalized;
        // 新しいランダムな方向を履歴に追加
        frameHistory.Add(randomDirection);

        // 履歴の範囲をframeHistorySizeに制限
        while (frameHistory.Count > frameHistorySize)
        {
            frameHistory.RemoveAt(0);
        }

        cycleTime = Time.time - startTime;
        Vector3 averagedDirection;

        // periodicTimeごとに速度が変化
        if (cycleTime > periodicTime)
        {
            startTime = Time.time;

            averagedDirection = makeVelocityVector(anglePatternNum);
            velocity = averagedDirection;
        }
        else
        {
            averagedDirection = velocity;
        }
        print(averagedDirection);

        // 平均化された方向に移動
        Vector3 newPosition = transform.position + averagedDirection * stepSize * Time.deltaTime;
        // 移動範囲の制限
        newPosition.x = Mathf.Clamp(newPosition.x, initialPosition.x - moveRange, initialPosition.x + moveRange);
        newPosition.y = Mathf.Clamp(newPosition.y, initialPosition.y - moveRange, initialPosition.y + moveRange);

        // オブジェクトを新しい位置に移動
        transform.position = newPosition;

        // ゲーム終了
        if (Time.timeScale == 0 && OVRInput.GetDown(OVRInput.RawButton.RHandTrigger))
        {
            SaveAndQuitGame();
        }
    }

    Vector3 makeVelocityVector(int split_num)
    {
        Vector3 averagedDirection = Vector3.zero;
        if (split_num > 0)
        {
            int n = UnityEngine.Random.Range(0, split_num);
            print(n);
            averagedDirection = new Vector3(Mathf.Cos(n * 2 * Mathf.PI / split_num), Mathf.Sin(n * 2 * Mathf.PI / split_num), 0);
        }
        else
        {
            // 過去のフレームを平均化（ローパスフィルタ）
            for (int i = 0; i < frameHistory.Count; i++)
            {
                averagedDirection += frameHistory[i];
            }
            averagedDirection /= frameHistory.Count;
        }

        // 移動範囲の制限
        if (transform.position.x <= initialPosition.x - moveRange)
        {
            averagedDirection = new Vector3(Mathf.Abs(averagedDirection.x), averagedDirection.y, averagedDirection.z);
        }
        else if (transform.position.x >= initialPosition.x + moveRange)
        {

            averagedDirection = new Vector3(-Mathf.Abs(averagedDirection.x), averagedDirection.y, averagedDirection.z);
        }
        else if (transform.position.y <= initialPosition.y - moveRange)
        {
            averagedDirection = new Vector3(averagedDirection.x, Mathf.Abs(averagedDirection.y), averagedDirection.z);
        }
        else if (transform.position.y >= initialPosition.y + moveRange)
        {
            averagedDirection = new Vector3(averagedDirection.x, -Mathf.Abs(averagedDirection.y), averagedDirection.z);
        }
        return averagedDirection;
    }

    /// <summary>
    /// ゲームを一時停止します．
    /// </summary>
    void PauseGame()
    {
        if (Time.timeScale == 0)
        {
            Time.timeScale = 1;
            transform.position = initialPosition;
        }
        else
        {
            Time.timeScale = 0;
            transform.position = initialPosition;
        }
    }

    /// <summary>
    /// セーブしてゲームを終了します．
    /// </summary>
    void SaveAndQuitGame()
    {
        if (posLog.Count > 300)
        {
            // アプリケーションが終了する際に、結果を日時付きのCSVに出力
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = "tracking_results_" + timestamp + ".csv";
            string folderName = Path.Combine("ResultData", "tracking");
            string filePath;
#if UNITY_EDITOR
            filePath = Path.Combine(Application.dataPath, folderName, fileName);
#else
            filePath = Path.Combine(Application.persistentDataPath, fileName);
#endif

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("time,posX,posY,posZ,gazeX,gazeY,gazeZ");
                for (int i = 0; i < rotationLog.Count; i++)
                {
                    writer.WriteLine(timeLog[i] + "," + posLog[i].x + "," + posLog[i].y + "," + posLog[i].z + "," +
                                     gazeLog[i].x + "," + gazeLog[i].y + "," + gazeLog[i].z);
                }

                writer.WriteLine("HMD");

                //HMDの軌跡を記録
                writer.WriteLine("time,posX,posY,posZ,rotX,rotY,rotZ");
                for (int i = 0; i < rotationLog.Count; i++)
                {
                    writer.WriteLine(timeLog[i] + "," + positionLog[i].x + "," + positionLog[i].y + "," + positionLog[i].z + "," +
                                     CenterRotValue(rotationLog[i].x) + "," + CenterRotValue(rotationLog[i].y) + "," + CenterRotValue(rotationLog[i].z));
                }
                positionLog.Clear();
                rotationLog.Clear();
            }

            Debug.Log("Results saved to: " + filePath);
        }
        else
        {
            Debug.Log("Quit game.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;   // UnityEditorの実行を停止する処理
#else
            Application.Quit();                                // ゲームを終了する処理
#endif
        }
    }

    /// <summary>
    /// アプリケーション終了時に呼ばれる関数です．
    /// </summary>
    /// <remarks>
    /// ここでの中身は SaveAndQuitGame 関数とほぼ同じです．
    /// しかしながら，アプリ化して HMD に入れた場合は呼ばれないことが多く使えません．
    /// </remarks>
    void OnApplicationQuit()
    {
        if (posLog.Count > 300)
        {
            // アプリケーションが終了する際に、結果を日時付きのCSVに出力
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = "tracking_results_" + timestamp + ".csv";
            string folderName = Path.Combine("ResultData", "tracking");
            string filePath;
#if UNITY_EDITOR
            filePath = Path.Combine(Application.dataPath, folderName, fileName);
#else
            filePath = Path.Combine(Application.persistentDataPath, fileName);
#endif

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("time,posX,posY,posZ,gazeX,gazeY,gazeZ");
                for (int i = 0; i < rotationLog.Count; i++)
                {
                    writer.WriteLine(timeLog[i] + "," + posLog[i].x + "," + posLog[i].y + "," + posLog[i].z + "," +
                                     gazeLog[i].x + "," + gazeLog[i].y + "," + gazeLog[i].z);
                }

                writer.WriteLine("HMD");

                //HMDの軌跡を記録
                writer.WriteLine("time,posX,posY,posZ,rotX,rotY,rotZ");
                for (int i = 0; i < rotationLog.Count; i++)
                {
                    writer.WriteLine(timeLog[i] + "," + positionLog[i].x + "," + positionLog[i].y + "," + positionLog[i].z + "," +
                                     CenterRotValue(rotationLog[i].x) + "," + CenterRotValue(rotationLog[i].y) + "," + CenterRotValue(rotationLog[i].z));
                }
                positionLog.Clear();
                rotationLog.Clear();
            }

            Debug.Log("Results saved to: " + filePath);
        }
    }

    float CenterRotValue(float value)
    {
        if (value > 180)
        {
            value = value - 360;
        }
        return value;
    }
}
